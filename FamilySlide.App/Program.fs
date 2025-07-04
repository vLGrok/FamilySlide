namespace FamilySlide.App

open System
open Avalonia
open Avalonia.ReactiveUI
open Serilog

module Program =

    [<CompiledName "BuildAvaloniaApp">]
    let buildAvaloniaApp () =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace(areas = Array.empty)
            .UseReactiveUI()

    [<EntryPoint; STAThread>]
    let main argv =
        Log.Logger <-
            LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(
                    "familyslide.log", 
                    rollingInterval = RollingInterval.Day,
                    flushToDiskInterval = System.TimeSpan.FromSeconds(1.0),
                    shared = true,
                    buffered = false)
                .CreateLogger()

        Log.Information("Starting FamilySlide...")
        Log.Information("About to call buildAvaloniaApp().StartWithClassicDesktopLifetime")
        
        try
            let result = buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
            Log.Information("FamilySlide application ended normally")
            result
        finally
            Log.CloseAndFlush()
            System.Threading.Thread.Sleep(100) // Give time for final flush
