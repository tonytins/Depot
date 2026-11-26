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
            // Depot [--appearance appearance.xml] [document.uix]: reads with the appearance, and opens the document.
            var (document, appearance) = StartupArguments.Read(desktop.Args ?? []);
            if (appearance is not null)
                window.UseAppearance(appearance);
            if (document is not null)
                window.Open(document);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
