namespace FamilySlide.App

open System
open Avalonia
open Avalonia.ReactiveUI

module Program =

    [<CompiledName "BuildAvaloniaApp">]
    let buildAvaloniaApp () =
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace(areas = Array.empty).UseReactiveUI() // ✅ Add this

    [<EntryPoint; STAThread>]
    let main argv =
        buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
