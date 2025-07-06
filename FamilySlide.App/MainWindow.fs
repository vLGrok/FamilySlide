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
open Serilog
open FolderSettings

type Model =
    { FolderPath: string
      Images: string list
      CurrentIndex: int
      CurrentBitmap: Bitmap option
      FolderSettings: FolderSettings option }

type Msg =
    | NextImage
    | PrevImage
    | LoadImages
    | KeyPressed of Avalonia.Input.Key

module MainWindow =

    let init folderPath =
        Log.Information("Initializing FamilySlide with folder: " + folderPath)
        { FolderPath = folderPath
          Images = []
          CurrentIndex = 0
          CurrentBitmap = None
          FolderSettings = None },
        Cmd.ofMsg LoadImages

    let update msg model =
        Log.Debug("Update called with message: {Message}", msg.ToString())
        match msg with
        | LoadImages ->
            Log.Information("Loading images from folder")
            if Directory.Exists(model.FolderPath) then
                Log.Debug("Folder exists: {Folder}", model.FolderPath)
                
                // Update the last folder path in user settings
                UserSettings.updateLastFolderPath model.FolderPath
                
                let allFiles = Directory.EnumerateFiles(model.FolderPath) |> Seq.toList
                Log.Debug("Total files in folder: {Count}", allFiles.Length)

                let files =
                    allFiles
                    |> List.filter (fun f -> 
                        FamilySlide.Core.ImageLoader.supportedExtensions 
                        |> List.exists (fun ext -> f.EndsWith(ext, System.StringComparison.OrdinalIgnoreCase)))

                Log.Information("Found {Count} image files", files.Length)
                files |> List.iteri (fun i path -> Log.Debug("Image {Index}: {Path}", i, path))

                let first =
                    files
                    |> List.tryHead
                    |> Option.bind (fun path ->
                        Log.Debug("Loading first image: {Path}", path)
                        try
                            use img = Image.Load<Rgba32>(path)
                            use ms = new MemoryStream()
                            img.SaveAsBmp(ms)
                            ms.Position <- 0L
                            Log.Debug("Successfully loaded first image")
                            Some(new Bitmap(ms))
                        with
                        | ex ->
                            Log.Error(ex, "Failed to load first image: {Path}", path)
                            None)

                Log.Information("Image loading completed - {Count} images available, first image loaded: {HasImage}", 
                    files.Length, first.IsSome)

                // Load or create folder settings for these image files
                let folderSettings = FolderSettings.loadOrCreateFolderSettings model.FolderPath files
                Log.Information("Folder settings loaded with {SettingsCount} image entries, save-settings: {SaveSettings}", 
                    folderSettings.Images.Count, folderSettings.SaveImageSettings)

                { model with
                    Images = files
                    CurrentBitmap = first
                    FolderSettings = Some folderSettings },
                Cmd.none
            else
                Log.Warning("Folder does not exist: {Folder}", model.FolderPath)
                // Return early if folder doesn't exist
                { model with Images = []; CurrentBitmap = None; FolderSettings = None }, Cmd.none

        | KeyPressed key ->
            Log.Debug("KeyPressed message received: {Key}", key)
            match key with
            | Avalonia.Input.Key.Right -> 
                Log.Debug("KeyPressed: Right arrow - processing as NextImage")
                // Process NextImage directly
                let nextIndex = min (model.CurrentIndex + 1) (model.Images.Length - 1)
                Log.Debug("NextImage: Current index {CurrentIndex}, New index {NextIndex}, Total images {TotalImages}", 
                    model.CurrentIndex, nextIndex, model.Images.Length)

                let bmp =
                    if model.Images.Length > 0 && nextIndex < model.Images.Length then
                        let path = model.Images[nextIndex]
                        Log.Debug("Loading next image: {Path}", path)
                        try
                            use img = Image.Load<Rgba32>(path)
                            use ms = new MemoryStream()
                            img.SaveAsBmp(ms)
                            ms.Position <- 0L
                            Log.Debug("Successfully loaded next image")
                            Some(new Bitmap(ms))
                        with
                        | ex ->
                            Log.Error(ex, "Failed to load next image: {Path}", path)
                            None
                    else
                        Log.Debug("Cannot load next image - no images or index out of bounds")
                        Log.Warning("Cannot load next image - no images or index out of bounds")
                        None

                { model with
                    CurrentIndex = nextIndex
                    CurrentBitmap = bmp },
                Cmd.none
            | Avalonia.Input.Key.Left -> 
                Log.Debug("KeyPressed: Left arrow - processing as PrevImage")
                // Process PrevImage directly
                let prevIndex = max (model.CurrentIndex - 1) 0
                Log.Debug("PrevImage: Current index {CurrentIndex}, New index {PrevIndex}, Total images {TotalImages}", 
                    model.CurrentIndex, prevIndex, model.Images.Length)

                let bmp =
                    if model.Images.Length > 0 && prevIndex >= 0 && prevIndex < model.Images.Length then
                        let path = model.Images[prevIndex]
                        Log.Debug("Loading previous image: {Path}", path)
                        try
                            use img = Image.Load<Rgba32>(path)
                            use ms = new MemoryStream()
                            img.SaveAsBmp(ms)
                            ms.Position <- 0L
                            Log.Debug("Successfully loaded previous image")
                            Some(new Bitmap(ms))
                        with
                        | ex ->
                            Log.Error(ex, "Failed to load previous image: {Path}", path)
                            None
                    else
                        Log.Debug("Cannot load previous image - no images or index out of bounds")
                        Log.Warning("Cannot load previous image - no images or index out of bounds")
                        None

                { model with
                    CurrentIndex = prevIndex
                    CurrentBitmap = bmp },
                Cmd.none
            | _ -> 
                Log.Debug("KeyPressed: Unhandled key: {Key}", key)
                model, Cmd.none

        | NextImage ->
            Log.Debug("NextImage command executed")
            Log.Debug("NextImage handler entered")
            // Circular navigation for NextImage
            let nextIndex = 
                if model.Images.Length = 0 then 0
                elif model.CurrentIndex >= model.Images.Length - 1 then 0  // Wrap to first
                else model.CurrentIndex + 1
            Log.Debug("NextImage: Current index {CurrentIndex}, New index {NextIndex}, Total images {TotalImages}", 
                model.CurrentIndex, nextIndex, model.Images.Length)

            let bmp =
                if model.Images.Length > 0 && nextIndex < model.Images.Length then
                    let path = model.Images[nextIndex]
                    Log.Debug("Loading next image: {Path}", path)
                    try
                        use img = Image.Load<Rgba32>(path)
                        use ms = new MemoryStream()
                        img.SaveAsBmp(ms)
                        ms.Position <- 0L
                        Log.Debug("Successfully loaded next image")
                        Some(new Bitmap(ms))
                    with
                    | ex ->
                        Log.Error(ex, "Failed to load next image: {Path}", path)
                        None
                else
                    Log.Debug("Cannot load next image - no images or index out of bounds")
                    Log.Warning("Cannot load next image - no images or index out of bounds")
                    None

            Log.Debug("NextImage completed - New index: {Index}, Has image: {HasImage}", 
                nextIndex, bmp.IsSome)
            { model with
                CurrentIndex = nextIndex
                CurrentBitmap = bmp },
            Cmd.none

        | PrevImage ->
            Log.Debug("PrevImage command executed")
            Log.Debug("PrevImage handler entered")
            // Circular navigation for PrevImage
            let prevIndex = 
                if model.Images.Length = 0 then 0
                elif model.CurrentIndex <= 0 then model.Images.Length - 1  // Wrap to last
                else model.CurrentIndex - 1
            Log.Debug("PrevImage: Current index {CurrentIndex}, New index {PrevIndex}, Total images {TotalImages}", 
                model.CurrentIndex, prevIndex, model.Images.Length)

            let bmp =
                if model.Images.Length > 0 && prevIndex >= 0 && prevIndex < model.Images.Length then
                    let path = model.Images[prevIndex]
                    Log.Debug("Loading previous image: {Path}", path)
                    try
                        use img = Image.Load<Rgba32>(path)
                        use ms = new MemoryStream()
                        img.SaveAsBmp(ms)
                        ms.Position <- 0L
                        Log.Debug("Successfully loaded previous image")
                        Some(new Bitmap(ms))
                    with
                    | ex ->
                        Log.Error(ex, "Failed to load previous image: {Path}", path)
                        None
                else
                    Log.Debug("Cannot load previous image - no images or index out of bounds")
                    Log.Warning("Cannot load previous image - no images or index out of bounds")
                    None

            Log.Debug("PrevImage completed - New index: {Index}, Has image: {HasImage}", 
                prevIndex, bmp.IsSome)
            { model with
                CurrentIndex = prevIndex
                CurrentBitmap = bmp },
            Cmd.none

    let view model dispatch =
        Log.Debug("View function called with {ImageCount} images, current index {Index}", 
            model.Images.Length, model.CurrentIndex)
        DockPanel.create
            [ DockPanel.children
                  [ match model.CurrentBitmap with
                    | Some bmp -> 
                        Image.create [ 
                            Image.source bmp
                            Image.stretch Avalonia.Media.Stretch.Uniform
                            Image.focusable true
                            Image.onKeyDown (fun args ->
                                Log.Debug("Image KeyDown fired: {Key}", args.Key)
                                System.Console.WriteLine($"CONSOLE: Image KeyDown fired: {args.Key}")
                                match args.Key with
                                | Avalonia.Input.Key.Right -> 
                                    Log.Information("Navigating to next image")
                                    Log.Debug("Image: Right arrow pressed, dispatching NextImage")
                                    dispatch NextImage
                                | Avalonia.Input.Key.Left -> 
                                    Log.Information("Navigating to previous image") 
                                    Log.Debug("Image: Left arrow pressed, dispatching PrevImage")
                                    dispatch PrevImage
                                | _ -> 
                                    Log.Debug("Image: Other key pressed: {Key}", args.Key)
                            )
                        ]
                    | None -> 
                        TextBlock.create [ 
                            TextBlock.text "No image"
                            TextBlock.focusable true
                            TextBlock.onKeyDown (fun args ->
                                Log.Debug("TextBlock KeyDown fired: {Key}", args.Key)
                                match args.Key with
                                | Avalonia.Input.Key.Right -> 
                                    Log.Information("Navigating to next image")
                                    Log.Debug("TextBlock: Right arrow pressed, dispatching NextImage")
                                    dispatch NextImage
                                | Avalonia.Input.Key.Left -> 
                                    Log.Information("Navigating to previous image")
                                    Log.Debug("TextBlock: Left arrow pressed, dispatching PrevImage")
                                    dispatch PrevImage
                                | _ -> 
                                    Log.Debug("TextBlock: Other key pressed: {Key}", args.Key)
                            )
                        ] ]
              // Add DockPanel-level key handling as a fallback
              DockPanel.focusable true
              DockPanel.onKeyDown (fun args ->
                  Log.Debug("DockPanel KeyDown fired: {Key}", args.Key)
                  System.Console.WriteLine($"CONSOLE: DockPanel KeyDown fired: {args.Key}")
                  match args.Key with
                  | Avalonia.Input.Key.Right -> 
                      Log.Information("Navigating to next image")
                      Log.Debug("DockPanel: Right arrow pressed, dispatching NextImage")
                      dispatch NextImage
                  | Avalonia.Input.Key.Left -> 
                      Log.Information("Navigating to previous image")
                      Log.Debug("DockPanel: Left arrow pressed, dispatching PrevImage")
                      dispatch PrevImage
                  | _ -> 
                      Log.Debug("DockPanel: Other key pressed: {Key}", args.Key)
              ) ]

type MainWindow() as this =
    inherit HostWindow()

    do
        System.Console.WriteLine("ELMISH MAINWINDOW CONSTRUCTOR CALLED!!!")
        System.Console.WriteLine("CONSOLE: MainWindow constructor DO block started")
        Log.Information("FamilySlide MainWindow initializing")
        base.Title <- "FamilySlide"
        base.Width <- 800.0
        base.Height <- 600.0
        base.CanResize <- true
        base.Focusable <- true

        // Get available screens and position on primary screen
        let screens = this.Screens.All
        if screens.Count > 1 then
            let primaryScreen = screens.[0] // Use primary screen (index 0)
            this.WindowStartupLocation <- WindowStartupLocation.Manual
            this.Position <- PixelPoint(primaryScreen.Bounds.X + 100, primaryScreen.Bounds.Y + 100)
            Log.Information("Opening on primary screen: {DisplayName}", primaryScreen.DisplayName)
            System.Console.WriteLine($"Opening on primary screen: {primaryScreen.DisplayName}")
        else
            Log.Information("Single screen detected, using default positioning")
            System.Console.WriteLine("Only one screen detected, using default positioning")

        // Load configuration to get folder path
        let config = Configuration.loadConfiguration [||]
        let folderPath = config.AppSettings.FolderPath
        Log.Information("Using folder path: {Folder}", folderPath)

        try
            let mutable currentModel = None
            
            let updateWindowTitle (model: Model) =
                let title = 
                    if model.Images.Length > 0 && model.CurrentIndex >= 0 && model.CurrentIndex < model.Images.Length then
                        let currentImagePath = model.Images[model.CurrentIndex]
                        let directory = System.IO.Path.GetDirectoryName(currentImagePath)
                        let filename = System.IO.Path.GetFileName(currentImagePath)
                        $"FamilySlide - {directory}    {filename}"
                    else
                        "FamilySlide"
                
                Log.Debug("Updating window title to: {Title}", title)
                this.Title <- title
            
            let customUpdate msg model =
                let newModel, cmd = MainWindow.update msg model
                currentModel <- Some newModel
                updateWindowTitle newModel
                newModel, cmd
            
            let program =
                Elmish.Program.mkProgram (fun _ -> 
                    let model, cmd = MainWindow.init folderPath
                    currentModel <- Some model
                    updateWindowTitle model
                    model, cmd) customUpdate MainWindow.view
                |> Program.withHost this

            Log.Information("Elmish program created successfully")

            // Ensure window gets focus when attached to visual tree
            this.AttachedToVisualTree.Add(fun _ -> 
                Log.Debug("Window attached to visual tree, setting focus")
                this.Focus() |> ignore)

            // Add a simple test to see if ANY key events work at window level
            this.KeyDown.Add(fun args ->
                Log.Debug("Window level KeyDown detected: {Key}", args.Key)
                System.Console.WriteLine($"CONSOLE: WINDOW LEVEL KeyDown detected: {args.Key}")
            )

            Log.Information("Starting Elmish program")
            
            // Start the program
            program |> Program.run
            
            Log.Information("Elmish program started successfully")
        with
        | ex -> 
            Log.Error(ex, "Error in MainWindow initialization")
            reraise()
