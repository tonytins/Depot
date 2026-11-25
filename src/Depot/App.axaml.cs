using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Depot;

/// <summary>Depot's application: its look, and its main window.</summary>
public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow window = new();
            desktop.MainWindow = window;
            // Depot document.uix opens it.
            if (desktop.Args is [var path, ..])
                window.Open(path);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
