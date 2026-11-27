using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Depot;

/// <summary>
/// Depot's title bar, which is also its toolbar, as the Venture Design Language has it: the <see cref="Start"/> items
/// at the left, the window's title centred, the <see cref="End"/> items at the right, and Windows-style minimize,
/// maximize, and close buttons at the right end. Where the desktop allows (<see cref="TitleBars.Merges()"/>), it's the
/// window's title bar; otherwise it sits under the window manager's. Adapted from VentureOS's Roundhouse (public
/// domain), with the window buttons after Sims1Pkg's, since Depot is a cousin of Venture's apps rather than one of them.
/// </summary>
/// <remarks>Put the items in it, then call <see cref="Attach"/> from the window. Its look is in App.axaml.</remarks>
public sealed class WindowTitleBar : Border
{
    /// <summary>The bar's height: a Venture title bar with a toolbar in it.</summary>
    public const double BarHeight = 42;

    private readonly TitleBarPanel content = new();
    private readonly StackPanel startItems = new() { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel endItems = new() { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock title = new() { Classes = { "barTitle" }, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    private Window? host;

    public WindowTitleBar()
    {
        Height = BarHeight;
        Classes.Add("titleBar");

        MinimizeButton = WindowButton("MinimizeButton", Words.Get(TextKeys.WindowMinimize), "M0,5 L10,5");
        MaximizeButton = WindowButton("MaximizeButton", Words.Get(TextKeys.WindowMaximize), "M0.5,0.5 L9.5,0.5 L9.5,9.5 L0.5,9.5 Z");
        CloseButton = WindowButton("CloseButton", Words.Get(TextKeys.WindowClose), "M0,0 L10,10 M10,0 L0,10");
        CloseButton.Classes.Add("close");
        MinimizeButton.Click += (_, _) =>
        {
            if (host is not null)
                host.WindowState = WindowState.Minimized;
        };
        MaximizeButton.Click += (_, _) => ToggleMaximized();
        CloseButton.Click += (_, _) => host?.Close();
        WindowButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            IsVisible = false,
            Children = { MinimizeButton, MaximizeButton, CloseButton },
        };
        DockPanel.SetDock(WindowButtons, Dock.Right);

        content.Children.Add(startItems);
        content.Children.Add(title);
        content.Children.Add(endItems);
        Child = new DockPanel { Children = { WindowButtons, content } };
        PlaceContent();
        DoubleTapped += OnDoubleTapped;
    }

    /// <summary>The items at the bar's left end.</summary>
    public Avalonia.Controls.Controls Start => startItems.Children;

    /// <summary>The items at the bar's right end, before the window's buttons.</summary>
    public Avalonia.Controls.Controls End => endItems.Children;

    /// <summary>The title shown in the bar: the window's own.</summary>
    public string? Title => title.Text;

    /// <summary>The window's buttons, shown where Depot draws them (<see cref="DrawsWindowButtons"/>).</summary>
    public StackPanel WindowButtons { get; }

    public Button MinimizeButton { get; }

    public Button MaximizeButton { get; }

    public Button CloseButton { get; }

    /// <summary>Whether the bar is the window's title bar.</summary>
    public bool IsMerged { get; private set; }

    /// <summary>Whether Depot draws the window's buttons in the bar: where the bar is the title bar on Linux.</summary>
    public bool DrawsWindowButtons =>
        IsMerged && OperatingSystem.IsLinux() && host is { WindowState: not WindowState.FullScreen };

    /// <summary>
    /// Makes the bar <paramref name="window"/>'s title bar where <paramref name="merged"/> says it can be, with the
    /// window's buttons drawn in it on Linux and the system's own beside it on Windows and macOS; or leaves the window
    /// manager's title bar above it. Call it once, after the bar's items are in place, since only the controls in it
    /// then are kept from dragging the window.
    /// </summary>
    /// <exception cref="InvalidOperationException">The bar is already attached to a window.</exception>
    public void Attach(Window window, bool merged)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (host is not null)
            throw new InvalidOperationException("The title bar already belongs to a window.");

        host = window;
        IsMerged = merged;
        window.ExtendClientAreaToDecorationsHint = merged;
        window.ExtendClientAreaTitleBarHeightHint = merged ? Height : -1;
        title.Bind(TextBlock.TextProperty, window.GetObservable(Window.TitleProperty));

        // The bar drags the window; what's on it, buttons and pickers alike, takes clicks.
        WindowDecorationProperties.SetElementRole(this, merged ? WindowDecorationsElementRole.TitleBar : WindowDecorationsElementRole.None);
        foreach (var control in this.GetLogicalDescendants().OfType<TemplatedControl>())
            WindowDecorationProperties.SetElementRole(control, merged ? WindowDecorationsElementRole.User : WindowDecorationsElementRole.None);

        // On Linux, Avalonia draws the frame and Depot the buttons; the frame draws nothing else (FrameOnly in
        // App.axaml). Windows and macOS keep their own buttons beside the bar.
        if (merged && OperatingSystem.IsLinux()
            && Application.Current?.TryFindResource("FrameOnly", out var theme) == true && theme is ControlTheme frameOnly)
            window.WindowDecorationsTheme = frameOnly;

        window.PropertyChanged += OnHostPropertyChanged;
        ShowWindowButtons();
    }

    private static Button WindowButton(string name, string tip, string glyph)
    {
        Button button = new()
        {
            Name = name,
            Content = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(glyph), StrokeThickness = 1, Width = 10, Height = 10 },
        };
        button.Classes.Add("caption");
        ToolTip.SetTip(button, tip);
        return button;
    }

    private void OnHostPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == Window.WindowDecorationMarginProperty)
            PlaceContent();
        else if (change.Property == Window.WindowStateProperty || change.Property == Window.CanResizeProperty)
            ShowWindowButtons();
    }

    private void ShowWindowButtons()
    {
        WindowButtons.IsVisible = DrawsWindowButtons;

        // A window that can't be resized doesn't offer to grow.
        MaximizeButton.IsEnabled = host?.CanResize ?? true;
        ToolTip.SetTip(MaximizeButton, Words.Get(host?.WindowState == WindowState.Maximized ? TextKeys.WindowRestore : TextKeys.WindowMaximize));
        PlaceContent();
    }

    /// <summary>Keeps the bar's items clear of any window buttons the system draws over it, as macOS's on the left.</summary>
    private void PlaceContent()
    {
        var system = IsMerged && !DrawsWindowButtons && host is not null ? host.WindowDecorationMargin : default;
        var right = WindowButtons.IsVisible ? 4 : 8;
        content.Margin = new Thickness(8 + system.Left, 0, right + system.Right, 0);
    }

    private void ToggleMaximized()
    {
        if (host is { CanResize: true })
            host.WindowState = host.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Double-clicking the bar itself, not a control on it, maximizes or restores, as a title bar does.
        if (IsMerged && e.Source is Visual source && source.FindAncestorOfType<TemplatedControl>(includeSelf: true) is null)
            ToggleMaximized();
    }
}
