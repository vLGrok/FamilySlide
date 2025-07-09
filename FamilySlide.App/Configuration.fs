namespace FamilySlide.App

open System
open System.IO
open System.Text.Json
open Serilog

type LoggingConfig = {
    MinimumLevel: string
}

type CacheConfig = {
    MaxFullImages: int
    MaxThumbnails: int
    // Memory management settings
    MaxMemoryMB: int
    LowMemoryThresholdMB: int
    AggressiveCleanupThresholdMB: int
    PreloadNeighborImages: bool
    // Background cleanup settings
    BackgroundCleanupIntervalMinutes: int
    IdleCleanupDelayMinutes: int
    EnablePeriodicCleanup: bool
}

type ImageConfig = {
    ThumbnailMaxSize: int
}

type ZoomConfig = {
    MinLevelPercent: int // Minimum zoom percentage (e.g., 10 = 10%)
    MaxLevelPercent: int // Maximum zoom percentage (e.g., 1000 = 1000%)
    ZoomStepPercent: int // Additive zoom step in percentage points (e.g., 20 = +20%)
}

type UIConfig = {
    ToolbarButtonWidth: int
    ToolbarButtonHeight: int
    ToolbarMargin: int
    ToolbarSeparatorHeight: int
    ToolbarSeparatorMargin: int
}

type WindowConfig = {
    DefaultWidth: int
    DefaultHeight: int
}

type AppConfig = {
    Logging: LoggingConfig
    Cache: CacheConfig
    Image: ImageConfig
    Zoom: ZoomConfig
    UI: UIConfig
    Window: WindowConfig
}

type ApplicationConfiguration = {
    AppConfig: AppConfig
    UserSettings: UserSettings
}

module Configuration =
    
    let private defaultAppConfig = {
        Logging = { MinimumLevel = "Information" }
        Cache = { 
            MaxFullImages = 3
            MaxThumbnails = 50
            MaxMemoryMB = 512
            LowMemoryThresholdMB = 256
            AggressiveCleanupThresholdMB = 128
            PreloadNeighborImages = true
            BackgroundCleanupIntervalMinutes = 10
            IdleCleanupDelayMinutes = 2
            EnablePeriodicCleanup = true
        }
        Image = { ThumbnailMaxSize = 256 }
        Zoom = { MinLevelPercent = 10; MaxLevelPercent = 1000; ZoomStepPercent = 20 }
        UI = { 
            ToolbarButtonWidth = 40
            ToolbarButtonHeight = 30
            ToolbarMargin = 2
            ToolbarSeparatorHeight = 20
            ToolbarSeparatorMargin = 4
        }
        Window = { DefaultWidth = 800; DefaultHeight = 600 }
    }

    let private ensureAppSettingsExists () =
        let appDirectory = System.AppDomain.CurrentDomain.BaseDirectory
        let appSettingsPath = Path.Combine(appDirectory, "appsettings.json")
        
        if not (File.Exists(appSettingsPath)) then
            Log.Information("Creating default appsettings.json file at: {Path}", appSettingsPath)
            let defaultAppSettings = """{
  "Logging": {
    "MinimumLevel": "Information"
  },
  "Cache": {
    "MaxFullImages": 3,
    "MaxThumbnails": 50,
    "MaxMemoryMB": 512,
    "LowMemoryThresholdMB": 256,
    "AggressiveCleanupThresholdMB": 128,
    "PreloadNeighborImages": true,
    "BackgroundCleanupIntervalMinutes": 10,
    "IdleCleanupDelayMinutes": 2,
    "EnablePeriodicCleanup": true
  },
  "Image": {
    "ThumbnailMaxSize": 256
  },
  "Zoom": {
    "MinLevelPercent": 10,
    "MaxLevelPercent": 1000,
    "ZoomStepPercent": 20
  },
  "UI": {
    "ToolbarButtonWidth": 40,
    "ToolbarButtonHeight": 30,
    "ToolbarMargin": 2,
    "ToolbarSeparatorHeight": 20,
    "ToolbarSeparatorMargin": 4
  },
  "Window": {
    "DefaultWidth": 800,
    "DefaultHeight": 600
  }
}"""
            try
                File.WriteAllText(appSettingsPath, defaultAppSettings)
                Log.Information("Created default appsettings.json at: {Path}", Path.GetFullPath(appSettingsPath))
            with
            | ex -> Log.Error(ex, "Failed to create default appsettings.json")
        else
            Log.Debug("appsettings.json exists at: {Path}", Path.GetFullPath(appSettingsPath))

    let private loadAppConfig (appSettingsPath: string) =
        try
            if File.Exists(appSettingsPath) then
                Log.Debug("Loading app config from: {Path}", appSettingsPath)
                let json = File.ReadAllText(appSettingsPath)
                let appConfig = JsonSerializer.Deserialize<AppConfig>(json)
                Log.Debug("App config loaded successfully")
                appConfig
            else
                Log.Information("App settings file not found, using defaults")
                defaultAppConfig
        with
        | ex ->
            Log.Warning(ex, "Failed to load app config from {Path}, using defaults", appSettingsPath)
            defaultAppConfig
    
    let loadConfiguration (argv: string[]) =
        Log.Debug("Loading application configuration")
        
        ensureAppSettingsExists ()
        
        let appDirectory = System.AppDomain.CurrentDomain.BaseDirectory
        let appSettingsPath = Path.Combine(appDirectory, "appsettings.json")
        
        let appConfig = loadAppConfig appSettingsPath
        let userSettings = UserSettings.loadUserSettings ()
        
        Log.Information("Configuration loaded - Log level: {LogLevel}", appConfig.Logging.MinimumLevel)
        Log.Debug("Cache settings: MaxFullImages={MaxFull}, MaxThumbnails={MaxThumbnails}, MaxMemory={MaxMemory}MB", 
            appConfig.Cache.MaxFullImages, appConfig.Cache.MaxThumbnails, appConfig.Cache.MaxMemoryMB)
        Log.Debug("Image settings: ThumbnailMaxSize={ThumbnailSize}", 
            appConfig.Image.ThumbnailMaxSize)
        Log.Debug("Zoom settings: MinLevelPercent={MinLevel}%, MaxLevelPercent={MaxLevel}%", 
            appConfig.Zoom.MinLevelPercent, appConfig.Zoom.MaxLevelPercent)
        Log.Debug("UI settings: ButtonSize={Width}x{Height}, Margin={Margin}", 
            appConfig.UI.ToolbarButtonWidth, appConfig.UI.ToolbarButtonHeight, appConfig.UI.ToolbarMargin)
        Log.Debug("Window settings: DefaultSize={Width}x{Height}", 
            appConfig.Window.DefaultWidth, appConfig.Window.DefaultHeight)
        Log.Debug("User window settings: {Width}x{Height}, position: {X},{Y}, state: {State}, first run: {FirstRun}",
            userSettings.Window.Width, userSettings.Window.Height, 
            userSettings.Window.X, userSettings.Window.Y, 
            userSettings.Window.State, userSettings.Window.IsFirstRun)
        
        {
            AppConfig = appConfig
            UserSettings = userSettings
        }

    let getFolderPath (argv: string[]) =
        match argv |> Array.tryFind (fun arg -> not (arg.StartsWith("-"))) with
        | Some cmdLineFolder when Directory.Exists(cmdLineFolder) -> 
            Log.Information("Using folder path from command line: {FolderPath}", cmdLineFolder)
            cmdLineFolder
        | _ ->
            let currentDir = Environment.CurrentDirectory
            Log.Information("Using current directory: {FolderPath}", currentDir)
            currentDir
