namespace FamilySlide.App

open System
open System.IO
open Avalonia.Media.Imaging
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Processing
open Serilog

/// Results for image operations
type ImageLoadResult = 
    | Success of Bitmap * int * int  // bitmap, width, height
    | Error of string

/// Service for image file operations
module ImageService =
    
    /// Get all supported image files in a directory
    let getImageFiles (folderPath: string) =
        try
            if Directory.Exists(folderPath) then
                let allFiles = Directory.EnumerateFiles(folderPath) |> Seq.toList
                Log.Debug("Found {Count} total files in folder: {Folder}", allFiles.Length, folderPath)
                
                let imageFiles =
                    allFiles
                    |> List.filter (fun f -> 
                        FamilySlide.Core.ImageLoader.supportedExtensions 
                        |> List.exists (fun ext -> f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                
                Log.Information("Found {Count} image files in folder: {Folder}", imageFiles.Length, folderPath)
                Ok imageFiles
            else
                let error = $"Folder does not exist: {folderPath}"
                Log.Warning(error)
                Result.Error error
        with
        | ex ->
            let error = $"Error reading folder {folderPath}: {ex.Message}"
            Log.Error(ex, "Error reading folder: {Folder}", folderPath)
            Result.Error error
    
    /// Load image bitmap with proper memory management
    let loadImageBitmap (filePath: string) =
        try
            Log.Debug("Loading image bitmap: {FilePath}", filePath)
            
            // Use ImageSharp to load and convert to Avalonia Bitmap
            use img = Image.Load<Rgba32>(filePath)
            let width = img.Width
            let height = img.Height
            
            // Convert to Avalonia Bitmap through memory stream
            use ms = new MemoryStream()
            img.SaveAsBmp(ms)
            ms.Position <- 0L
            
            // Create Avalonia Bitmap from stream
            let bitmap = new Bitmap(ms)
            
            Log.Debug("Successfully loaded image: {FilePath} ({Width}x{Height})", filePath, width, height)
            Success (bitmap, width, height)
            
        with
        | ex ->
            let error = $"Failed to load image {Path.GetFileName(filePath)}: {ex.Message}"
            Log.Error(ex, "Failed to load image: {FilePath}", filePath)
            Error error
    
    /// Load thumbnail bitmap (max 256x256)
    let loadThumbnailBitmap (filePath: string) =
        try
            Log.Debug("Loading thumbnail for: {FilePath}", filePath)
            
            use img = Image.Load<Rgba32>(filePath)
            let originalWidth = img.Width
            let originalHeight = img.Height
            
            // Calculate thumbnail size maintaining aspect ratio
            let maxSize = 256
            let scale = min (float maxSize / float originalWidth) (float maxSize / float originalHeight)
            let thumbWidth = int (float originalWidth * scale)
            let thumbHeight = int (float originalHeight * scale)
            
            // Resize image
            img.Mutate(fun x -> x.Resize(thumbWidth, thumbHeight) |> ignore)
            
            // Convert to Avalonia Bitmap
            use ms = new MemoryStream()
            img.SaveAsBmp(ms)
            ms.Position <- 0L
            let bitmap = new Bitmap(ms)
            
            Log.Debug("Created thumbnail: {FilePath} ({ThumbWidth}x{ThumbHeight} from {OrigWidth}x{OrigHeight})", 
                filePath, thumbWidth, thumbHeight, originalWidth, originalHeight)
            Success (bitmap, originalWidth, originalHeight)
            
        with
        | ex ->
            let error = $"Failed to create thumbnail for {Path.GetFileName(filePath)}: {ex.Message}"
            Log.Error(ex, "Failed to create thumbnail: {FilePath}", filePath)
            Error error
    
    /// Load ImageState from file path with thumbnail
    let loadImageState (filePath: string) =
        let imageState = ImageState.fromFilePath filePath
        
        match loadThumbnailBitmap filePath with
        | Success (thumbnail, width, height) ->
            imageState
            |> ImageState.withThumbnail thumbnail
            |> ImageState.clearError
            |> fun state -> 
                { state with Info = ImageInfo.withDimensions width height state.Info }
        | Error error ->
            imageState 
            |> ImageState.withError error
    
    /// Load full resolution image for an ImageState
    let loadFullImage (imageState: ImageState) =
        if imageState.IsFullImageLoaded then
            // Already loaded
            imageState
        else
            match loadImageBitmap imageState.Info.FilePath with
            | Success (bitmap, width, height) ->
                imageState
                |> ImageState.withFullImage bitmap
                |> fun state -> 
                    { state with Info = ImageInfo.withDimensions width height state.Info }
            | Error error ->
                imageState |> ImageState.withError error
    
    /// Clear full image from memory to free up space
    let unloadFullImage (imageState: ImageState) =
        if imageState.IsFullImageLoaded then
            Log.Debug("Unloading full image from memory: {FilePath}", imageState.Info.FilePath)
            // Dispose the bitmap if it exists
            imageState.FullImage |> Option.iter (fun bitmap -> bitmap.Dispose())
            ImageState.clearFullImage imageState
        else
            imageState
    
    /// Get file information for an image
    let getImageInfo (filePath: string) =
        try
            let fileInfo = FileInfo(filePath)
            if fileInfo.Exists then
                Ok (ImageInfo.fromFilePath filePath)
            else
                Result.Error $"File not found: {filePath}"
        with
        | ex ->
            Result.Error $"Error reading file info for {filePath}: {ex.Message}"
    
    /// Delete an image file
    let deleteImageFile (filePath: string) =
        try
            if File.Exists(filePath) then
                File.Delete(filePath)
                Log.Information("Deleted image file: {FilePath}", filePath)
                Ok ()
            else
                Result.Error $"File not found: {filePath}"
        with
        | ex ->
            let error = $"Failed to delete {filePath}: {ex.Message}"
            Log.Error(ex, "Failed to delete image file: {FilePath}", filePath)
            Result.Error error
    
    /// Check if a file is a supported image format
    let isSupportedImageFile (filePath: string) =
        FamilySlide.Core.ImageLoader.supportedExtensions 
        |> List.exists (fun ext -> filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
