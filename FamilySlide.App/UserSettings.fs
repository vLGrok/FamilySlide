namespace FamilySlide.App

open System
open System.IO
open System.Text.Json
open Serilog

type UserWindowSettings = {
    X: int option
    Y: int option
    Width: int
    Height: int
    State: string  // "Normal", "Maximized", "Minimized"
    IsFirstRun: bool
}

type UserSettings = {
    Window: UserWindowSettings
    LastFolderPath: string option
}

module UserSettings =
    
    let getDefaultPicturesDirectory () =
        try
            // Try to get the Pictures special folder
            let picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            if Directory.Exists(picturesPath) then
                picturesPath
            else
                // Fallback to user's home directory
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        with
        | _ ->
            // Ultimate fallback to current directory
            Environment.CurrentDirectory
    
    let private getAppDataPath () =
        let appName = "FamilySlide"
        match Environment.OSVersion.Platform with
        | PlatformID.Win32NT -> 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName)
        | PlatformID.Unix when Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") <> null ->
            Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"), appName)
        | PlatformID.Unix ->
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", appName)
        | PlatformID.MacOSX ->
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", appName)
        | _ ->
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName)
    
    let private getUserSettingsPath () =
        let appDataPath = getAppDataPath()
        Path.Combine(appDataPath, "user-settings.json")
    
    let private createDefaultSettings () = {
        Window = {
            X = None
            Y = None
            Width = 800
            Height = 600
            State = "Normal"
            IsFirstRun = true
        }
        LastFolderPath = None
    }
    
    let private jsonOptions = JsonSerializerOptions(WriteIndented = true)
    
    let loadUserSettings () =
        let settingsPath = getUserSettingsPath()
        
        try
            if File.Exists(settingsPath) then
                Log.Debug("Loading user settings from: {Path}", settingsPath)
                let json = File.ReadAllText(settingsPath)
                let settings = JsonSerializer.Deserialize<UserSettings>(json, jsonOptions)
                Log.Debug("User settings loaded successfully")
                settings
            else
                Log.Information("User settings file not found, creating default settings at: {Path}", settingsPath)
                let defaultSettings = createDefaultSettings()
                
                // Create the default user settings file
                let directory = Path.GetDirectoryName(settingsPath)
                if not (Directory.Exists(directory)) then
                    Directory.CreateDirectory(directory) |> ignore
                    Log.Debug("Created user settings directory: {Directory}", directory)
                
                let json = JsonSerializer.Serialize(defaultSettings, jsonOptions)
                File.WriteAllText(settingsPath, json)
                Log.Information("Created default user settings file")
                
                defaultSettings
        with
        | ex ->
            Log.Warning(ex, "Failed to load user settings from {Path}, using defaults", settingsPath)
            createDefaultSettings()
    
    let saveUserSettings (settings: UserSettings) =
        let settingsPath = getUserSettingsPath()
        let directory = Path.GetDirectoryName(settingsPath)
        
        try
            if not (Directory.Exists(directory)) then
                Directory.CreateDirectory(directory) |> ignore
                Log.Debug("Created user settings directory: {Directory}", directory)
            
            let json = JsonSerializer.Serialize(settings, jsonOptions)
            File.WriteAllText(settingsPath, json)
            Log.Debug("User settings saved to: {Path}", settingsPath)
        with
        | ex ->
            Log.Error(ex, "Failed to save user settings to {Path}", settingsPath)

    let updateLastFolderPath (folderPath: string) =
        try
            let currentSettings = loadUserSettings()
            let updatedSettings = { currentSettings with LastFolderPath = Some folderPath }
            saveUserSettings updatedSettings
            Log.Debug("Updated last folder path to: {FolderPath}", folderPath)
        with
        | ex ->
            Log.Warning(ex, "Failed to update last folder path")

    let getLastFolderPathOrDefault () =
        try
            let settings = loadUserSettings()
            match settings.LastFolderPath with
            | Some path when Directory.Exists(path) -> path
            | _ -> 
                let defaultPath = getDefaultPicturesDirectory()
                Log.Information("Using default Pictures directory: {Path}", defaultPath)
                defaultPath
        with
        | ex ->
            Log.Warning(ex, "Failed to get last folder path, using current directory")
            Environment.CurrentDirectory
