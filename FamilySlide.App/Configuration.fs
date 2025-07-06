namespace FamilySlide.App

open System.IO
open Microsoft.Extensions.Configuration
open Serilog

type AppWindowSettings = {
    DefaultWidth: int
    DefaultHeight: int
    DefaultState: string
}

type AppSettings = {
    LogLevel: string
    FolderPath: string
    Window: AppWindowSettings
}

type ApplicationConfiguration = {
    AppSettings: AppSettings
    UserSettings: UserSettings
}

module Configuration =
    
    let private ensureAppSettingsExists () =
        let appSettingsPath = "appsettings.json"
        if not (File.Exists(appSettingsPath)) then
            Log.Information("Creating default appsettings.json file")
            let defaultAppSettings = """{
  "Logging": {
    "MinimumLevel": "Information"
  },
  "Application": {
    "FolderPath": "/Users/rkerr/Pictures/iPadPhotos"
  },
  "Window": {
    "DefaultWidth": 800,
    "DefaultHeight": 600,
    "DefaultState": "Normal"
  }
}"""
            try
                File.WriteAllText(appSettingsPath, defaultAppSettings)
                Log.Information("Created default appsettings.json at: {Path}", Path.GetFullPath(appSettingsPath))
            with
            | ex -> Log.Error(ex, "Failed to create default appsettings.json")
        else
            Log.Debug("appsettings.json exists at: {Path}", Path.GetFullPath(appSettingsPath))
    
    let loadAppSettings (argv: string[]) =
        let config : IConfiguration = 
            ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional = true)
                .AddCommandLine(argv)
                .Build()
        
        {
            LogLevel = 
                match config.["Logging:MinimumLevel"] with
                | null -> "Information"
                | value -> value
            FolderPath =
                match config.["Application:FolderPath"] with
                | null -> "/Users/rkerr/Pictures/iPadPhotos" // fallback
                | value -> value
            Window = {
                DefaultWidth = 
                    match config.["Window:DefaultWidth"] with
                    | null -> 800
                    | value -> int value
                DefaultHeight = 
                    match config.["Window:DefaultHeight"] with
                    | null -> 600
                    | value -> int value
                DefaultState = 
                    match config.["Window:DefaultState"] with
                    | null -> "Normal"
                    | value -> value
            }
        }
    
    let loadConfiguration (argv: string[]) =
        Log.Debug("Loading application configuration")
        
        // Ensure settings files exist before loading
        ensureAppSettingsExists ()
        
        let appSettings = loadAppSettings argv
        let userSettings = UserSettings.loadUserSettings ()
        
        Log.Information("Configuration loaded - App folder: {FolderPath}, Log level: {LogLevel}", 
            appSettings.FolderPath, appSettings.LogLevel)
        Log.Debug("Window defaults: {Width}x{Height}, state: {State}", 
            appSettings.Window.DefaultWidth, appSettings.Window.DefaultHeight, appSettings.Window.DefaultState)
        Log.Debug("User window settings: {Width}x{Height}, position: {X},{Y}, state: {State}, first run: {FirstRun}",
            userSettings.Window.Width, userSettings.Window.Height, 
            userSettings.Window.X, userSettings.Window.Y, 
            userSettings.Window.State, userSettings.Window.IsFirstRun)
        
        {
            AppSettings = appSettings
            UserSettings = userSettings
        }
