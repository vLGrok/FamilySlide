namespace FamilySlide.App

open System
open System.Collections.Generic
open System.IO
open Avalonia.Media.Imaging
open Serilog

/// Cache for managing multiple ImageState objects with memory limits
module ImageCache =
    
    /// Cache state containing all loaded images and configuration
    type CacheState = {
        /// All image states indexed by file path
        Images: Map<string, ImageState>
        /// Recently accessed full images (for LRU eviction)
        RecentFullImages: string list
        /// Recently accessed thumbnails (for LRU eviction)  
        RecentThumbnails: string list
        /// Configuration settings
        MaxFullImages: int
        MaxThumbnails: int
        ImageConfig: ImageConfig
        /// Memory monitoring state
        MemoryMonitoring: MemoryManager.MonitoringState
        /// Full cache configuration
        CacheConfig: CacheConfig
    }
    
    /// Create cache state with configuration
    let createCache (cacheConfig: CacheConfig) (imageConfig: ImageConfig) = {
        Images = Map.empty
        RecentFullImages = []
        RecentThumbnails = []
        MaxFullImages = cacheConfig.MaxFullImages
        MaxThumbnails = cacheConfig.MaxThumbnails
        ImageConfig = imageConfig
        MemoryMonitoring = MemoryManager.createMonitoringState()
        CacheConfig = cacheConfig
    }
    
    /// Create cache with default settings (for backward compatibility)
    let empty = {
        Images = Map.empty
        RecentFullImages = []
        RecentThumbnails = []
        MaxFullImages = 3
        MaxThumbnails = 50
        ImageConfig = { ThumbnailMaxSize = 256 }
        MemoryMonitoring = MemoryManager.createMonitoringState()
        CacheConfig = {
            MaxFullImages = 3
            MaxThumbnails = 50
            MaxMemoryMB = 512
            LowMemoryThresholdMB = 256
            AggressiveCleanupThresholdMB = 128
            PreloadNeighborImages = true
            BackgroundCleanupIntervalMinutes = 10
            IdleCleanupDelayMinutes = 2
            EnablePeriodicCleanup = true
        }
    }
    
    /// Add image to recent list and trim to max size
    let private updateRecentList maxSize imagePath recentList =
        imagePath :: (recentList |> List.filter ((<>) imagePath))
        |> List.truncate maxSize
    
    /// Dispose bitmaps that are being evicted from cache
    let private disposeBitmaps (imageStates: ImageState list) =
        imageStates
        |> List.iter (fun state ->
            state.FullImage |> Option.iter (fun bitmap -> 
                let context = $"Cache evict full: {Path.GetFileName(state.Info.FilePath)}"
                BitmapLifecycle.disposeBitmap bitmap context)
            state.Thumbnail |> Option.iter (fun bitmap -> 
                let context = $"Cache evict thumb: {Path.GetFileName(state.Info.FilePath)}"
                BitmapLifecycle.disposeBitmap bitmap context))
    
    /// Get ImageState from cache or create new one
    let getImageState (filePath: string) (cache: CacheState) =
        match Map.tryFind filePath cache.Images with
        | Some imageState -> 
            // Update recent thumbnails list
            let newRecentThumbnails = updateRecentList cache.MaxThumbnails filePath cache.RecentThumbnails
            imageState, { cache with RecentThumbnails = newRecentThumbnails }
        | None -> 
            // Create new ImageState with thumbnail
            let newImageState = ImageService.loadImageState cache.ImageConfig filePath
            let newImages = Map.add filePath newImageState cache.Images
            let newRecentThumbnails = updateRecentList cache.MaxThumbnails filePath cache.RecentThumbnails
            
            // Check if we need to evict old thumbnails
            let imagesToEvict = 
                if cache.RecentThumbnails.Length > cache.MaxThumbnails then
                    cache.RecentThumbnails 
                    |> List.skip (cache.MaxThumbnails - 1)
                    |> List.choose (fun path -> Map.tryFind path cache.Images)
                else
                    []
            
            // Dispose evicted thumbnails
            disposeBitmaps imagesToEvict
            
            // Remove evicted images from cache
            let cleanedImages = 
                imagesToEvict
                |> List.fold (fun acc state -> Map.remove state.Info.FilePath acc) newImages
            
            let newCache = { 
                cache with 
                    Images = cleanedImages
                    RecentThumbnails = newRecentThumbnails 
            }
            
            Log.Debug("Added image to cache: {FilePath}", filePath)
            newImageState, newCache
    
    /// Load full resolution image for ImageState and update cache
    let rec loadFullImage (filePath: string) (cache: CacheState) =
        match Map.tryFind filePath cache.Images with
        | Some imageState when not imageState.IsFullImageLoaded ->
            // Load full image
            let updatedImageState = ImageService.loadFullImage imageState
            let newImages = Map.add filePath updatedImageState cache.Images
            let newRecentFullImages = updateRecentList cache.MaxFullImages filePath cache.RecentFullImages
            
            // Check if we need to evict old full images
            let imagesToEvict = 
                if cache.RecentFullImages.Length > cache.MaxFullImages then
                    cache.RecentFullImages 
                    |> List.skip (cache.MaxFullImages - 1)
                    |> List.choose (fun path -> Map.tryFind path newImages)
                    |> List.filter (fun state -> state.IsFullImageLoaded)
                else
                    []
            
            // Unload evicted full images (keep thumbnails)
            let cleanedImages = 
                imagesToEvict
                |> List.fold (fun acc state -> 
                    let unloadedState = ImageService.unloadFullImage state
                    Map.add state.Info.FilePath unloadedState acc) newImages
            
            let newCache = { 
                cache with 
                    Images = cleanedImages
                    RecentFullImages = newRecentFullImages 
            }
            
            Log.Debug("Loaded full image: {FilePath}", filePath)
            updatedImageState, newCache
            
        | Some imageState ->
            // Already loaded, just update recent list
            let newRecentFullImages = updateRecentList cache.MaxFullImages filePath cache.RecentFullImages
            imageState, { cache with RecentFullImages = newRecentFullImages }
            
        | None ->
            // Image not in cache, load it first then load full image
            let newImageState, cacheWithImage = getImageState filePath cache
            // Check if the imageState was loaded successfully and has a thumbnail
            if newImageState.Thumbnail.IsSome then
                loadFullImage filePath cacheWithImage
            else
                // Failed to load even thumbnail, return as-is
                newImageState, cacheWithImage
    
    /// Update ImageState in cache (for transforms, etc.)
    let updateImageState (imageState: ImageState) (cache: CacheState) =
        let newImages = Map.add imageState.Info.FilePath imageState cache.Images
        { cache with Images = newImages }
    
    /// Get list of all cached image file paths
    let getCachedPaths (cache: CacheState) =
        cache.Images |> Map.keys |> List.ofSeq
    
    /// Get cache statistics for debugging
    let getStats (cache: CacheState) =
        let totalImages = cache.Images.Count
        let fullImagesLoaded = 
            cache.Images 
            |> Map.values 
            |> Seq.filter (fun state -> state.IsFullImageLoaded)
            |> Seq.length
        let thumbnailsLoaded = 
            cache.Images 
            |> Map.values 
            |> Seq.filter (fun state -> state.Thumbnail.IsSome)
            |> Seq.length
        
        // Also log bitmap lifecycle stats
        let bitmapStats = BitmapLifecycle.logStats "Cache Stats"
        
        Log.Debug("Cache stats: {TotalImages} total, {FullImages} full images, {Thumbnails} thumbnails", 
            totalImages, fullImagesLoaded, thumbnailsLoaded)
        
        {| TotalImages = totalImages
           FullImagesLoaded = fullImagesLoaded  
           ThumbnailsLoaded = thumbnailsLoaded
           RecentFullImages = cache.RecentFullImages
           RecentThumbnails = cache.RecentThumbnails
           BitmapStats = bitmapStats |}
    
    /// Clear all cached images and dispose bitmaps
    let clear (cache: CacheState) =
        let allImages = cache.Images |> Map.values |> List.ofSeq
        disposeBitmaps allImages
        Log.Information("Cleared image cache")
        empty
    
    /// Remove specific image from cache
    let removeImage (filePath: string) (cache: CacheState) =
        match Map.tryFind filePath cache.Images with
        | Some imageState ->
            disposeBitmaps [imageState]
            let newImages = Map.remove filePath cache.Images
            let newRecentFullImages = cache.RecentFullImages |> List.filter ((<>) filePath)
            let newRecentThumbnails = cache.RecentThumbnails |> List.filter ((<>) filePath)
            
            Log.Debug("Removed image from cache: {FilePath}", filePath)
            { cache with 
                Images = newImages
                RecentFullImages = newRecentFullImages
                RecentThumbnails = newRecentThumbnails }
        | None -> 
            cache
    
    /// Clear only full images from cache (keep thumbnails)
    let clearFullImages (cache: CacheState) =
        let updatedImages = 
            cache.Images
            |> Map.map (fun _ imageState ->
                if imageState.IsFullImageLoaded then
                    imageState.FullImage |> Option.iter (fun bitmap ->
                        let context = $"Clear full: {Path.GetFileName(imageState.Info.FilePath)}"
                        BitmapLifecycle.disposeBitmap bitmap context)
                    ImageState.clearFullImage imageState
                else
                    imageState)
        
        Log.Information("Cleared all full images from cache")
        { cache with 
            Images = updatedImages
            RecentFullImages = [] }
    
    /// Emergency cache clear for low memory situations
    let emergencyClear (cache: CacheState) =
        let allImages = cache.Images |> Map.values |> List.ofSeq
        disposeBitmaps allImages
        BitmapLifecycle.logStats "Emergency Clear" |> ignore
        Log.Warning("Emergency cache clear performed due to memory pressure")
        { cache with 
            Images = Map.empty
            RecentFullImages = []
            RecentThumbnails = [] }
    
    /// Check memory pressure and perform cleanup if needed
    let checkMemoryPressureAndCleanup (cache: CacheState) =
        let stats = MemoryManager.getMemoryStats()
        let pressure = MemoryManager.assessMemoryPressure cache.CacheConfig stats
        
        MemoryManager.logMemoryStats "Cache Check" stats
        
        let shouldCleanup = MemoryManager.shouldTriggerCleanup cache.MemoryMonitoring pressure
        
        if shouldCleanup then
            Log.Information("Memory pressure detected: {Pressure}, triggering cleanup", pressure)
            let actions = MemoryManager.getRecommendedCleanupActions pressure
            
            let updatedCache = 
                actions
                |> List.fold (fun acc action ->
                    match action with
                    | MemoryManager.ClearOldThumbnails ->
                        // Clear thumbnails that are older in the LRU list
                        let keepCount = acc.MaxThumbnails / 2
                        let toRemove = acc.RecentThumbnails |> List.skip keepCount
                        toRemove |> List.fold (fun c path -> removeImage path c) acc
                    | MemoryManager.ClearAllFullImages ->
                        clearFullImages acc
                    | MemoryManager.ClearAllThumbnails ->
                        let allImages = acc.Images |> Map.values |> List.ofSeq
                        allImages
                        |> List.iter (fun state ->
                            state.Thumbnail |> Option.iter (fun bitmap ->
                                let context = $"Clear all thumbs: {Path.GetFileName(state.Info.FilePath)}"
                                BitmapLifecycle.disposeBitmap bitmap context))
                        { acc with 
                            Images = acc.Images |> Map.map (fun _ state -> { state with Thumbnail = None })
                            RecentThumbnails = [] }
                    | MemoryManager.EmergencyFullClear ->
                        emergencyClear acc
                    | MemoryManager.ForceGC ->
                        MemoryManager.forceGarbageCollection "Cache Cleanup" |> ignore
                        acc
                ) cache
            
            let newMonitoringState = 
                MemoryManager.updateMonitoringState cache.MemoryMonitoring stats pressure true
            
            { updatedCache with MemoryMonitoring = newMonitoringState }
        else
            let newMonitoringState = 
                MemoryManager.updateMonitoringState cache.MemoryMonitoring stats pressure false
            
            { cache with MemoryMonitoring = newMonitoringState }
    
    /// Manual memory check and cleanup (can be called from UI)
    let manualMemoryCleanup (cache: CacheState) =
        checkMemoryPressureAndCleanup cache
    
    /// Get memory statistics for the cache
    let getMemoryInfo (cache: CacheState) =
        let stats = MemoryManager.getMemoryStats()
        let pressure = MemoryManager.assessMemoryPressure cache.CacheConfig stats
        MemoryManager.logMemoryStats "Manual Check" stats
        {| Stats = stats; Pressure = pressure |}
