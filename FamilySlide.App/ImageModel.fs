namespace FamilySlide.App

open System
open Avalonia.Media.Imaging

/// Represents basic image metadata
type ImageInfo = {
    FilePath: string
    FileName: string
    FileSize: int64
    LastModified: DateTime
    Width: int option
    Height: int option
}

/// Represents transformations that can be applied to an image
type Transform = {
    /// Rotation in 90-degree increments (0, 90, 180, 270)
    Rotation: int
    /// Whether the image is flipped horizontally
    FlipHorizontal: bool
    /// Whether the image is flipped vertically
    FlipVertical: bool
    /// Zoom level (1.0 = 100%, 2.0 = 200%, etc.)
    Zoom: float
    /// Pan offset X in pixels
    OffsetX: float
    /// Pan offset Y in pixels
    OffsetY: float
}

/// Represents the current state of an image in the viewer
type ImageState = {
    /// Basic file information
    Info: ImageInfo
    /// Current transformations applied
    Transform: Transform
    /// Cached thumbnail bitmap (256x256 max)
    Thumbnail: Bitmap option
    /// Full resolution bitmap (loaded on demand)
    FullImage: Bitmap option
    /// Whether the full image is currently loaded
    IsFullImageLoaded: bool
    /// Whether there was an error loading this image
    LoadError: string option
}

module Transform =
    /// Default transform state (no modifications)
    let default' = {
        Rotation = 0
        FlipHorizontal = false
        FlipVertical = false
        Zoom = 1.0
        OffsetX = 0.0
        OffsetY = 0.0
    }
    
    /// Reset transform to default state
    let reset _ = default'
    
    /// Rotate image by 90 degrees clockwise
    let rotateRight transform = 
        { transform with Rotation = (transform.Rotation + 90) % 360 }
    
    /// Rotate image by 90 degrees counter-clockwise
    let rotateLeft transform = 
        { transform with Rotation = (transform.Rotation + 270) % 360 }
    
    /// Flip image horizontally
    let flipHorizontal transform = 
        { transform with FlipHorizontal = not transform.FlipHorizontal }
    
    /// Flip image vertically
    let flipVertical transform = 
        { transform with FlipVertical = not transform.FlipVertical }
    
    /// Set zoom level (legacy version with hardcoded limits)
    let setZoom zoom (transform: Transform) = 
        { transform with Zoom = max 0.1 (min 10.0 zoom) }
    
    /// Set zoom level with explicit constraints
    let setZoomWithLimits (minLevel: float) (maxLevel: float) zoom (transform: Transform) = 
        { transform with Zoom = max minLevel (min maxLevel zoom) }
    
    /// Set zoom level using configuration
    let setZoomWithConfig (zoomConfig: ZoomConfig) zoom (transform: Transform) = 
        setZoomWithLimits zoomConfig.MinLevel zoomConfig.MaxLevel zoom transform
    
    /// Set pan offset
    let setPan offsetX offsetY (transform: Transform) = 
        { transform with OffsetX = offsetX; OffsetY = offsetY }

module ImageInfo =
    /// Create ImageInfo from file path
    let fromFilePath filePath =
        let fileInfo = System.IO.FileInfo(filePath)
        {
            FilePath = filePath
            FileName = fileInfo.Name
            FileSize = fileInfo.Length
            LastModified = fileInfo.LastWriteTime
            Width = None  // Will be populated when image is loaded
            Height = None
        }
    
    /// Update ImageInfo with image dimensions
    let withDimensions width height (imageInfo: ImageInfo) =
        { imageInfo with Width = Some width; Height = Some height }

module ImageState =
    /// Create initial ImageState from file path
    let fromFilePath filePath =
        {
            Info = ImageInfo.fromFilePath filePath
            Transform = Transform.default'
            Thumbnail = None
            FullImage = None
            IsFullImageLoaded = false
            LoadError = None
        }
    
    /// Update ImageState with load error
    let withError error (imageState: ImageState) =
        { imageState with LoadError = Some error }
    
    /// Clear error from ImageState
    let clearError (imageState: ImageState) =
        { imageState with LoadError = None }
    
    /// Update ImageState with thumbnail
    let withThumbnail thumbnail (imageState: ImageState) =
        { imageState with Thumbnail = Some thumbnail }
    
    /// Update ImageState with full image
    let withFullImage fullImage (imageState: ImageState) =
        { imageState with 
            FullImage = Some fullImage
            IsFullImageLoaded = true }
    
    /// Clear full image from memory (keep thumbnail)
    let clearFullImage (imageState: ImageState) =
        { imageState with 
            FullImage = None
            IsFullImageLoaded = false }
    
    /// Apply transform to ImageState
    let withTransform transform (imageState: ImageState) =
        { imageState with Transform = transform }
    
    /// Get display bitmap (thumbnail or full image)
    let getDisplayBitmap (imageState: ImageState) =
        match imageState.FullImage with
        | Some fullImage -> Some fullImage
        | None -> imageState.Thumbnail
