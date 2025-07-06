namespace FamilySlide.App

open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml

type App() =
    inherit Application()

    // Store command line arguments for access by MainWindow
    static member val CommandLineArgs = [||] with get, set

    override this.Initialize() =
            AvaloniaXamlLoader.Load(this)

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
             desktop.MainWindow <- MainWindow(App.CommandLineArgs)
        | _ -> ()

        base.OnFrameworkInitializationCompleted()
