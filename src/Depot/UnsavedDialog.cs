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
    public UnsavedDialog() : this(Words.Get(TextKeys.UnsavedThisDocument)) { }

    public UnsavedDialog(string name)
    {
        Title = Words.Get(TextKeys.UnsavedTitle);
        Width = 400;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Button save = new() { Content = Words.Get(TextKeys.UnsavedSave), IsDefault = true };
        Button dontSave = new() { Content = Words.Get(TextKeys.UnsavedDontSave) };
        Button cancel = new() { Content = Words.Get(TextKeys.UnsavedCancel), IsCancel = true };
        save.Click += (_, _) => Close(UnsavedChoice.Save);
        dontSave.Click += (_, _) => Close(UnsavedChoice.DontSave);
        cancel.Click += (_, _) => Close(UnsavedChoice.Cancel);

        Content = new StackPanel
        {
            Margin = new(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = Words.Get(TextKeys.UnsavedQuestion, name), FontWeight = Avalonia.Media.FontWeight.SemiBold, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new TextBlock { Text = Words.Get(TextKeys.UnsavedNote), Classes = { "secondary" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
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
