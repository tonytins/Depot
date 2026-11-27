using Avalonia;

namespace Depot;

/// <summary>Starts Depot.</summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Depot's Avalonia app, kept separate so off-screen tests can start it without a window.</summary>
    /// <remarks>
    /// On X11, Avalonia leaves the window manager to draw the title bar, buttons and all, unless it's asked to draw
    /// the frame itself, which Depot's bar needs to be the title bar; Roundhouse and Sims1Pkg ask the same. The option
    /// is marked experimental in Avalonia 12, so it's on only where <see cref="TitleBars.Merges()"/> would merge the bar.
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp()
    {
#pragma warning disable AVALONIA_X11_CSD // Experimental; Roundhouse and Sims1Pkg draw their merged title bars on X11 the same way.
        X11PlatformOptions x11 = new() { EnableDrawnDecorations = TitleBars.Merges() };
#pragma warning restore AVALONIA_X11_CSD
        return AppBuilder.Configure<App>().UsePlatformDetect().With(x11).LogToTrace();
    }
}
