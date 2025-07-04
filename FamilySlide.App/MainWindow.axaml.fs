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

    let mainImage: Avalonia.Controls.Image = this.FindControl("MainImage")

    do
        this.InitializeComponent()

        let folderPath = "/Users/rkerr/Pictures/iPadPhotos/"

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
