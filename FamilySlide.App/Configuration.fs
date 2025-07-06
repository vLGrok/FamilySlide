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
        // Look for appsettings.json in the same directory as the executing assembly
        let appDirectory = System.AppDomain.CurrentDomain.BaseDirectory
        let appSettingsPath = Path.Combine(appDirectory, "appsettings.json")
        
        if not (File.Exists(appSettingsPath)) then
            Log.Information("Creating default appsettings.json file at: {Path}", appSettingsPath)
            let defaultAppSettings = """{
  "Logging": {
    "MinimumLevel": "Information"
  },
  "Application": {
    "FolderPath": ""
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
        // Look for appsettings.json in the same directory as the executing assembly
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
            FolderPath =
                match config.["Application:FolderPath"] with
                | null -> "" // Empty string as fallback, will be determined later
                | value when System.String.IsNullOrWhiteSpace(value) -> ""
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
        
        // Determine the folder path: use command line arg, or last folder path, or app default
        let folderPath = 
            // First check command line arguments for folder path
            match argv |> Array.tryFind (fun arg -> not (arg.StartsWith("-"))) with
            | Some cmdLineFolder when Directory.Exists(cmdLineFolder) -> 
                Log.Information("Using folder path from command line: {FolderPath}", cmdLineFolder)
                cmdLineFolder
            | _ ->
                // Fall back to last folder path or default
                let lastOrDefault = UserSettings.getLastFolderPathOrDefault()
                Log.Information("Using last/default folder path: {FolderPath}", lastOrDefault)
                lastOrDefault
        
        // Update app settings with the determined folder path
        let finalAppSettings = { appSettings with FolderPath = folderPath }
        
        Log.Information("Configuration loaded - App folder: {FolderPath}, Log level: {LogLevel}", 
            finalAppSettings.FolderPath, finalAppSettings.LogLevel)
        Log.Debug("Window defaults: {Width}x{Height}, state: {State}", 
            finalAppSettings.Window.DefaultWidth, finalAppSettings.Window.DefaultHeight, finalAppSettings.Window.DefaultState)
        Log.Debug("User window settings: {Width}x{Height}, position: {X},{Y}, state: {State}, first run: {FirstRun}",
            userSettings.Window.Width, userSettings.Window.Height, 
            userSettings.Window.X, userSettings.Window.Y, 
            userSettings.Window.State, userSettings.Window.IsFirstRun)
        
        {
            AppSettings = finalAppSettings
            UserSettings = userSettings
        }
