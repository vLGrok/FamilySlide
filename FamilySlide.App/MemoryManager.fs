namespace FamilySlide.App

open System
open System.Diagnostics
open System.Runtime
open Serilog

/// Module for monitoring memory usage and triggering cleanup actions
module MemoryManager =
    
    /// Memory usage statistics
    type MemoryStats = {
        /// Total memory allocated by the current process (MB)
        ProcessMemoryMB: int64
        /// Working set memory (MB) 
        WorkingSetMB: int64
        /// GC heap memory (MB)
        GCMemoryMB: int64
        /// Generation 2 collections since startup
        Gen2Collections: int
        /// Large object heap size (MB)
        LOHSizeMB: int64
        /// Timestamp when stats were collected
        Timestamp: DateTime
    }
    
    /// Memory pressure levels
    type MemoryPressure =
        | Low
        | Normal  
        | High
        | Critical
    
    /// Get current memory statistics
    let getMemoryStats () =
        let currentProcess = Process.GetCurrentProcess()
        let gcInfo = GC.GetTotalMemory(false)
        
        {
            ProcessMemoryMB = currentProcess.WorkingSet64 / (1024L * 1024L)
            WorkingSetMB = currentProcess.WorkingSet64 / (1024L * 1024L)
            GCMemoryMB = gcInfo / (1024L * 1024L)
            Gen2Collections = GC.CollectionCount(2)
            LOHSizeMB = GCSettings.LargeObjectHeapCompactionMode |> fun _ -> gcInfo / (1024L * 1024L) // Approximation
            Timestamp = DateTime.Now
        }
    
    /// Assess memory pressure based on configuration
    let assessMemoryPressure (cacheConfig: CacheConfig) (stats: MemoryStats) =
        let memoryMB = stats.ProcessMemoryMB
        
        if memoryMB >= int64 cacheConfig.AggressiveCleanupThresholdMB then
            Critical
        elif memoryMB >= int64 cacheConfig.LowMemoryThresholdMB then
            High
        elif memoryMB >= (int64 cacheConfig.MaxMemoryMB * 3L / 4L) then
            Normal
        else
            Low
    
    /// Force garbage collection and log results
    let forceGarbageCollection (context: string) =
        let beforeStats = getMemoryStats()
        
        Log.Debug("Forcing GC: {Context} - Before: {BeforeMemory}MB", context, beforeStats.GCMemoryMB)
        
        // Force full GC
        GC.Collect()
        GC.WaitForPendingFinalizers()
        GC.Collect()
        
        let afterStats = getMemoryStats()
        let freedMB = beforeStats.GCMemoryMB - afterStats.GCMemoryMB
        
        Log.Information("GC completed: {Context} - Freed: {FreedMemory}MB (Before: {Before}MB, After: {After}MB)", 
            context, freedMB, beforeStats.GCMemoryMB, afterStats.GCMemoryMB)
        
        afterStats
    
    /// Log detailed memory statistics
    let logMemoryStats (context: string) (stats: MemoryStats) =
        Log.Information("Memory stats ({Context}): Process={ProcessMB}MB, GC={GCMB}MB, Gen2={Gen2}, WorkingSet={WorkingSetMB}MB", 
            context, stats.ProcessMemoryMB, stats.GCMemoryMB, stats.Gen2Collections, stats.WorkingSetMB)
    
    /// Memory cleanup actions that can be triggered
    type CleanupAction =
        | ClearOldThumbnails
        | ClearAllFullImages
        | ClearAllThumbnails
        | EmergencyFullClear
        | ForceGC
    
    /// Get recommended cleanup actions based on memory pressure
    let getRecommendedCleanupActions (pressure: MemoryPressure) =
        match pressure with
        | Low -> []
        | Normal -> [ForceGC]
        | High -> [ClearOldThumbnails; ForceGC]
        | Critical -> [ClearAllFullImages; ClearOldThumbnails; ForceGC]
    
    /// Memory monitoring state
    type MonitoringState = {
        LastStats: MemoryStats option
        ConsecutiveHighPressure: int
        LastCleanupTime: DateTime option
        CleanupCooldownMinutes: int
    }
    
    /// Create initial monitoring state
    let createMonitoringState () = {
        LastStats = None
        ConsecutiveHighPressure = 0
        LastCleanupTime = None
        CleanupCooldownMinutes = 5 // Don't cleanup more than once every 5 minutes
    }
    
    /// Check if cleanup is needed and not in cooldown
    let shouldTriggerCleanup (state: MonitoringState) (pressure: MemoryPressure) =
        let now = DateTime.Now
        let inCooldown = 
            match state.LastCleanupTime with
            | Some lastTime -> (now - lastTime).TotalMinutes < float state.CleanupCooldownMinutes
            | None -> false
        
        let needsCleanup = 
            match pressure with
            | Critical -> true // Always cleanup on critical
            | High -> state.ConsecutiveHighPressure >= 2 // Cleanup after 2 consecutive high pressure readings
            | _ -> false
        
        needsCleanup && not inCooldown
    
    /// Update monitoring state with new pressure reading
    let updateMonitoringState (state: MonitoringState) (stats: MemoryStats) (pressure: MemoryPressure) (cleanupTriggered: bool) =
        let newConsecutiveHigh = 
            match pressure with
            | High | Critical -> state.ConsecutiveHighPressure + 1
            | _ -> 0
        
        let newLastCleanupTime = 
            if cleanupTriggered then Some stats.Timestamp
            else state.LastCleanupTime
        
        {
            LastStats = Some stats
            ConsecutiveHighPressure = newConsecutiveHigh
            LastCleanupTime = newLastCleanupTime
            CleanupCooldownMinutes = state.CleanupCooldownMinutes
        }
