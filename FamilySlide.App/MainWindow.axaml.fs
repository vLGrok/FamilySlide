namespace FamilySlide.App

open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml
open FamilySlide.Core.ImageLoader  // ✅ your module

type MainWindow () as this = 
    inherit Window ()

    do
        this.InitializeComponent()

        let folderPath = "/Users/rkerr/Pictures/iPadPhotos/"
        match getFirstImagePath folderPath with
        | Some path -> 
            printfn $"First image path: {path}"
        | None ->
            printfn "No images found."

    member private this.InitializeComponent() =
#if DEBUG
        this.AttachDevTools()
#endif
        AvaloniaXamlLoader.Load(this)
