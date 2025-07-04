namespace FamilySlide.App

open System.IO
open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml
open Avalonia.Media.Imaging
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Formats
open SixLabors.ImageSharp.Processing
open FamilySlide.Core.ImageLoader // ✅ your module

type MainWindow() as this =
    inherit Window()

    do
        this.InitializeComponent()

        // Get available screens and position on secondary screen if available
        let screens = this.Screens.All

        if screens.Count > 1 then
            let secondaryScreen = screens.[0] // Use primary screen
            this.WindowStartupLocation <- WindowStartupLocation.Manual
            this.Position <- PixelPoint(secondaryScreen.Bounds.X + 100, secondaryScreen.Bounds.Y + 100)
            printfn $"Opening on secondary screen: {secondaryScreen.DisplayName}"
        else
            printfn "Only one screen detected, using default positioning"

        let mainImage: Avalonia.Controls.Image = this.FindControl("MainImage")
        let folderPath = "/Users/rkerr/Pictures/iPadPhotos"

        match getFirstImagePath folderPath with
        | Some path ->
            printfn $"First image path: {path}"

            // Load image bytes
            use image = SixLabors.ImageSharp.Image.Load<Rgba32>(path)

            // Copy to Avalonia Bitmap
            use ms = new MemoryStream()
            image.SaveAsBmp(ms)
            ms.Position <- 0L

            let bmp = new Bitmap(ms)
            mainImage.Source <- bmp

        | None -> printfn "No images found."

    member private this.InitializeComponent() =
#if DEBUG
        this.AttachDevTools()
#endif
        AvaloniaXamlLoader.Load(this)
