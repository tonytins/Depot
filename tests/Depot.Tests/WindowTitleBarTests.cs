using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;

namespace Depot.Tests;

/// <summary><see cref="WindowTitleBar"/>: Depot's title bar, which is its toolbar.</summary>
public sealed class WindowTitleBarTests
{
    private static (Window Window, WindowTitleBar Bar, Button Item) Make()
    {
        WindowTitleBar bar = new();
        Button item = new() { Content = "Open…" };
        bar.Start.Add(item);
        Window window = new() { Title = "budget.uix — Depot", Content = bar };
        return (window, bar, item);
    }

    [Fact]
    public void UnderTheWindowManagersTitleBarItDrawsNoWindowButtons() => TestApp.Run(() =>
    {
        var (window, bar, item) = Make();

        bar.Attach(window, merged: false);

        Assert.False(bar.IsMerged);
        Assert.False(bar.WindowButtons.IsVisible);
        Assert.False(window.ExtendClientAreaToDecorationsHint);
        Assert.Equal(WindowDecorationsElementRole.None, WindowDecorationProperties.GetElementRole(item));
    });

    [Fact]
    public void AsTheTitleBarItDragsTheWindowAndItsItemsTakeClicks() => TestApp.Run(() =>
    {
        var (window, bar, item) = Make();

        bar.Attach(window, merged: true);

        Assert.True(window.ExtendClientAreaToDecorationsHint);
        Assert.Equal(WindowTitleBar.BarHeight, window.ExtendClientAreaTitleBarHeightHint);
        Assert.Equal(WindowDecorationsElementRole.TitleBar, WindowDecorationProperties.GetElementRole(bar));
        Assert.Equal(WindowDecorationsElementRole.User, WindowDecorationProperties.GetElementRole(item));
        Assert.Equal(WindowDecorationsElementRole.User, WindowDecorationProperties.GetElementRole(bar.CloseButton));
        // Depot draws the buttons only where the system doesn't: on Linux.
        Assert.Equal(OperatingSystem.IsLinux(), bar.WindowButtons.IsVisible);
    });

    [Fact]
    public void TheButtonsAreWindowsOrderedMinimizeMaximizeClose() => TestApp.Run(() =>
    {
        var (_, bar, _) = Make();

        Assert.Equal([bar.MinimizeButton, bar.MaximizeButton, bar.CloseButton], bar.WindowButtons.Children);
    });

    [Fact]
    public void TheBarShowsTheWindowsTitleAsItChanges() => TestApp.Run(() =>
    {
        var (window, bar, _) = Make();
        bar.Attach(window, merged: true);

        window.Title = "budget.uix (edited) — Depot";

        Assert.Equal("budget.uix (edited) — Depot", bar.Title);
    });

    [Fact]
    public void AWindowThatCantBeResizedDoesntOfferToGrow() => TestApp.Run(() =>
    {
        var (window, bar, _) = Make();
        bar.Attach(window, merged: true);

        window.CanResize = false;

        Assert.False(bar.MaximizeButton.IsEnabled);
    });

    [Fact]
    public void ABarBelongsToOneWindow() => TestApp.Run(() =>
    {
        var (window, bar, _) = Make();
        bar.Attach(window, merged: true);

        Assert.Throws<InvalidOperationException>(() => bar.Attach(new Window(), merged: true));
    });
}
