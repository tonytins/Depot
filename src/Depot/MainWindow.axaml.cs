using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Depot;

/// <summary>Depot's window. Empty until the live preview arrives.</summary>
public sealed partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);
}
