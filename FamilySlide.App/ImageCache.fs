namespace FamilySlide.App

open System
open System.Collections.Generic
open Avalonia.Media.Imaging
open Serilog

/// Cache for managing multiple ImageState objects with memory limits
module ImageCache =
    
    /// Maximum number of full-resolution images to keep in memory
    let private maxFullImages = 3
    
    /// Maximum number of thumbnails to keep in memory
    let private maxThumbnails = 50
    
    /// Cache state containing all loaded images
    type CacheState = {
        /// All image states indexed by file path
        Images: Map<string, ImageState>
        /// Recently accessed full images (for LRU eviction)
        RecentFullImages: string list
        /// Recently accessed thumbnails (for LRU eviction)  
        RecentThumbnails: string list
    }
    
    /// Create empty cache state
    let empty = {
        Images = Map.empty
        RecentFullImages = []
        RecentThumbnails = []
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
                Log.Debug("Disposing full image bitmap: {FilePath}", state.Info.FilePath)
                bitmap.Dispose())
            state.Thumbnail |> Option.iter (fun bitmap -> 
                Log.Debug("Disposing thumbnail bitmap: {FilePath}", state.Info.FilePath)
                bitmap.Dispose()))
    
    /// Get ImageState from cache or create new one
    let getImageState (filePath: string) (cache: CacheState) =
        match Map.tryFind filePath cache.Images with
        | Some imageState -> 
            // Update recent thumbnails list
            let newRecentThumbnails = updateRecentList maxThumbnails filePath cache.RecentThumbnails
            imageState, { cache with RecentThumbnails = newRecentThumbnails }
        | None -> 
            // Create new ImageState with thumbnail
            let newImageState = ImageService.loadImageState filePath
            let newImages = Map.add filePath newImageState cache.Images
            let newRecentThumbnails = updateRecentList maxThumbnails filePath cache.RecentThumbnails
            
            // Check if we need to evict old thumbnails
            let imagesToEvict = 
                cache.RecentThumbnails 
                |> List.skip (maxThumbnails - 1)
                |> List.choose (fun path -> Map.tryFind path cache.Images)
            
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
            let newRecentFullImages = updateRecentList maxFullImages filePath cache.RecentFullImages
            
            // Check if we need to evict old full images
            let imagesToEvict = 
                cache.RecentFullImages 
                |> List.skip (maxFullImages - 1)
                |> List.choose (fun path -> Map.tryFind path newImages)
                |> List.filter (fun state -> state.IsFullImageLoaded)
            
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
            let newRecentFullImages = updateRecentList maxFullImages filePath cache.RecentFullImages
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
        
        Log.Debug("Cache stats: {TotalImages} total, {FullImages} full images, {Thumbnails} thumbnails", 
            totalImages, fullImagesLoaded, thumbnailsLoaded)
        
        {| TotalImages = totalImages
           FullImagesLoaded = fullImagesLoaded  
           ThumbnailsLoaded = thumbnailsLoaded
           RecentFullImages = cache.RecentFullImages
           RecentThumbnails = cache.RecentThumbnails |}
    
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
