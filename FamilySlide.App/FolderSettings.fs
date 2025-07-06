namespace FamilySlide.App

open System.IO
open System.Text.Json
open System
open Serilog

type ImageViewSettings = {
    Zoom: float
    OffsetX: float
    OffsetY: float
}

type FolderSettings = {
    SaveImageSettings: bool
    Images: Map<string, ImageViewSettings>
}

module FolderSettings =
    
    // Track folders where we've already warned about write access to avoid repeated messages
    let mutable private warnedReadOnlyFolders = Set.empty<string>
    
    let private defaultImageSettings = {
        Zoom = 1.0
        OffsetX = 0.0
        OffsetY = 0.0
    }
    
    let private defaultFolderSettings = {
        SaveImageSettings = false
        Images = Map.empty
    }
    
    let private getFolderSettingsPath (folderPath: string) =
        Path.Combine(folderPath, "foldersettings.json")
    
    let private createDefaultFolderSettings (folderPath: string) (imageFiles: string list) =
        Log.Information("Creating default foldersettings.json for folder: {Folder}", folderPath)
        
        let imageMap = 
            imageFiles
            |> List.map (fun fullPath -> Path.GetFileName(fullPath), defaultImageSettings)
            |> Map.ofList
        
        let settings = { defaultFolderSettings with Images = imageMap }
        
        let settingsPath = getFolderSettingsPath folderPath
        try
            let json = JsonSerializer.Serialize(settings, JsonSerializerOptions(WriteIndented = true))
            File.WriteAllText(settingsPath, json)
            Log.Information("Created foldersettings.json with {Count} image entries at: {Path}", 
                imageFiles.Length, settingsPath)
            settings
        with
        | :? UnauthorizedAccessException as ex -> 
            // Only warn once per folder about read-only access
            if not (warnedReadOnlyFolders.Contains folderPath) then
                warnedReadOnlyFolders <- warnedReadOnlyFolders.Add folderPath
                Log.Warning("Cannot create foldersettings.json in read-only folder: {Path} - continuing with default settings", folderPath)
            settings
        | ex -> 
            Log.Error(ex, "Failed to create foldersettings.json at: {Path}", settingsPath)
            settings
    
    let private loadExistingFolderSettings (folderPath: string) =
        let settingsPath = getFolderSettingsPath folderPath
        try
            let json = File.ReadAllText(settingsPath)
            let settings = JsonSerializer.Deserialize<FolderSettings>(json)
            Log.Debug("Loaded existing foldersettings.json from: {Path}", settingsPath)
            Some settings
        with
        | ex ->
            Log.Warning(ex, "Failed to load foldersettings.json from: {Path}", settingsPath)
            None
    
    let private syncSettingsWithFiles (settings: FolderSettings) (imageFiles: string list) =
        let currentFileNames = 
            imageFiles 
            |> List.map Path.GetFileName 
            |> Set.ofList
        
        let settingsFileNames = 
            settings.Images 
            |> Map.toSeq 
            |> Seq.map fst 
            |> Set.ofSeq
        
        // Find files to add (in folder but not in settings)
        let filesToAdd = Set.difference currentFileNames settingsFileNames
        
        // Find entries to remove (in settings but not in folder)
        let entriesToRemove = Set.difference settingsFileNames currentFileNames
        
        Log.Debug("Syncing folder settings: {AddCount} files to add, {RemoveCount} entries to remove", 
            filesToAdd.Count, entriesToRemove.Count)
        
        // Add missing files with default settings
        let updatedImages = 
            filesToAdd
            |> Set.fold (fun acc fileName -> 
                Log.Debug("Adding default settings for new file: {FileName}", fileName)
                Map.add fileName defaultImageSettings acc
            ) settings.Images
        
        // Remove entries for files that no longer exist
        let finalImages = 
            entriesToRemove
            |> Set.fold (fun acc fileName -> 
                Log.Debug("Removing settings for deleted file: {FileName}", fileName)
                Map.remove fileName acc
            ) updatedImages
        
        { settings with Images = finalImages }
    
    let private saveFolderSettings (folderPath: string) (settings: FolderSettings) =
        let settingsPath = getFolderSettingsPath folderPath
        try
            let json = JsonSerializer.Serialize(settings, JsonSerializerOptions(WriteIndented = true))
            File.WriteAllText(settingsPath, json)
            Log.Debug("Saved foldersettings.json to: {Path}", settingsPath)
        with
        | :? UnauthorizedAccessException as ex ->
            // Only warn once per folder about read-only access
            if not (warnedReadOnlyFolders.Contains folderPath) then
                warnedReadOnlyFolders <- warnedReadOnlyFolders.Add folderPath
                Log.Warning("Cannot save foldersettings.json in read-only folder: {Path} - changes will not be persisted", folderPath)
        | ex ->
            Log.Error(ex, "Failed to save foldersettings.json to: {Path}", settingsPath)
    
    let loadOrCreateFolderSettings (folderPath: string) (imageFiles: string list) =
        Log.Information("Loading or creating folder settings for: {Folder}", folderPath)
        
        // Only create settings if there are actually images
        if imageFiles.IsEmpty then
            Log.Debug("No images found in folder, not creating foldersettings.json")
            defaultFolderSettings
        else
            let settingsPath = getFolderSettingsPath folderPath
            
            if File.Exists(settingsPath) then
                // Load existing settings and sync with current files
                match loadExistingFolderSettings folderPath with
                | Some existingSettings ->
                    let syncedSettings = syncSettingsWithFiles existingSettings imageFiles
                    
                    // Save the synced settings if changes were made
                    if syncedSettings <> existingSettings then
                        Log.Information("Folder settings synced - updating foldersettings.json")
                        saveFolderSettings folderPath syncedSettings
                    else
                        Log.Debug("Folder settings already in sync")
                    
                    syncedSettings
                | None ->
                    // Failed to load, create new
                    createDefaultFolderSettings folderPath imageFiles
            else
                // Create new settings file
                createDefaultFolderSettings folderPath imageFiles
    
    let getImageSettings (settings: FolderSettings) (fileName: string) =
        settings.Images
        |> Map.tryFind fileName
        |> Option.defaultValue defaultImageSettings
    
    let updateImageSettings (folderPath: string) (settings: FolderSettings) (fileName: string) (imageSettings: ImageViewSettings) =
        if settings.SaveImageSettings then
            let updatedSettings = { 
                settings with 
                    Images = Map.add fileName imageSettings settings.Images 
            }
            saveFolderSettings folderPath updatedSettings
            Log.Debug("Updated and saved settings for image: {FileName}", fileName)
            updatedSettings
        else
            Log.Debug("Image settings saving is disabled, not persisting changes for: {FileName}", fileName)
            settings
