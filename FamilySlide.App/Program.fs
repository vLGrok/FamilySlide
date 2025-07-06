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
        // Look for appsettings.json in the same directory as the executing assembly
        let appDirectory = System.AppDomain.CurrentDomain.BaseDirectory
        let appSettingsPath = Path.Combine(appDirectory, "appsettings.json")
        
        let config : IConfiguration = 
            ConfigurationBuilder()
                .AddJsonFile(appSettingsPath, optional = true)
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
        // Load configuration first to get log level
        let tempLogLevel = getLogLevel argv

        Log.Logger <-
            LoggerConfiguration()
                .MinimumLevel.Is(tempLogLevel)
                .WriteTo.Console()
                .WriteTo.File(
                    "familyslide.log", 
                    rollingInterval = RollingInterval.Day,
                    flushToDiskInterval = System.TimeSpan.FromSeconds(1.0),
                    shared = true,
                    buffered = false)
                .CreateLogger()

        Log.Information("Starting FamilySlide with log level: {LogLevel}...", tempLogLevel)
        
        // Load full configuration now that logging is set up
        let config = Configuration.loadConfiguration argv
        
        Log.Information("About to call buildAvaloniaApp().StartWithClassicDesktopLifetime")
        
        try
            let result = buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
            Log.Information("FamilySlide application ended normally")
            result
        finally
            Log.CloseAndFlush()
            System.Threading.Thread.Sleep(100) // Give time for final flush
