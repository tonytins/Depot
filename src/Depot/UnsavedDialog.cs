using Avalonia.Controls;
using Avalonia.Layout;

namespace Depot;

/// <summary>What to do with changes that haven't been saved.</summary>
public enum UnsavedChoice
{
    /// <summary>Keep the document open, as if nothing was asked.</summary>
    Cancel,

    /// <summary>Save, then go on.</summary>
    Save,

    /// <summary>Let the changes go.</summary>
    DontSave,
}

/// <summary>Asks whether to save a document's changes before it's closed or replaced.</summary>
public sealed class UnsavedDialog : Window
{
    public UnsavedDialog() : this("this document") { }

    public UnsavedDialog(string name)
    {
        Title = "Unsaved Changes";
        Width = 400;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Button save = new() { Content = "Save", IsDefault = true };
        Button dontSave = new() { Content = "Don't Save" };
        Button cancel = new() { Content = "Cancel", IsCancel = true };
        save.Click += (_, _) => Close(UnsavedChoice.Save);
        dontSave.Click += (_, _) => Close(UnsavedChoice.DontSave);
        cancel.Click += (_, _) => Close(UnsavedChoice.Cancel);

        Content = new StackPanel
        {
            Margin = new(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = $"Save the changes to {name}?", FontWeight = Avalonia.Media.FontWeight.SemiBold, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new TextBlock { Text = "If you don't, they're lost.", Classes = { "secondary" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { dontSave, cancel, save },
                },
            },
        };
    }
}
