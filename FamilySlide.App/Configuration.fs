namespace FamilySlide.App

open System
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
    Window: AppWindowSettings
}

type ApplicationConfiguration = {
    AppSettings: AppSettings
    UserSettings: UserSettings
}

module Configuration =
    
    let private ensureAppSettingsExists () =
        let appDirectory = System.AppDomain.CurrentDomain.BaseDirectory
        let appSettingsPath = Path.Combine(appDirectory, "appsettings.json")
        
        if not (File.Exists(appSettingsPath)) then
            Log.Information("Creating default appsettings.json file at: {Path}", appSettingsPath)
            let defaultAppSettings = """{
  "Logging": {
    "MinimumLevel": "Information"
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
        let appDirectory = System.AppDomain.CurrentDomain.BaseDirectory
        let appSettingsPath = Path.Combine(appDirectory, "appsettings.json")
        
        let config : IConfiguration = 
            ConfigurationBuilder()
                .AddJsonFile(appSettingsPath, optional = true)
                .AddCommandLine(argv)
                .Build()
        
        {
            LogLevel = 
                match config.["Logging:MinimumLevel"] with
                | null -> "Information"
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
        
        ensureAppSettingsExists ()
        
        let appSettings = loadAppSettings argv
        let userSettings = UserSettings.loadUserSettings ()
        
        Log.Information("Configuration loaded - Log level: {LogLevel}", appSettings.LogLevel)
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

    let getFolderPath (argv: string[]) =
        match argv |> Array.tryFind (fun arg -> not (arg.StartsWith("-"))) with
        | Some cmdLineFolder when Directory.Exists(cmdLineFolder) -> 
            Log.Information("Using folder path from command line: {FolderPath}", cmdLineFolder)
            cmdLineFolder
        | _ ->
            let currentDir = Environment.CurrentDirectory
            Log.Information("Using current directory: {FolderPath}", currentDir)
            currentDir
