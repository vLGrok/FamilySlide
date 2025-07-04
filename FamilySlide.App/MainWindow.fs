namespace FamilySlide.App

open Avalonia
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI.Elmish
open Elmish
open FamilySlide.Core.ImageLoader
open Avalonia.Media.Imaging
open System.IO
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats

type Model =
    { FolderPath: string
      Images: string list
      CurrentIndex: int
      CurrentBitmap: Bitmap option }

type Msg =
    | NextImage
    | PrevImage
    | LoadImages

module MainWindow =

    let init folderPath =
        { FolderPath = folderPath
          Images = []
          CurrentIndex = 0
          CurrentBitmap = None },
        Cmd.ofMsg LoadImages

    let update msg model =
        match msg with
        | LoadImages ->
            let files =
                Directory.EnumerateFiles(model.FolderPath)
                |> Seq.filter (fun f -> ImageLoader.supportedExtensions |> List.exists (f.EndsWith))
                |> Seq.toList

            let first =
                files
                |> List.tryHead
                |> Option.bind (fun path ->
                    use img = Image.Load<Rgba32>(path)
                    use ms = new MemoryStream()
                    img.SaveAsBmp(ms)
                    ms.Position <- 0L
                    Some(new Bitmap(ms)))

            { model with
                Images = files
                CurrentBitmap = first },
            Cmd.none

        | NextImage ->
            let nextIndex = min (model.CurrentIndex + 1) (model.Images.Length - 1)

            let bmp =
                if model.Images.Length > 0 then
                    let path = model.Images[nextIndex]
                    use img = Image.Load<Rgba32>(path)
                    use ms = new MemoryStream()
                    img.SaveAsBmp(ms)
                    ms.Position <- 0L
                    Some(new Bitmap(ms))
                else
                    None

            { model with
                CurrentIndex = nextIndex
                CurrentBitmap = bmp },
            Cmd.none

        | PrevImage ->
            let prevIndex = max (model.CurrentIndex - 1) 0

            let bmp =
                if model.Images.Length > 0 then
                    let path = model.Images[prevIndex]
                    use img = Image.Load<Rgba32>(path)
                    use ms = new MemoryStream()
                    img.SaveAsBmp(ms)
                    ms.Position <- 0L
                    Some(new Bitmap(ms))
                else
                    None

            { model with
                CurrentIndex = prevIndex
                CurrentBitmap = bmp },
            Cmd.none

    let view model dispatch =
        DockPanel.create
            [ DockPanel.children
                  [ match model.CurrentBitmap with
                    | Some bmp -> Image.create [ Image.source bmp; Image.stretch Avalonia.Media.Stretch.Uniform ]
                    | None -> TextBlock.create [ TextBlock.text "No image" ] ] ]

type MainWindow() as this =
    inherit HostWindow()

    do
        base.Title <- "FamilySlide"
        base.Width <- 800.0
        base.Height <- 600.0
        let folderPath = "/Users/rkerr/Pictures/TestImages"

        let program =
            Elmish.Program.mkProgram (fun _ -> MainWindow.init folderPath) MainWindow.update MainWindow.view
            |> Program.withHost this

        // ✅ Add KeyDown hook BEFORE running the loop
        this.KeyDown.Add(fun args ->
            match args.Key with
            | Avalonia.Input.Key.Right -> program.Dispatch NextImage
            | Avalonia.Input.Key.Left -> program.Dispatch PrevImage
            | _ -> ())

        program |> Program.run // This will start the Elmish program and render the UI
