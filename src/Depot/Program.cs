using Avalonia;

namespace Depot;

/// <summary>Starts Depot.</summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Depot's Avalonia app, kept separate so off-screen tests can start it without a window.</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
