namespace FamilySlide.App

open System
open System.Threading
open System.Threading.Tasks
open Serilog

/// Background service for periodic cache cleanup and memory management
module BackgroundCleanupService =
    
    /// State of the background cleanup service
    type CleanupServiceState = {
        IsRunning: bool
        LastCleanupTime: DateTime option
        LastUserActivity: DateTime
        CleanupIntervalMs: int
        IdleDelayMs: int
        CancellationTokenSource: CancellationTokenSource option
    }
    
    /// Activity that affects cleanup timing
    type UserActivity =
        | ImageNavigation
        | ImageLoading
        | UserInput
        | ApplicationStart
    
    /// Create initial cleanup service state
    let createState (cacheConfig: CacheConfig) = {
        IsRunning = false
        LastCleanupTime = None
        LastUserActivity = DateTime.Now
        CleanupIntervalMs = cacheConfig.BackgroundCleanupIntervalMinutes * 60 * 1000
        IdleDelayMs = cacheConfig.IdleCleanupDelayMinutes * 60 * 1000
        CancellationTokenSource = None
    }
    
    /// Update last user activity time
    let recordUserActivity (activity: UserActivity) (state: CleanupServiceState) =
        Log.Debug("User activity recorded: {Activity}", activity)
        { state with LastUserActivity = DateTime.Now }
    
    /// Check if enough time has passed since last cleanup
    let shouldPerformCleanup (state: CleanupServiceState) =
        let now = DateTime.Now
        let timeSinceLastCleanup = 
            match state.LastCleanupTime with
            | Some lastTime -> (now - lastTime).TotalMilliseconds
            | None -> Double.MaxValue
        
        let timeSinceActivity = (now - state.LastUserActivity).TotalMilliseconds
        
        // Perform cleanup if:
        // 1. Enough time has passed since last cleanup AND
        // 2. User has been idle long enough to not interfere
        timeSinceLastCleanup >= float state.CleanupIntervalMs &&
        timeSinceActivity >= float state.IdleDelayMs
    
    /// Perform background cleanup operations
    let performBackgroundCleanup (getCacheState: unit -> ImageCache.CacheState) (updateCacheState: ImageCache.CacheState -> unit) =
        try
            Log.Debug("Starting background cleanup")
            let currentCache = getCacheState()
            
            // Get memory info before cleanup
            let memoryInfoBefore = ImageCache.getMemoryInfo currentCache
            Log.Information("Background cleanup - Memory before: {ProcessMB}MB, Pressure: {Pressure}", 
                memoryInfoBefore.Stats.ProcessMemoryMB, memoryInfoBefore.Pressure)
            
            // Perform gentle cleanup (less aggressive than manual cleanup)
            let cleanedCache = 
                match memoryInfoBefore.Pressure with
                | MemoryManager.Critical ->
                    // Critical pressure - aggressive cleanup
                    ImageCache.manualMemoryCleanup currentCache
                | MemoryManager.High ->
                    // High pressure - clear old full images
                    ImageCache.clearFullImages currentCache
                | MemoryManager.Normal ->
                    // Normal pressure - just force GC
                    MemoryManager.forceGarbageCollection "Background Cleanup" |> ignore
                    currentCache
                | MemoryManager.Low ->
                    // Low pressure - minimal action
                    currentCache
            
            // Update the cache
            updateCacheState cleanedCache
            
            // Log results
            let memoryInfoAfter = ImageCache.getMemoryInfo cleanedCache
            let memoryFreed = memoryInfoBefore.Stats.ProcessMemoryMB - memoryInfoAfter.Stats.ProcessMemoryMB
            
            Log.Information("Background cleanup completed - Memory freed: {FreedMB}MB, After: {AfterMB}MB", 
                memoryFreed, memoryInfoAfter.Stats.ProcessMemoryMB)
            
            true // Cleanup successful
        with
        | ex ->
            Log.Error(ex, "Error during background cleanup")
            false // Cleanup failed
    
    /// Background cleanup task that runs periodically
    let private cleanupTask (state: CleanupServiceState) (getCacheState: unit -> ImageCache.CacheState) (updateCacheState: ImageCache.CacheState -> unit) (updateState: CleanupServiceState -> unit) (cancellationToken: CancellationToken) =
        task {
            try
                Log.Information("Background cleanup service started")
                
                while not cancellationToken.IsCancellationRequested do
                    try
                        // Wait for the check interval (shorter than cleanup interval)
                        let checkIntervalMs = min 30000 (state.CleanupIntervalMs / 4) // Check every 30 seconds or 1/4 of cleanup interval
                        do! Task.Delay(checkIntervalMs, cancellationToken)
                        
                        // Check if cleanup should be performed
                        if shouldPerformCleanup state then
                            Log.Debug("Background cleanup conditions met, performing cleanup")
                            let success = performBackgroundCleanup getCacheState updateCacheState
                            
                            if success then
                                let newState = { state with LastCleanupTime = Some DateTime.Now }
                                updateState newState
                            
                        else
                            let timeSinceActivity = (DateTime.Now - state.LastUserActivity).TotalMinutes
                            let timeSinceCleanup = 
                                match state.LastCleanupTime with
                                | Some lastTime -> (DateTime.Now - lastTime).TotalMinutes
                                | None -> -1.0
                            
                            Log.Debug("Background cleanup skipped - Activity: {ActivityMin}min ago, Cleanup: {CleanupMin}min ago", 
                                timeSinceActivity, timeSinceCleanup)
                    with
                    | :? OperationCanceledException ->
                        Log.Information("Background cleanup service cancelled")
                        return ()
                    | ex ->
                        Log.Error(ex, "Error in background cleanup loop")
                        // Continue running despite errors
                        do! Task.Delay(5000, cancellationToken) // Wait 5 seconds before retrying
                
                Log.Information("Background cleanup service stopped")
            with
            | :? OperationCanceledException ->
                Log.Information("Background cleanup service cancelled")
            | ex ->
                Log.Error(ex, "Fatal error in background cleanup service")
        }
    
    /// Start the background cleanup service
    let startService (cacheConfig: CacheConfig) (state: CleanupServiceState) (getCacheState: unit -> ImageCache.CacheState) (updateCacheState: ImageCache.CacheState -> unit) (updateState: CleanupServiceState -> unit) =
        if not cacheConfig.EnablePeriodicCleanup then
            Log.Information("Background cleanup is disabled in configuration")
            state
        elif state.IsRunning then
            Log.Warning("Background cleanup service is already running")
            state
        else
            try
                let cts = new CancellationTokenSource()
                let newState = { state with IsRunning = true; CancellationTokenSource = Some cts }
                
                // Start the background task
                let _ = Task.Run(System.Func<Task>(fun () -> cleanupTask newState getCacheState updateCacheState updateState cts.Token))
                
                Log.Information("Background cleanup service started - Interval: {IntervalMin}min, Idle delay: {IdleMin}min", 
                    cacheConfig.BackgroundCleanupIntervalMinutes, cacheConfig.IdleCleanupDelayMinutes)
                
                newState
            with
            | ex ->
                Log.Error(ex, "Failed to start background cleanup service")
                state
    
    /// Stop the background cleanup service
    let stopService (state: CleanupServiceState) =
        if not state.IsRunning then
            state
        else
            try
                match state.CancellationTokenSource with
                | Some cts ->
                    cts.Cancel()
                    cts.Dispose()
                | None -> ()
                
                Log.Information("Background cleanup service stopped")
                { state with IsRunning = false; CancellationTokenSource = None }
            with
            | ex ->
                Log.Error(ex, "Error stopping background cleanup service")
                { state with IsRunning = false; CancellationTokenSource = None }
    
    /// Get service status for debugging
    let getServiceStatus (state: CleanupServiceState) =
        let timeSinceActivity = 
            if state.LastUserActivity = DateTime.MinValue then -1.0
            else (DateTime.Now - state.LastUserActivity).TotalMinutes
        
        let timeSinceCleanup = 
            match state.LastCleanupTime with
            | Some lastTime -> (DateTime.Now - lastTime).TotalMinutes
            | None -> -1.0
        
        {| 
            IsRunning = state.IsRunning
            TimeSinceActivityMinutes = timeSinceActivity
            TimeSinceCleanupMinutes = timeSinceCleanup
            CleanupIntervalMinutes = float state.CleanupIntervalMs / 60000.0
            IdleDelayMinutes = float state.IdleDelayMs / 60000.0
        |}
