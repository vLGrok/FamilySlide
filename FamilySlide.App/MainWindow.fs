namespace FamilySlide.App

open Avalonia
open Avalonia.Controls
open Avalonia.Layout
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
    // Menu and toolbar commands (non-functional for now)
    | MenuQuit
    | MenuOpenFolder
    | MenuAbout
    // Navigation
    | ToolbarLeft
    | ToolbarRight
    // Zoom
    | ToolbarZoomOut
    | ToolbarZoomIn
    | ToolbarZoomFit
    | ToolbarZoomFitWidth
    | ToolbarZoomFitHeight
    | ToolbarZoomActual
    // Transform
    | ToolbarRotateLeft
    | ToolbarRotateRight
    | ToolbarFlipHorizontal
    | ToolbarFlipVertical
    // View
    | ToolbarCenter
    | ToolbarRestore
    | ToolbarFullscreen
    // Slideshow
    | ToolbarSlideshow

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

        // Menu commands (non-functional for now)
        | MenuQuit ->
            Log.Information("Menu Quit clicked (not implemented)")
            model, Cmd.none
        | MenuOpenFolder ->
            Log.Information("Menu Open Folder clicked (not implemented)")
            model, Cmd.none
        | MenuAbout ->
            Log.Information("Menu About clicked (not implemented)")
            model, Cmd.none

        // Toolbar commands (non-functional for now)
        | ToolbarLeft ->
            Log.Information("Toolbar Left clicked (not implemented)")
            model, Cmd.none
        | ToolbarRight ->
            Log.Information("Toolbar Right clicked (not implemented)")
            model, Cmd.none
        | ToolbarZoomOut ->
            Log.Information("Toolbar Zoom Out clicked (not implemented)")
            model, Cmd.none
        | ToolbarZoomIn ->
            Log.Information("Toolbar Zoom In clicked (not implemented)")
            model, Cmd.none
        | ToolbarZoomFit ->
            Log.Information("Toolbar Zoom Fit clicked (not implemented)")
            model, Cmd.none
        | ToolbarZoomFitWidth ->
            Log.Information("Toolbar Zoom Fit Width clicked (not implemented)")
            model, Cmd.none
        | ToolbarZoomFitHeight ->
            Log.Information("Toolbar Zoom Fit Height clicked (not implemented)")
            model, Cmd.none
        | ToolbarZoomActual ->
            Log.Information("Toolbar Zoom Actual clicked (not implemented)")
            model, Cmd.none
        | ToolbarRotateLeft ->
            Log.Information("Toolbar Rotate Left clicked (not implemented)")
            model, Cmd.none
        | ToolbarRotateRight ->
            Log.Information("Toolbar Rotate Right clicked (not implemented)")
            model, Cmd.none
        | ToolbarFlipHorizontal ->
            Log.Information("Toolbar Flip Horizontal clicked (not implemented)")
            model, Cmd.none
        | ToolbarFlipVertical ->
            Log.Information("Toolbar Flip Vertical clicked (not implemented)")
            model, Cmd.none
        | ToolbarCenter ->
            Log.Information("Toolbar Center clicked (not implemented)")
            model, Cmd.none
        | ToolbarRestore ->
            Log.Information("Toolbar Restore clicked (not implemented)")
            model, Cmd.none
        | ToolbarFullscreen ->
            Log.Information("Toolbar Fullscreen clicked (not implemented)")
            model, Cmd.none
        | ToolbarSlideshow ->
            Log.Information("Toolbar Slideshow clicked (not implemented)")
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
        
        // Main layout with menu and toolbar
        DockPanel.create [
            DockPanel.children [
                // Menu bar at top
                Menu.create [
                    DockPanel.dock Dock.Top
                    Menu.viewItems [
                        MenuItem.create [
                            MenuItem.header "File"
                            MenuItem.viewItems [
                                MenuItem.create [
                                    MenuItem.header "Open Folder..."
                                    MenuItem.onClick (fun _ -> dispatch MenuOpenFolder)
                                ]
                                MenuItem.create [
                                    MenuItem.header "-" // Separator
                                ]
                                MenuItem.create [
                                    MenuItem.header "Quit"
                                    MenuItem.onClick (fun _ -> dispatch MenuQuit)
                                ]
                            ]
                        ]
                        MenuItem.create [
                            MenuItem.header "Help"
                            MenuItem.viewItems [
                                MenuItem.create [
                                    MenuItem.header "About"
                                    MenuItem.onClick (fun _ -> dispatch MenuAbout)
                                ]
                            ]
                        ]
                    ]
                ]

                // Toolbar at top (below menu)
                StackPanel.create [
                    DockPanel.dock Dock.Top
                    StackPanel.orientation Orientation.Horizontal
                    StackPanel.margin (0, 4, 0, 4)
                    StackPanel.children [
                        // Navigation group
                        Button.create [
                            Button.content "◀"
                            Button.width 40
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarLeft)
                            ToolTip.tip "Previous Image"
                        ]
                        Button.create [
                            Button.content "▶"
                            Button.width 40
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarRight)
                            ToolTip.tip "Next Image"
                        ]
                        
                        // Separator
                        Border.create [
                            Border.width 1
                            Border.height 20
                            Border.margin (4, 5)
                            Border.background "#CCCCCC"
                        ]
                        
                        // Zoom group
                        Button.create [
                            Button.content "−"
                            Button.width 40
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarZoomOut)
                            ToolTip.tip "Zoom Out"
                        ]
                        Button.create [
                            Button.content "+"
                            Button.width 40
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarZoomIn)
                            ToolTip.tip "Zoom In"
                        ]
                        Button.create [
                            Button.content "⊞"
                            Button.width 40
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarZoomFit)
                            ToolTip.tip "Zoom to Fit"
                        ]
                        Button.create [
                            Button.content "↔"
                            Button.width 40
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarZoomFitWidth)
                            ToolTip.tip "Fit Width"
                        ]
                        Button.create [
                            Button.content "↕"
                            Button.width 40
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarZoomFitHeight)
                            ToolTip.tip "Fit Height"
                        ]
                        Button.create [
                            Button.content "1:1"
                            Button.width 40
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarZoomActual)
                            ToolTip.tip "Actual Size"
                        ]
                        
                        // Separator
                        Border.create [
                            Border.width 1
                            Border.height 20
                            Border.margin (4, 5)
                            Border.background "#CCCCCC"
                        ]
                        
                        // Transform group
                        Button.create [
                            Button.content "↶"
                            Button.width 40
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarRotateLeft)
                            ToolTip.tip "Rotate Left 90°"
                        ]
                        Button.create [
                            Button.content "↷"
                            Button.width 40
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarRotateRight)
                            ToolTip.tip "Rotate Right 90°"
                        ]
                        Button.create [
                            Button.content "⇆"
                            Button.width 40
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarFlipHorizontal)
                            ToolTip.tip "Flip Horizontal"
                        ]
                        Button.create [
                            Button.content "⟺"
                            Button.width 40
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarFlipVertical)
                            ToolTip.tip "Flip Vertical"
                        ]
                        
                        // Separator
                        Border.create [
                            Border.width 1
                            Border.height 20
                            Border.margin (4, 5)
                            Border.background "#CCCCCC"
                        ]
                        
                        // View group
                        Button.create [
                            Button.content "⊙"
                            Button.width 40
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarCenter)
                            ToolTip.tip "Center Image"
                        ]
                        Button.create [
                            Button.content "⌂"
                            Button.width 40
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarRestore)
                            ToolTip.tip "Restore Original View"
                        ]
                        Button.create [
                            Button.content "⛶"
                            Button.width 40
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarFullscreen)
                            ToolTip.tip "Fullscreen"
                        ]
                        
                        // Separator
                        Border.create [
                            Border.width 1
                            Border.height 20
                            Border.margin (4, 5)
                            Border.background "#CCCCCC"
                        ]
                        
                        // Slideshow
                        Button.create [
                            Button.content "▶▶"
                            Button.width 50
                            Button.height 30
                            Button.margin (2, 0)
                            Button.onClick (fun _ -> dispatch ToolbarSlideshow)
                            ToolTip.tip "Start Slideshow"
                        ]
                    ]
                ]

                // Main content area (image display)
                match model.CurrentBitmap with
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
                    ]
            ]
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
            )
        ]

type MainWindow(argv: string[]) as this =
    inherit HostWindow()

    do
        System.Console.WriteLine("ELMISH MAINWINDOW CONSTRUCTOR CALLED!!!")
        System.Console.WriteLine("CONSOLE: MainWindow constructor DO block started")
        
        // Load configuration first to get user settings
        let config = Configuration.loadConfiguration argv
        let folderPath = Configuration.getFolderPath argv
        Log.Information("Using folder path: {Folder}", folderPath)
        
        Log.Information("FamilySlide MainWindow initializing")
        base.Title <- "FamilySlide"
        base.Width <- float config.UserSettings.Window.Width
        base.Height <- float config.UserSettings.Window.Height
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
