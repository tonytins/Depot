using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using Depot.Designer;
using Depot.Documents;
using Depot.Interface;
using Depot.Interface.Xml;
using Depot.Menus;
using Depot.Rendering;
using TextMateSharp.Grammars;
using Path = System.IO.Path;

namespace Depot;

/// <summary>
/// Depot's window: a document's text beside the canvas Hudini draws, which designs it, and an outline and inspector
/// of its elements. Every change, typed or designed, is a change to the text, so Undo takes any of them back.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>How long typing pauses before the canvas redraws.</summary>
    public static readonly TimeSpan RedrawDelay = TimeSpan.FromMilliseconds(200);

    // GNOME's blue, so the selection reads over the amber accent and every look's surfaces. Immutable, since a
    // mutable brush belongs to the thread that made it, and a static one is made wherever the class is first used.
    private static readonly IBrush SelectionBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x35, 0x84, 0xE4));

    private readonly TextEditor editor;
    private readonly Button menuButton;
    private readonly NativeMenu nativeMenu = new();
    private readonly Button undoButton;
    private readonly Button redoButton;
    private readonly Button addButton;
    private readonly ComboBox lookPicker;
    private readonly Button appearanceButton;
    private HudiniAppearance? appearance;
    private readonly TextBlock status;
    private readonly Image canvas;
    private readonly Panel canvasHost;
    private readonly Avalonia.Controls.Canvas selectionLayer;
    private readonly TextBlock canvasMessage;
    private readonly ListBox outline;
    private readonly TextBlock inspectorHint;
    private readonly InspectorPanel inspector;
    private readonly DispatcherTimer redraw;
    private readonly string pictureFolder = Path.Combine(Path.GetTempPath(), $"depot-{Environment.ProcessId}");
    private readonly PreviewQueue? preview;
    private readonly IDisposable? renderOwner;

    private DocumentFile? document;
    private int documentId;
    private bool loading;
    private bool syncingOutline;
    private bool closingConfirmed;
    private UIDocument? design;
    private int[] outlineRows = [];
    private Drag? drag;

    public MainWindow() : this(RendererLocation.Find()) { }

    /// <summary>A window drawing with depot-render at <paramref name="rendererPath"/>, or drawing nothing without one.</summary>
    public MainWindow(string? rendererPath) : this(rendererPath is null ? null : new Renderer(rendererPath)) { }

    private MainWindow(Renderer? renderer) : this(renderer is null ? null : renderer.RenderAsync, renderer) { }

    /// <summary>
    /// A window drawing with <paramref name="render"/>, such as a stand-in in tests, or nothing without one;
    /// <paramref name="owner"/> is disposed of when the window closes.
    /// </summary>
    public MainWindow(Func<RenderRequest, Task<RenderReply>>? render, IDisposable? owner = null)
    {
        AvaloniaXamlLoader.Load(this);
        editor = this.Get<TextEditor>("Editor");
        menuButton = this.Get<Button>("MenuButton");
        undoButton = this.Get<Button>("UndoButton");
        redoButton = this.Get<Button>("RedoButton");
        addButton = this.Get<Button>("AddButton");
        lookPicker = this.Get<ComboBox>("LookPicker");
        appearanceButton = this.Get<Button>("AppearanceButton");
        status = this.Get<TextBlock>("Status");
        canvas = this.Get<Image>("Canvas");
        canvasHost = this.Get<Panel>("CanvasHost");
        selectionLayer = this.Get<Avalonia.Controls.Canvas>("SelectionLayer");
        canvasMessage = this.Get<TextBlock>("CanvasMessage");
        outline = this.Get<ListBox>("Outline");
        inspectorHint = this.Get<TextBlock>("InspectorHint");
        inspector = this.Get<InspectorPanel>("InspectorView");
        renderOwner = owner;

        this.Get<WindowTitleBar>("TitleBar").Attach(this, TitleBars.Merges());

        lookPicker.ItemsSource = Enum.GetValues<CanvasLook>().Select(look => look.Title()).ToArray();
        lookPicker.SelectedIndex = (int)CanvasLook.Depot;
        lookPicker.SelectionChanged += (_, _) =>
        {
            RequestPicture();
            RefreshMenus();
        };

        Menus = BuildMenus();
        NativeMenu.SetMenu(this, nativeMenu);
        foreach (var (key, modifiers, run) in (ReadOnlySpan<(Key, KeyModifiers, Action)>)
                 [
                     (Key.O, KeyModifiers.Control, () => _ = ChooseAndOpenAsync()),
                     (Key.S, KeyModifiers.Control, () => Save()),
                     (Key.W, KeyModifiers.Control, Close),
                     (Key.Q, KeyModifiers.Control, Close),
                 ])
        {
            // Bound to the window, so they act only when the focused control, such as the editor, hasn't already.
            // Undo, Redo, and the designer's keys are the editor's and the canvas's own (OnCanvasKeyDown).
            KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(key, modifiers), Command = AppMenu.Command(run) });
        }

        menuButton.Click += (_, _) => ShowFlyout(Menus, menuButton, PlacementMode.BottomEdgeAlignedLeft);
        undoButton.Click += (_, _) => Undo();
        redoButton.Click += (_, _) => Redo();
        addButton.Click += (_, _) => ShowFlyout(MenuNamed("Insert").Entries, addButton, PlacementMode.BottomEdgeAlignedLeft);
        appearanceButton.Click += (_, _) => ShowFlyout(AppearanceEntries(), appearanceButton, PlacementMode.BottomEdgeAlignedRight);

        redraw = new DispatcherTimer { Interval = RedrawDelay };
        redraw.Tick += (_, _) =>
        {
            redraw.Stop();
            RequestPicture();
            ShowPanels();
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

        if (render is not null)
        {
            preview = new PreviewQueue(render);
            preview.Answered += ShowAnswer;
            preview.Failed += (request, exception) => ShowCanvasProblem(request, exception.Message);
        }

        canvasHost.PointerPressed += OnCanvasPressed;
        canvasHost.PointerMoved += OnCanvasMoved;
        canvasHost.PointerReleased += OnCanvasReleased;
        canvasHost.KeyDown += OnCanvasKeyDown;
        outline.SelectionChanged += (_, _) =>
        {
            if (!syncingOutline)
                Select(outline.SelectedIndex >= 0 && outline.SelectedIndex < outlineRows.Length ? outlineRows[outline.SelectedIndex] : null);
        };
        outline.KeyDown += OnCanvasKeyDown;
        inspector.Commit = CommitAttribute;

        Closing += OnClosing;
        Closed += (_, _) =>
        {
            renderOwner?.Dispose();
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
        ShowPanels();
    }

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

    /// <summary>Where each element of the document was drawn, or <see langword="null"/> when it can't be designed.</summary>
    public DesignMap? Map { get; private set; }

    /// <summary>
    /// The text as a document, with what Hudini reads from it or why it refuses it; <see langword="null"/> without
    /// one open, and for a <c>.uis</c> file, which isn't designed.
    /// </summary>
    public UIDocument? Design
    {
        get
        {
            if (document is not { IsUis: false })
                return null;
            if (design is null || design.Text != editor.Text || !ReferenceEquals(design.Appearance, appearance))
                design = UIDocument.Read(editor.Text, appearance);
            return design;
        }
    }

    /// <summary>
    /// The Hudini appearance file documents are read and drawn with, as a game reads its windows, or
    /// <see langword="null"/> for none. A <c>.uis</c> file is read without one, as Hudini reads it.
    /// </summary>
    public string? AppearancePath { get; private set; }

    /// <summary>
    /// Reads documents with the appearance at <paramref name="path"/> from now on, or with none for
    /// <see langword="null"/>, and draws the open one again. An appearance Hudini wouldn't read is refused, saying why
    /// in the status bar, and the one in use stays.
    /// </summary>
    /// <returns>Whether the appearance is in use.</returns>
    public bool UseAppearance(string? path)
    {
        HudiniAppearance? loaded = null;
        string? fullPath = null;
        if (path is not null)
        {
            try
            {
                fullPath = System.IO.Path.GetFullPath(path);
                loaded = HudiniAppearance.Load(fullPath);
            }
            catch (Exception exception) when (exception is UIScriptException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                ShowStatus($"Depot can't use the appearance {System.IO.Path.GetFileName(path)}: {exception.Message}", isProblem: true);
                return false;
            }
        }

        appearance = loaded;
        AppearancePath = fullPath;
        RefreshMenus();
        appearanceButton.Content = fullPath is null ? "No appearance ▾" : $"Appearance: {System.IO.Path.GetFileName(fullPath)} ▾";
        ToolTip.SetTip(appearanceButton, fullPath ?? "The Hudini appearance documents are read with, as a game reads its windows");
        design = null;
        RequestPicture();
        ShowPanels();
        return true;
    }

    private async Task ChooseAppearanceAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a Hudini Appearance",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Hudini appearances") { Patterns = ["*.xml"] }],
        });
        if (files is [var file] && file.TryGetLocalPath() is { } path)
            UseAppearance(path);
    }

    /// <summary>The selected element, by its index among the document's elements, or <see langword="null"/>.</summary>
    public int? Selection { get; private set; }

    /// <summary>The tab and pane the canvas shows, which follow the selection.</summary>
    public ShownParts Shown { get; private set; } = new();

    /// <summary>The inspector, for tests to read and edit.</summary>
    public InspectorPanel Inspector => inspector;

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
        design = null;
        Selection = null;
        Shown = new();
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
        Map = null;
        canvas.Source = null;
        ShowTitle();
        ShowEdited();
        ShowStatus(opened.IsReadOnly ? $"{opened.Name} is a .uis file: Depot shows it, and doesn't change it." : "", isProblem: false);
        RequestPicture();
        ShowPanels();
        return true;
    }

    /// <summary>
    /// Saves the text to the document exactly as it is, even text Hudini would refuse, which the status bar then
    /// warns of.
    /// </summary>
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
        if (Design?.Problem is { } problem)
            ShowStatus($"Saved {open.Name}, but Hudini won't read it as it is: {problem.Message}", isProblem: true);
        else
            ShowStatus($"Saved {open.Name}.", isProblem: false);
        return true;
    }

    /// <summary>Undoes the last change to the text, typed or designed, and redraws.</summary>
    public void Undo()
    {
        if (!editor.CanUndo)
            return;
        editor.Undo();
        RedrawNow();
    }

    /// <summary>Redoes what <see cref="Undo"/> undid, and redraws.</summary>
    public void Redo()
    {
        if (!editor.CanRedo)
            return;
        editor.Redo();
        RedrawNow();
    }

    /// <summary>Selects an element, by its index among the document's elements, or nothing.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The document has no such element.</exception>
    public void Select(int? element)
    {
        if (element is { } index && (Design?.Xml is not { } xml || index < 0 || index >= xml.Children.Count))
            throw new ArgumentOutOfRangeException(nameof(element), element, "The document has no such element.");
        Selection = element;

        // Something in a tab or pane that isn't shown is drawn once it's asked for; its outline follows the picture.
        if (Design is { IsEditable: true } current && Shown.Selecting(current, element) is var shown && shown != Shown)
        {
            Shown = shown;
            RequestPicture();
        }

        ShowSelection();
        ShowPanels();
    }

    /// <summary>
    /// Shows an edited document, such as one the designer changed, by replacing only the text that changed, so the
    /// editor keeps its place and Undo takes the edit back, and redraws now.
    /// </summary>
    /// <exception cref="InvalidOperationException">No document Depot designs is open.</exception>
    public void Apply(UIDocument edited)
    {
        ArgumentNullException.ThrowIfNull(edited);
        if (document is not { IsReadOnly: false })
            throw new InvalidOperationException("Open a UIScript document before designing one.");

        ReplaceEditorText(edited.Text);
        design = edited;
        RedrawNow();
    }

    /// <summary>Adds a control or part from the library, selects it, and says why if Hudini wouldn't read it.</summary>
    public bool Add(string element) => Edit(current => DesignEdits.Add(current, element));

    /// <summary>Moves the selected control or visual by whole pixels, as the arrow keys do.</summary>
    public bool Nudge(int dx, int dy)
    {
        if (Selection is not { } element || Map?.BoundsOf(element) is not { } bounds || Design?.Xml is not { } xml
            || !FrameEdit.IsFramed(xml.Children[element].Name))
        {
            return false;
        }

        return Edit(current => (FrameEdit.Apply(current, element, bounds, new FrameChange(dx, dy, 0, 0)), element));
    }

    /// <summary>Deletes the selection, with the comments above it, leaving nothing selected.</summary>
    public bool DeleteSelection() =>
        Selection is { } element && Edit(current => (current.RemoveElement(element), (int?)null));

    /// <summary>Copies the selection just after it, set off down and right, and selects the copy.</summary>
    public bool DuplicateSelection() =>
        Selection is { } element && Edit(current => DesignEdits.Duplicate(current, element));

    /// <summary>Moves the selected control or visual in front of the next one.</summary>
    public bool BringForward() => Selection is { } element && Edit(current => DesignEdits.BringForward(current, element));

    /// <summary>Moves the selected control or visual behind the one before it.</summary>
    public bool SendBackward() => Selection is { } element && Edit(current => DesignEdits.SendBackward(current, element));

    // Makes a designer edit, selecting what it says to, or says why Hudini wouldn't read the result.
    private bool Edit(Func<UIDocument, (UIDocument Document, int Element)> edit) =>
        Edit(current => { var (edited, element) = edit(current); return (edited, (int?)element); });

    private bool Edit(Func<UIDocument, (UIDocument Document, int? Element)> edit)
    {
        if (Design is not { IsEditable: true } current)
            return false;
        try
        {
            var (edited, element) = edit(current);
            Apply(edited);
            Select(element);
            return true;
        }
        catch (Exception exception) when (exception is UIScriptException or ArgumentException)
        {
            ShowStatus($"Hudini wouldn't read that: {exception.Message}", isProblem: true);
            return false;
        }
    }

    // Commits an inspector change to the selected element, or gives the reason it can't be made.
    private string? CommitAttribute(string attribute, string? value)
    {
        if (Selection is not { } element || Design is not { IsEditable: true } current)
            return "Select an element of a document Hudini reads first.";

        try
        {
            Apply(value is null ? current.RemoveAttribute(element, attribute) : current.SetAttribute(element, attribute, value));
            ShowPanels();
            return null;
        }
        catch (Exception exception) when (exception is UIScriptException or ArgumentException)
        {
            return exception.Message;
        }
    }

    /// <summary>
    /// Puts edited text in the editor by replacing only what changed, so the editor keeps its place and its Undo
    /// takes the change back.
    /// </summary>
    private void ReplaceEditorText(string text)
    {
        var current = editor.Text;
        var prefix = 0;
        var limit = Math.Min(current.Length, text.Length);
        while (prefix < limit && current[prefix] == text[prefix])
            prefix++;
        var suffix = 0;
        while (suffix < limit - prefix && current[^(suffix + 1)] == text[^(suffix + 1)])
            suffix++;

        editor.Document.Replace(prefix, current.Length - prefix - suffix, text.Substring(prefix, text.Length - prefix - suffix));
    }

    private void RedrawNow()
    {
        redraw.Stop();
        RequestPicture();
        ShowPanels();
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

    private void RequestPicture() => RequestPicture(editor.Text);

    // A .uis file is drawn from disk, as Hudini reads it; a UIScript document from its text, saved or not, or from
    // a drag's text before it reaches the editor.
    private void RequestPicture(string text)
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
        var shown = !open.IsUis && Design is { } current ? Shown.In(current) : new ShownParts();
        preview.Request(new RenderRequest
        {
            Document = open.IsUis ? null : text,
            Path = open.IsUis ? open.Path : null,
            Format = open.IsUis ? "uis" : "uix",
            Theme = Look.Name(),
            Title = Path.GetFileNameWithoutExtension(open.Path),
            Directory = Path.GetDirectoryName(open.Path),
            Appearance = open.IsUis ? null : AppearancePath,
            Tab = shown.Tab,
            Pane = shown.Pane,
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
        Map = Design is { IsEditable: true } current ? new DesignMap(current, reply.Frames!) : null;
        if (drag is null)
        {
            ShowStatus(Describe(reply.Frames!, document), isProblem: false);
            ShowSelection();
        }

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
        Map = null;
        ShowSelection();
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
        if (notes.Count > 0)
            return string.Join(" ", notes);
        var showing = (frames.Tab, frames.Pane) switch
        {
            ({ } tab, { } pane) => $", showing the {tab} tab and the {pane} pane",
            ({ } tab, null) => $", showing the {tab} tab",
            (null, { } pane) => $", showing the {pane} pane",
            _ => "",
        };
        return $"{frames.Width:0} × {frames.Height:0}, as Hudini draws it{showing}.";
    }

    /// <summary>
    /// Shows the document's elements in the outline, and the selected one's attributes in the inspector. A document
    /// Hudini refuses stays listed but can't be designed until it reads.
    /// </summary>
    private void ShowPanels()
    {
        var current = Design;
        var xml = current?.Xml;
        if (Selection is { } selected && (xml is null || selected >= xml.Children.Count))
            Selection = null;

        var editable = current is { IsEditable: true };
        addButton.IsEnabled = editable;
        undoButton.IsEnabled = editor.CanUndo && document is { IsReadOnly: false };
        redoButton.IsEnabled = editor.CanRedo && document is { IsReadOnly: false };

        syncingOutline = true;
        try
        {
            outlineRows = xml is null ? [] : [.. Enumerable.Range(0, xml.Children.Count)];
            outline.ItemsSource = xml is null ? Array.Empty<string>() : xml.Children.Select(OutlineLabel).ToArray();
            outline.SelectedIndex = Selection is { } row ? Array.IndexOf(outlineRows, row) : -1;
        }
        finally
        {
            syncingOutline = false;
        }

        inspectorHint.Text = document is null ? "Open a document to see its elements."
            : document.IsUis ? "Depot shows .uis files, and doesn't change them."
            : !editable ? $"Hudini can't read the text as it is, so only the text can be edited: {current?.Problem?.Message}"
            : Selection is null ? "Select an element to see its attributes."
            : "";
        inspectorHint.IsVisible = inspectorHint.Text.Length > 0;
        inspector.Show(editable && Selection is { } element ? xml!.Children[element] : null);
        RefreshMenus();
    }

    /// <summary>An element as the outline lists it: its kind, and its name.</summary>
    private static string OutlineLabel(FlatXmlElement element) =>
        (element.Depth > 0 ? new string(' ', element.Depth * 2) : "") + (element["name"] is { } name ? $"{element.Name}  {name}" : element.Name);

    private IReadOnlyList<Handle> HandlesOf(int element) =>
        Design is { IsEditable: true, Xml: { } xml } && element < xml.Children.Count ? FrameEdit.HandlesFor(xml.Children[element].Name) : [];

    /// <summary>Outlines the selection on its own layer over the canvas, with handles on what can be resized.</summary>
    private void ShowSelection() => ShowSelection(Selection is { } element ? Map?.BoundsOf(element) : null);

    private void ShowSelection(FrameRect? bounds)
    {
        selectionLayer.Children.Clear();
        if (canvas.Source is { } image)
            (selectionLayer.Width, selectionLayer.Height) = (image.Size.Width, image.Size.Height);
        if (Selection is not { } element || bounds is null)
            return;

        Rectangle box = new() { Width = bounds.Width + 1, Height = bounds.Height + 1, Stroke = SelectionBrush, StrokeThickness = 1 };
        Avalonia.Controls.Canvas.SetLeft(box, bounds.X - 0.5);
        Avalonia.Controls.Canvas.SetTop(box, bounds.Y - 0.5);
        selectionLayer.Children.Add(box);

        foreach (var handle in HandlesOf(element))
        {
            var (x, y) = FrameDrag.Centre(bounds, handle);
            Rectangle grip = new() { Width = FrameDrag.HandleSize, Height = FrameDrag.HandleSize, Fill = Brushes.White, Stroke = SelectionBrush, StrokeThickness = 1 };
            Avalonia.Controls.Canvas.SetLeft(grip, x - (FrameDrag.HandleSize / 2));
            Avalonia.Controls.Canvas.SetTop(grip, y - (FrameDrag.HandleSize / 2));
            selectionLayer.Children.Add(grip);
        }
    }

    /// <summary>A drag on the canvas: what it moves, from where, and the document it would make so far.</summary>
    private sealed class Drag(int element, Handle handle, Point start, FrameRect bounds, UIDocument from)
    {
        public int Element { get; } = element;
        public Handle Handle { get; } = handle;
        public Point Start { get; } = start;
        public FrameRect Bounds { get; } = bounds;
        public UIDocument From { get; } = from;
        public FrameChange Change { get; set; }
        public UIDocument? Pending { get; set; }
    }

    /// <summary>Presses on the canvas at a point in its pixels, as a click does; for the pointer and for tests.</summary>
    public void PressCanvas(Point point)
    {
        if (Map is null || Design is not { IsEditable: true } current)
            return;

        // A handle of what's selected wins over whatever is under it; otherwise the press selects what's there.
        var handle = Handle.None;
        var element = Selection is { } selected && Map.BoundsOf(selected) is { } bounds
            && (handle = FrameDrag.HandleAt(bounds, (float)point.X, (float)point.Y, HandlesOf(selected))) != Handle.None
                ? selected
                : Map.ElementAt((float)point.X, (float)point.Y);
        Select(element);

        // A handle resizes what has one; dragging anywhere else moves only a control or visual.
        if (element is { } index && Map.BoundsOf(index) is { } from
            && (handle != Handle.None || FrameEdit.IsFramed(current.Xml!.Children[index].Name)))
        {
            drag = new Drag(index, handle, point, from, current);
        }
    }

    /// <summary>Drags what was pressed to a point; the outline follows now, Hudini's picture as soon as it's drawn.</summary>
    public void DragCanvas(Point point)
    {
        if (drag is null)
            return;

        // Whole pixels, as documents are written.
        var change = FrameDrag.Change(drag.Bounds, drag.Handle,
            (float)Math.Round(point.X - drag.Start.X), (float)Math.Round(point.Y - drag.Start.Y));
        if (change == drag.Change)
            return;
        drag.Change = change;

        try
        {
            drag.Pending = change.IsEmpty ? null : FrameEdit.Apply(drag.From, drag.Element, drag.Bounds, change);
            ShowSelection(change.ApplyTo(drag.Bounds));
            RequestPicture((drag.Pending ?? drag.From).Text);
        }
        catch (UIScriptException exception)
        {
            ShowStatus($"Hudini wouldn't read that: {exception.Message}", isProblem: true);
        }
    }

    /// <summary>Lets go of a drag: the text changes once, so one Undo takes the whole drag back.</summary>
    public void ReleaseCanvas()
    {
        if (drag is null)
            return;
        var pending = drag.Pending;
        drag = null;
        if (pending is not null)
            Apply(pending);
        else
            ShowSelection();
    }

    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(canvasHost).Properties.IsLeftButtonPressed)
            return;
        canvasHost.Focus();
        PressCanvas(e.GetPosition(canvas));
        if (drag is not null)
            e.Pointer.Capture(canvasHost);
        e.Handled = true;
    }

    private void OnCanvasMoved(object? sender, PointerEventArgs e)
    {
        if (drag is not null)
            DragCanvas(e.GetPosition(canvas));
    }

    private void OnCanvasReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (drag is null)
            return;
        e.Pointer.Capture(null);
        ReleaseCanvas();
    }

    // The designer's keys, while the canvas or outline has the keyboard, so they still edit text in the editor.
    private void OnCanvasKeyDown(object? sender, KeyEventArgs e)
    {
        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var step = shift ? DesignEdits.Step : 1;
        Action? action = (e.Key, command, shift) switch
        {
            (Key.Z, true, false) => Undo,
            (Key.Z, true, true) or (Key.Y, true, _) => Redo,
            (Key.Delete or Key.Back, false, _) => () => DeleteSelection(),
            (Key.D, true, _) => () => DuplicateSelection(),
            (Key.OemCloseBrackets, true, _) => () => BringForward(),
            (Key.OemOpenBrackets, true, _) => () => SendBackward(),
            (Key.Left, false, _) when sender == canvasHost => () => Nudge(-step, 0),
            (Key.Right, false, _) when sender == canvasHost => () => Nudge(step, 0),
            (Key.Up, false, _) when sender == canvasHost => () => Nudge(0, -step),
            (Key.Down, false, _) when sender == canvasHost => () => Nudge(0, step),
            _ => null,
        };
        if (action is null)
            return;
        action();
        e.Handled = true;
    }

    /// <summary>Depot's menus, shown in the desktop's global menu bar where it has one, and behind ☰ where it doesn't.</summary>
    public IReadOnlyList<AppMenuGroup> Menus { get; }

    /// <summary>Where Hudini's guide to UIScript is, which Help opens.</summary>
    public static readonly Uri GuideAddress = new("https://github.com/tonytins/Hudini/blob/main/docs/UISCRIPT.md");

    private AppMenuGroup MenuNamed(string title) => Menus.Single(group => group.Title == title);

    private List<MenuEntry> AppearanceEntries() =>
    [
        new MenuCommand("Choose an Appearance…", () => _ = ChooseAppearanceAsync()),
        new MenuCommand("No Appearance", () => UseAppearance(null), IsEnabled: () => AppearancePath is not null),
    ];

    private List<AppMenuGroup> BuildMenus()
    {
        bool Writable() => document is { IsReadOnly: false };
        bool Editable() => Design is { IsEditable: true };
        bool Selected() => Editable() && Selection is not null;
        KeyGesture Ctrl(Key key, KeyModifiers more = KeyModifiers.None) => new(key, KeyModifiers.Control | more);

        return
        [
            new("File",
            [
                new MenuCommand("Open…", () => _ = ChooseAndOpenAsync(), Ctrl(Key.O)),
                new MenuCommand("Save", () => Save(), Ctrl(Key.S), () => HasUnsavedChanges),
                new MenuSeparator(),
                new MenuCommand("Close Window", Close, Ctrl(Key.W)),
                new MenuCommand("Quit", Close, Ctrl(Key.Q)),
            ]),
            new("Edit",
            [
                new MenuCommand("Undo", Undo, Ctrl(Key.Z), () => Writable() && editor.CanUndo),
                new MenuCommand("Redo", Redo, Ctrl(Key.Z, KeyModifiers.Shift), () => Writable() && editor.CanRedo),
                new MenuSeparator(),
                new MenuCommand("Duplicate", () => DuplicateSelection(), Ctrl(Key.D), Selected),
                new MenuCommand("Delete", () => DeleteSelection(), new KeyGesture(Key.Delete), Selected),
            ]),
            new("Insert",
            [
                .. DesignEdits.Library.Select(control => new MenuCommand(control.Element, () => Add(control.Element), IsEnabled: Editable)),
                new MenuSeparator(),
                .. DesignEdits.Parts.Select(part => new MenuCommand(part.Title, () => Add(part.Element), IsEnabled: Editable)),
            ]),
            new("Arrange",
            [
                new MenuCommand("Bring Forward", () => BringForward(), Ctrl(Key.OemCloseBrackets), Selected),
                new MenuCommand("Send Backward", () => SendBackward(), Ctrl(Key.OemOpenBrackets), Selected),
            ]),
            new("View",
            [
                new AppMenuGroup("Look", [.. Enum.GetValues<CanvasLook>().Select(look =>
                    new MenuCommand(look.Title(), () => Look = look, IsChecked: () => Look == look))]),
                new AppMenuGroup("Appearance", AppearanceEntries()),
            ]),
            new("Help",
            [
                new MenuCommand("UIScript Guide", () => _ = OpenGuideAsync()),
            ]),
        ];
    }

    /// <summary>Shows menu entries as they are now, under <paramref name="target"/>.</summary>
    private static void ShowFlyout(IEnumerable<MenuEntry> entries, Control target, PlacementMode placement)
    {
        var flyout = AppMenu.Flyout(entries);
        flyout.Placement = placement;
        flyout.ShowAt(target);
    }

    // The global menu bar holds a copy of the menus, so it's filled again whenever what's enabled or checked changes.
    // Menus is null only while the constructor builds it.
    private void RefreshMenus()
    {
        if (Menus is not null)
            AppMenu.Fill(nativeMenu, Menus);
    }

    private async Task OpenGuideAsync()
    {
        if (!await Launcher.LaunchUriAsync(GuideAddress))
            ShowStatus($"Depot couldn't open {GuideAddress} in a browser.", isProblem: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // Where the desktop shows the menus in its own menu bar, ☰ would only repeat them.
        if (change.Property == NativeMenu.IsNativeMenuExportedProperty && menuButton is not null)
            menuButton.IsVisible = !NativeMenu.GetIsNativeMenuExported(this);
    }

    private void ShowStatus(string message, bool isProblem)
    {
        status.Text = message;
        status.Classes.Set("problem", isProblem);
    }

    private void ShowEdited()
    {
        RefreshMenus();
        undoButton.IsEnabled = editor.CanUndo && document is { IsReadOnly: false };
        redoButton.IsEnabled = editor.CanRedo && document is { IsReadOnly: false };
        ShowTitle();
    }

    private void ShowTitle() => Title = document is null
        ? "Depot"
        : $"{document.Name}{(HasUnsavedChanges ? " (edited)" : "")} — Depot";

    private ThemeName HighlightTheme() => ActualThemeVariant == ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus;
}
