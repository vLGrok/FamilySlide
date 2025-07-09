namespace FamilySlide.App

open System
open System.IO
open Avalonia.Media.Imaging
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Processing
open Serilog

/// Module for applying transformations to image bitmaps
module ImageTransform =
    
    /// Apply a Transform to a bitmap and return a new transformed bitmap
    let applyTransform (transform: Transform) (originalBitmap: Bitmap) =
        try
            Log.Debug("Applying transform - Rotation: {Rotation}, FlipH: {FlipH}, FlipV: {FlipV}", 
                transform.Rotation, transform.FlipHorizontal, transform.FlipVertical)
            
            // If no transformations needed, return original
            if transform.Rotation = 0 && not transform.FlipHorizontal && not transform.FlipVertical then
                originalBitmap
            else
                // Convert Avalonia Bitmap to ImageSharp Image for processing
                use memoryStream = new MemoryStream()
                originalBitmap.Save(memoryStream)
                memoryStream.Position <- 0L
                
                use image = Image.Load<Rgba32>(memoryStream)
                
                // Apply transformations
                image.Mutate(fun ctx ->
                    // Apply rotation first
                    match transform.Rotation with
                    | 90 -> ctx.RotateFlip(RotateMode.Rotate90, FlipMode.None) |> ignore
                    | 180 -> ctx.RotateFlip(RotateMode.Rotate180, FlipMode.None) |> ignore  
                    | 270 -> ctx.RotateFlip(RotateMode.Rotate270, FlipMode.None) |> ignore
                    | _ -> () // 0 degrees or invalid
                    
                    // Apply flips
                    if transform.FlipHorizontal && transform.FlipVertical then
                        ctx.RotateFlip(RotateMode.None, FlipMode.Horizontal) |> ignore
                        ctx.RotateFlip(RotateMode.None, FlipMode.Vertical) |> ignore
                    elif transform.FlipHorizontal then
                        ctx.RotateFlip(RotateMode.None, FlipMode.Horizontal) |> ignore
                    elif transform.FlipVertical then
                        ctx.RotateFlip(RotateMode.None, FlipMode.Vertical) |> ignore
                )
                
                // Convert back to Avalonia Bitmap
                use outputStream = new MemoryStream()
                image.SaveAsBmp(outputStream)
                outputStream.Position <- 0L
                
                let transformedBitmap = new Bitmap(outputStream)
                Log.Debug("Successfully applied transform to bitmap")
                transformedBitmap
                
        with
        | ex ->
            Log.Error(ex, "Failed to apply transform to bitmap")
            // Return original bitmap if transformation fails
            originalBitmap
    
    /// Get a transformed version of an ImageState's display bitmap
    let getTransformedBitmap (imageState: ImageState) =
        match ImageState.getDisplayBitmap imageState with
        | Some bitmap ->
            let transform = imageState.Transform
            // Only apply transform if there are actual transformations
            if transform.Rotation <> 0 || transform.FlipHorizontal || transform.FlipVertical then
                Some (applyTransform transform bitmap)
            else
                Some bitmap
        | None -> None
    
    /// Apply zoom and pan transformations (these don't modify the bitmap, just the display)
    let getViewTransform (imageState: ImageState) =
        let transform = imageState.Transform
        let zoomScale = Transform.getZoomScale transform
        {| ZoomScale = zoomScale
           ZoomPercent = transform.ZoomPercent
           OffsetX = transform.OffsetX  
           OffsetY = transform.OffsetY
           HasZoomPan = transform.ZoomPercent <> 100 || transform.OffsetX <> 0.0 || transform.OffsetY <> 0.0 |}
    
    /// Calculate zoom to fit image within given dimensions
    let calculateFitZoom (imageWidth: int) (imageHeight: int) (containerWidth: float) (containerHeight: float) =
        let scaleX = containerWidth / float imageWidth
        let scaleY = containerHeight / float imageHeight
        min scaleX scaleY
    
    /// Calculate zoom to fit width within given width
    let calculateFitWidthZoom (imageWidth: int) (containerWidth: float) =
        containerWidth / float imageWidth
    
    /// Calculate zoom to fit height within given height  
    let calculateFitHeightZoom (imageHeight: int) (containerHeight: float) =
        containerHeight / float imageHeight
    
    /// Create transform for zoom to fit
    let createFitTransform (imageState: ImageState) (containerWidth: float) (containerHeight: float) =
        match imageState.Info.Width, imageState.Info.Height with
        | Some width, Some height ->
            let zoom = calculateFitZoom width height containerWidth containerHeight
            Transform.setZoom zoom imageState.Transform
            |> Transform.setPan 0.0 0.0  // Center the image
        | _ -> imageState.Transform
    
    /// Create transform for zoom to fit width
    let createFitWidthTransform (imageState: ImageState) (containerWidth: float) =
        match imageState.Info.Width with
        | Some width ->
            let zoom = calculateFitWidthZoom width containerWidth
            Transform.setZoom zoom imageState.Transform
            |> Transform.setPan 0.0 0.0
        | _ -> imageState.Transform
    
    /// Create transform for zoom to fit height
    let createFitHeightTransform (imageState: ImageState) (containerHeight: float) =
        match imageState.Info.Height with
        | Some height ->
            let zoom = calculateFitHeightZoom height containerHeight
            Transform.setZoom zoom imageState.Transform  
            |> Transform.setPan 0.0 0.0
        | _ -> imageState.Transform
    
    /// Create transform for actual size (100% zoom)
    let createActualSizeTransform (imageState: ImageState) =
        Transform.setZoom 1.0 imageState.Transform
        |> Transform.setPan 0.0 0.0
    
    /// Zoom in by a factor (e.g., 1.5x) - converts to additive percentage
    let zoomIn (factor: float) (imageState: ImageState) =
        let currentZoomPercent = imageState.Transform.ZoomPercent
        let newZoomPercent = int (float currentZoomPercent * factor)
        Transform.setZoomPercent 10 1000 newZoomPercent imageState.Transform
    
    /// Zoom out by a factor (e.g., 0.75x) - converts to additive percentage
    let zoomOut (factor: float) (imageState: ImageState) =
        let currentZoomPercent = imageState.Transform.ZoomPercent
        let newZoomPercent = int (float currentZoomPercent * factor)
        Transform.setZoomPercent 10 1000 newZoomPercent imageState.Transform
    
    /// Center image by resetting pan to 0,0
    let centerImage (imageState: ImageState) =
        Transform.setPan 0.0 0.0 imageState.Transform
    
    /// Check if image has any transformations applied
    let hasTransformations (imageState: ImageState) =
        let t = imageState.Transform
        t.Rotation <> 0 || t.FlipHorizontal || t.FlipVertical || 
        t.ZoomPercent <> 100 || t.OffsetX <> 0.0 || t.OffsetY <> 0.0
    
    /// Get human-readable description of current transformations
    let getTransformDescription (imageState: ImageState) =
        let t = imageState.Transform
        let parts = []
        let parts = if t.Rotation <> 0 then $"Rotated {t.Rotation}°" :: parts else parts
        let parts = if t.FlipHorizontal then "Flipped Horizontally" :: parts else parts
        let parts = if t.FlipVertical then "Flipped Vertically" :: parts else parts
        let parts = if t.ZoomPercent <> 100 then sprintf "Zoom %d%%" t.ZoomPercent :: parts else parts
        let parts = if t.OffsetX <> 0.0 || t.OffsetY <> 0.0 then $"Pan ({t.OffsetX:F0}, {t.OffsetY:F0})" :: parts else parts
        
        if List.isEmpty parts then "No transformations"
        else String.Join(", ", List.rev parts)
