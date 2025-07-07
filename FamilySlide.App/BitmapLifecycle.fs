namespace FamilySlide.App

open System
open System.Collections.Concurrent
open Avalonia.Media.Imaging
open Serilog

/// Module for managing bitmap lifecycle and disposal tracking
module BitmapLifecycle =
    
    /// Active bitmap tracking for debugging memory leaks
    let private activeBitmaps = ConcurrentDictionary<int, string * DateTime>()
    
    /// Counter for total bitmaps created
    let mutable totalBitmapsCreated = 0L
    
    /// Counter for total bitmaps disposed
    let mutable totalBitmapsDisposed = 0L
    
    /// Statistics about bitmap lifecycle
    type BitmapStats = {
        Active: int
        TotalCreated: int64
        TotalDisposed: int64
        OldestActive: DateTime option
    }
    
    /// Track a newly created bitmap for debugging
    let trackBitmap (bitmap: Bitmap) (context: string) =
        if not (isNull bitmap) then
            let id = bitmap.GetHashCode()
            let timestamp = DateTime.Now
            activeBitmaps.TryAdd(id, (context, timestamp)) |> ignore
            System.Threading.Interlocked.Increment(&totalBitmapsCreated) |> ignore
            Log.Debug("Bitmap created: {Context} (ID: {Id}, Active: {Count})", 
                context, id, activeBitmaps.Count)
    
    /// Safely dispose a bitmap and track the disposal
    let disposeBitmap (bitmap: Bitmap) (context: string) =
        if not (isNull bitmap) then
            let id = bitmap.GetHashCode()
            try
                bitmap.Dispose()
                activeBitmaps.TryRemove(id) |> ignore
                System.Threading.Interlocked.Increment(&totalBitmapsDisposed) |> ignore
                Log.Debug("Bitmap disposed: {Context} (ID: {Id}, Active: {Count})", 
                    context, id, activeBitmaps.Count)
            with
            | ex -> 
                Log.Warning(ex, "Error disposing bitmap: {Context} (ID: {Id})", context, id)
    
    /// Dispose multiple bitmaps safely
    let disposeBitmaps (bitmaps: (Bitmap * string) list) =
        bitmaps
        |> List.iter (fun (bitmap, context) -> disposeBitmap bitmap context)
    
    /// Get current bitmap statistics
    let getStats () =
        let oldest = 
            if activeBitmaps.IsEmpty then
                None
            else
                activeBitmaps.Values
                |> Seq.map snd
                |> Seq.min
                |> Some
        
        {
            Active = activeBitmaps.Count
            TotalCreated = totalBitmapsCreated
            TotalDisposed = totalBitmapsDisposed
            OldestActive = oldest
        }
    
    /// Log current bitmap statistics
    let logStats (context: string) =
        let stats = getStats()
        Log.Information("Bitmap stats ({Context}): {Active} active, {Created} created, {Disposed} disposed", 
            context, stats.Active, stats.TotalCreated, stats.TotalDisposed)
        
        if stats.Active > 20 then
            Log.Warning("High number of active bitmaps detected: {Count}", stats.Active)
        
        stats
    
    /// Get list of long-running bitmaps (for debugging leaks)
    let getLongRunningBitmaps (olderThan: TimeSpan) =
        let threshold = DateTime.Now - olderThan
        activeBitmaps.Values
        |> Seq.filter (fun (_, timestamp) -> timestamp < threshold)
        |> Seq.map fst
        |> List.ofSeq
    
    /// Clear all tracking (should only be used for testing)
    let clearTracking () =
        activeBitmaps.Clear()
        totalBitmapsCreated <- 0L
        totalBitmapsDisposed <- 0L
        Log.Warning("Bitmap tracking cleared - this should only happen in tests")

/// Wrapper type for tracking bitmap disposal
type TrackedBitmap = {
    Bitmap: Bitmap
    Context: string
    CreatedAt: DateTime
} with
    interface IDisposable with
        member this.Dispose() =
            BitmapLifecycle.disposeBitmap this.Bitmap this.Context

module TrackedBitmap =
    /// Create a new tracked bitmap
    let create (bitmap: Bitmap) (context: string) =
        if isNull bitmap then
            invalidArg "bitmap" "Bitmap cannot be null"
        
        BitmapLifecycle.trackBitmap bitmap context
        {
            Bitmap = bitmap
            Context = context
            CreatedAt = DateTime.Now
        }
    
    /// Get the underlying bitmap
    let getBitmap (tracked: TrackedBitmap) = tracked.Bitmap
    
    /// Safely dispose the tracked bitmap
    let dispose (tracked: TrackedBitmap) =
        (tracked :> IDisposable).Dispose()
