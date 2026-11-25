using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using Depot.Documents;
using Depot.Rendering;
using TextMateSharp.Grammars;

namespace Depot;

/// <summary>Depot's window: a document's text beside the canvas, drawn by Hudini as you type.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>How long typing pauses before the canvas redraws.</summary>
    public static readonly TimeSpan RedrawDelay = TimeSpan.FromMilliseconds(200);

    private readonly TextEditor editor;
    private readonly Button saveButton;
    private readonly ComboBox lookPicker;
    private readonly TextBlock status;
    private readonly Image canvas;
    private readonly TextBlock canvasMessage;
    private readonly DispatcherTimer redraw;
    private readonly string pictureFolder = Path.Combine(Path.GetTempPath(), $"depot-{Environment.ProcessId}");
    private readonly Renderer? renderer;
    private readonly PreviewQueue? preview;

    private DocumentFile? document;
    private int documentId;
    private bool loading;
    private bool closingConfirmed;

    public MainWindow() : this(RendererLocation.Find()) { }

    /// <summary>A window drawing with depot-render at <paramref name="rendererPath"/>, or drawing nothing without one.</summary>
    public MainWindow(string? rendererPath)
    {
        AvaloniaXamlLoader.Load(this);
        editor = this.Get<TextEditor>("Editor");
        saveButton = this.Get<Button>("SaveButton");
        lookPicker = this.Get<ComboBox>("LookPicker");
        status = this.Get<TextBlock>("Status");
        canvas = this.Get<Image>("Canvas");
        canvasMessage = this.Get<TextBlock>("CanvasMessage");
        RendererPath = rendererPath;

        lookPicker.ItemsSource = Enum.GetValues<CanvasLook>().Select(look => look.Title()).ToArray();
        lookPicker.SelectedIndex = (int)CanvasLook.Depot;
        lookPicker.SelectionChanged += (_, _) => RequestPicture();

        this.Get<Button>("OpenButton").Click += async (_, _) => await ChooseAndOpenAsync();
        saveButton.Click += (_, _) => Save();

        redraw = new DispatcherTimer { Interval = RedrawDelay };
        redraw.Tick += (_, _) =>
        {
            redraw.Stop();
            RequestPicture();
        };
        editor.TextChanged += (_, _) =>
        {
            if (loading)
                return;
            ShowEdited();
            redraw.Stop();
            redraw.Start();
        };

        // XML highlighting, in colors that follow Depot's light or dark look.
        RegistryOptions highlightOptions = new(HighlightTheme());
        var highlighting = editor.InstallTextMate(highlightOptions);
        highlighting.SetGrammar(highlightOptions.GetScopeByLanguageId(highlightOptions.GetLanguageByExtension(".xml").Id));
        ActualThemeVariantChanged += (_, _) => highlighting.SetTheme(highlightOptions.LoadTheme(HighlightTheme()));

        if (rendererPath is not null)
        {
            renderer = new Renderer(rendererPath);
            preview = new PreviewQueue(renderer.RenderAsync);
            preview.Answered += ShowAnswer;
            preview.Failed += (request, exception) => ShowCanvasProblem(request, exception.Message);
        }

        Closing += OnClosing;
        Closed += (_, _) =>
        {
            renderer?.Dispose();
            try
            {
                if (Directory.Exists(pictureFolder))
                    Directory.Delete(pictureFolder, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Pictures left in the temporary folder are the system's to clear.
            }
        };
        ShowTitle();
    }

    /// <summary>The depot-render Depot draws with, or <see langword="null"/> when none was found.</summary>
    public string? RendererPath { get; }

    /// <summary>The document open, if any.</summary>
    public DocumentFile? Document => document;

    /// <summary>The text being edited.</summary>
    public string Text => editor.Text;

    /// <summary>Whether the text differs from what's saved.</summary>
    public bool HasUnsavedChanges => document is { IsReadOnly: false } && editor.Text != document.Text;

    /// <summary>The look the canvas draws in.</summary>
    public CanvasLook Look
    {
        get => (CanvasLook)Math.Max(0, lookPicker.SelectedIndex);
        set => lookPicker.SelectedIndex = (int)value;
    }

    /// <summary>The newest picture shown, and where Hudini placed what it shows.</summary>
    public FrameMap? Frames { get; private set; }

    /// <summary>depot-render answered for the open document, after the canvas and status bar show it.</summary>
    public event Action<RenderRequest, RenderReply>? Answered;

    /// <summary>Opens a document in place of the one open, which must be saved or let go of first.</summary>
    /// <returns>Whether it opened; when it didn't, the reason is in the status bar.</returns>
    public bool Open(string path)
    {
        DocumentFile opened;
        try
        {
            opened = DocumentFile.Open(path);
        }
        catch (DocumentException exception)
        {
            ShowStatus(exception.Message, isProblem: true);
            return false;
        }

        document = opened;
        documentId++;
        loading = true;
        try
        {
            editor.Text = opened.Text;
            editor.IsReadOnly = opened.IsReadOnly;
            editor.Document.UndoStack.ClearAll();
            editor.ScrollToHome();
        }
        finally
        {
            loading = false;
        }

        Frames = null;
        canvas.Source = null;
        ShowTitle();
        ShowStatus(opened.IsReadOnly ? $"{opened.Name} is a .uis file: Depot shows it, and doesn't change it." : "", isProblem: false);
        RequestPicture();
        return true;
    }

    /// <summary>Saves the text to the document.</summary>
    /// <returns>Whether it saved; when it didn't, the reason is in the status bar.</returns>
    public bool Save()
    {
        if (document is not { IsReadOnly: false } open)
            return false;
        try
        {
            open.Save(editor.Text);
        }
        catch (DocumentException exception)
        {
            ShowStatus(exception.Message, isProblem: true);
            return false;
        }

        ShowEdited();
        ShowStatus($"Saved {open.Name}.", isProblem: false);
        return true;
    }

    private async Task ChooseAndOpenAsync()
    {
        if (!await LetGoOfChangesAsync())
            return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a Document",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Interface documents") { Patterns = ["*.uix", "*.uis"] },
                new FilePickerFileType("UIScript") { Patterns = ["*.uix"] },
                new FilePickerFileType("Maxis interface scripts") { Patterns = ["*.uis"] },
            ],
        });
        if (files is [var file] && file.TryGetLocalPath() is { } path)
            Open(path);
    }

    // Whether whatever's unsaved has been saved or let go of, asking if there's any.
    private async Task<bool> LetGoOfChangesAsync()
    {
        if (!HasUnsavedChanges || document is null)
            return true;
        var choice = await new UnsavedDialog(document.Name).ShowDialog<UnsavedChoice>(this);
        return choice switch
        {
            UnsavedChoice.Save => Save(),
            UnsavedChoice.DontSave => true,
            _ => false,
        };
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (closingConfirmed || !HasUnsavedChanges)
            return;
        e.Cancel = true;
        try
        {
            if (await LetGoOfChangesAsync())
            {
                closingConfirmed = true;
                Close();
            }
        }
        catch (Exception exception)
        {
            // Asking failed, so the window stays open and the work with it.
            ShowStatus($"Couldn't ask about unsaved changes: {exception.Message}", isProblem: true);
        }
    }

    private void RequestPicture()
    {
        if (document is not { } open)
            return;
        if (preview is null)
        {
            canvasMessage.Text = "Depot can't draw: it didn't find depot-render. Build it with swift build in "
                + $"depot-render, put it beside Depot, or name it with {RendererLocation.Variable}.";
            canvasMessage.IsVisible = true;
            return;
        }

        Directory.CreateDirectory(pictureFolder);
        // A .uis file is drawn from disk, as Hudini reads it; a UIScript document from its text, saved or not.
        preview.Request(new RenderRequest
        {
            Document = open.IsUis ? null : editor.Text,
            Path = open.IsUis ? open.Path : null,
            Format = open.IsUis ? "uis" : "uix",
            Theme = Look.Name(),
            Title = Path.GetFileNameWithoutExtension(open.Path),
            Directory = Path.GetDirectoryName(open.Path),
            Output = Path.Combine(pictureFolder, "canvas.png"),
            DocumentId = documentId,
        });
    }

    private void ShowAnswer(RenderRequest request, RenderReply reply)
    {
        if (request.DocumentId != documentId)
            return;
        if (reply.Error is { } error)
        {
            ShowCanvasProblem(request, error);
            Answered?.Invoke(request, reply);
            return;
        }

        Bitmap picture;
        try
        {
            // Read whole, so the file is free for the next picture.
            using MemoryStream bytes = new(File.ReadAllBytes(request.Output));
            picture = new Bitmap(bytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            ShowCanvasProblem(request, $"Couldn't read depot-render's picture: {exception.Message}");
            Answered?.Invoke(request, reply);
            return;
        }

        var old = canvas.Source as Bitmap;
        canvas.Source = picture;
        canvas.Opacity = 1;
        old?.Dispose();
        canvasMessage.IsVisible = false;
        Frames = reply.Frames;
        ShowStatus(Describe(reply.Frames!, document), isProblem: false);
        Answered?.Invoke(request, reply);
    }

    // The last good picture stays, faded, so the layout doesn't jump while a typo is fixed.
    private void ShowCanvasProblem(RenderRequest request, string message)
    {
        if (request.DocumentId != documentId)
            return;
        canvas.Opacity = canvas.Source is null ? 1 : 0.4;
        canvasMessage.IsVisible = canvas.Source is null;
        canvasMessage.Text = message;
        ShowStatus(message, isProblem: true);
    }

    /// <summary>What the status bar says about a picture: anything Hudini left out or couldn't find.</summary>
    public static string Describe(FrameMap frames, DocumentFile? document)
    {
        ArgumentNullException.ThrowIfNull(frames);
        List<string> notes = [];
        if (document is { IsReadOnly: true })
            notes.Add($"{document.Name} is a .uis file: Depot shows it, and doesn't change it.");
        if (frames.IgnoredAttributes.Count > 0)
            notes.Add($"Hudini ignores {string.Join(", ", frames.IgnoredAttributes)}.");
        if (frames.MissingImages.Count > 0)
            notes.Add($"Images not found, outlined in red: {string.Join(", ", frames.MissingImages)}.");
        if (frames.MissingStrings.Count > 0)
            notes.Add($"{frames.MissingStrings.Count} {(frames.MissingStrings.Count == 1 ? "string shows its" : "strings show their")} table and index, since Depot doesn't read string tables yet.");
        if (frames.Notes.Count > 0)
            notes.Add($"{frames.Notes.Count} {(frames.Notes.Count == 1 ? "part" : "parts")} of the .uis file Hudini can't represent.");
        return notes.Count == 0 ? $"{frames.Width:0} × {frames.Height:0}, as Hudini draws it." : string.Join(" ", notes);
    }

    private void ShowStatus(string message, bool isProblem)
    {
        status.Text = message;
        status.Classes.Set("problem", isProblem);
    }

    private void ShowEdited()
    {
        saveButton.IsEnabled = HasUnsavedChanges;
        ShowTitle();
    }

    private void ShowTitle() => Title = document is null
        ? "Depot"
        : $"{document.Name}{(HasUnsavedChanges ? " (edited)" : "")} — Depot";

    private ThemeName HighlightTheme() => ActualThemeVariant == ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus;
}
