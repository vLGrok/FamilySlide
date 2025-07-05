namespace FamilySlide.App

open System
open System.IO
open Avalonia
open Avalonia.ReactiveUI
open Serilog
open Serilog.Events
open Microsoft.Extensions.Configuration

module Program =

    let getLogLevel (argv: string[]) =
        let config : IConfiguration = 
            ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional = true)
                .AddCommandLine(argv)
                .Build()
        
        let levelString = 
            match config.["Logging:MinimumLevel"] with
            | null -> "Information"
            | value -> value
        
        match levelString.ToUpperInvariant() with
        | "VERBOSE" -> LogEventLevel.Verbose
        | "DEBUG" -> LogEventLevel.Debug
        | "INFORMATION" -> LogEventLevel.Information
        | "WARNING" -> LogEventLevel.Warning
        | "ERROR" -> LogEventLevel.Error
        | "FATAL" -> LogEventLevel.Fatal
        | _ -> LogEventLevel.Information

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
        let logLevel = getLogLevel argv

        Log.Logger <-
            LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
                .WriteTo.Console()
                .WriteTo.File(
                    "familyslide.log", 
                    rollingInterval = RollingInterval.Day,
                    flushToDiskInterval = System.TimeSpan.FromSeconds(1.0),
                    shared = true,
                    buffered = false)
                .CreateLogger()

        Log.Information("Starting FamilySlide with log level: {LogLevel}...", logLevel)
        Log.Information("About to call buildAvaloniaApp().StartWithClassicDesktopLifetime")
        
        try
            let result = buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
            Log.Information("FamilySlide application ended normally")
            result
        finally
            Log.CloseAndFlush()
            System.Threading.Thread.Sleep(100) // Give time for final flush
