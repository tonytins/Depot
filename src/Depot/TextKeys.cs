namespace Depot;

/// <summary>
/// The keys of Depot's words in <c>uitext/english.dir/_100_depot.cst</c>, in the table's order. Every key here is in the
/// table and every key in the table is here, which a test checks.
/// </summary>
public static class TextKeys
{
    // The window's title
    public const string AppName = "app name";
    public const string TitleDocument = "title document";
    public const string TitleDocumentEdited = "title document edited";

    // The title bar
    public const string BarMenuTip = "bar menu tip";
    public const string BarUndo = "bar undo";
    public const string BarUndoTip = "bar undo tip";
    public const string BarRedo = "bar redo";
    public const string BarRedoTip = "bar redo tip";
    public const string BarAdd = "bar add";
    public const string BarAddTip = "bar add tip";
    public const string BarLookTip = "bar look tip";
    public const string BarNoAppearance = "bar no appearance";
    public const string BarAppearance = "bar appearance";
    public const string BarAppearanceTip = "bar appearance tip";
    public const string WindowMinimize = "window minimize";
    public const string WindowMaximize = "window maximize";
    public const string WindowRestore = "window restore";
    public const string WindowClose = "window close";

    // The menus
    public const string MenuFile = "menu file";
    public const string MenuEdit = "menu edit";
    public const string MenuInsert = "menu insert";
    public const string MenuArrange = "menu arrange";
    public const string MenuView = "menu view";
    public const string MenuHelp = "menu help";
    public const string ItemOpen = "item open";
    public const string ItemSave = "item save";
    public const string ItemCloseWindow = "item close window";
    public const string ItemQuit = "item quit";
    public const string ItemUndo = "item undo";
    public const string ItemRedo = "item redo";
    public const string ItemDuplicate = "item duplicate";
    public const string ItemDelete = "item delete";
    public const string ItemBringForward = "item bring forward";
    public const string ItemSendBackward = "item send backward";
    public const string ItemLook = "item look";
    public const string ItemAppearance = "item appearance";
    public const string ItemChooseAppearance = "item choose appearance";
    public const string ItemNoAppearance = "item no appearance";
    public const string ItemGuide = "item guide";

    // What Add and Insert offer besides controls, which go by their element's name
    public const string PartPicture = "part picture";
    public const string PartToolbarItem = "part toolbar item";
    public const string PartTitleField = "part title field";

    // The looks the canvas draws in
    public const string LookDepot = "look depot";
    public const string LookDepotLight = "look depot light";
    public const string LookStandard = "look standard";

    // The panes
    public const string PaneOutline = "pane outline";
    public const string PaneInspector = "pane inspector";
    public const string CanvasNoDocument = "canvas no document";
    public const string CanvasNoRenderer = "canvas no renderer";
    public const string HintNoDocument = "hint no document";
    public const string HintUis = "hint uis";
    public const string HintUnreadable = "hint unreadable";
    public const string HintSelect = "hint select";

    // The inspector
    public const string GroupIdentity = "group identity";
    public const string GroupFrame = "group frame";
    public const string GroupText = "group text";
    public const string GroupAppearance = "group appearance";
    public const string GroupBehaviour = "group behaviour";
    public const string GroupOther = "group other";
    public const string InspectorOtherNote = "inspector other note";
    public const string InspectorRequired = "inspector required";
    public const string InspectorSelectFirst = "inspector select first";

    // The status bar
    public const string StatusStart = "status start";
    public const string StatusUis = "status uis";
    public const string StatusSaved = "status saved";
    public const string StatusSavedUnreadable = "status saved unreadable";
    public const string StatusRefused = "status refused";
    public const string StatusAppearanceRefused = "status appearance refused";
    public const string StatusAppearanceRefusedNone = "status appearance refused none";
    public const string StatusUnsavedFailed = "status unsaved failed";
    public const string StatusPictureUnreadable = "status picture unreadable";
    public const string StatusGuideFailed = "status guide failed";
    public const string StatusIgnored = "status ignored";
    public const string StatusMissingImages = "status missing images";
    public const string StatusMissingString = "status missing string";
    public const string StatusMissingStrings = "status missing strings";
    public const string StatusUisPart = "status uis part";
    public const string StatusUisParts = "status uis parts";
    public const string StatusSize = "status size";
    public const string StatusSizeTab = "status size tab";
    public const string StatusSizePane = "status size pane";
    public const string StatusSizeTabPane = "status size tab pane";

    // Choosing files
    public const string ChooseDocument = "choose document";
    public const string ChooseAppearance = "choose appearance";
    public const string FilesInterface = "files interface";
    public const string FilesUix = "files uix";
    public const string FilesUis = "files uis";
    public const string FilesAppearance = "files appearance";

    // Unsaved changes
    public const string UnsavedTitle = "unsaved title";
    public const string UnsavedQuestion = "unsaved question";
    public const string UnsavedNote = "unsaved note";
    public const string UnsavedSave = "unsaved save";
    public const string UnsavedDontSave = "unsaved dont save";
    public const string UnsavedCancel = "unsaved cancel";
    public const string UnsavedThisDocument = "unsaved this document";

    // Documents
    public const string DocumentMissing = "document missing";
    public const string DocumentTooLarge = "document too large";
    public const string DocumentUnreadable = "document unreadable";
    public const string DocumentNotUtf8 = "document not utf8";
    public const string DocumentUnsaved = "document unsaved";

    // depot-render
    public const string RendererFailedBefore = "renderer failed before";
    public const string RendererTimedOut = "renderer timed out";
    public const string RendererLost = "renderer lost";
    public const string RendererSaid = "renderer said";
    public const string RendererCantStart = "renderer cant start";
    public const string RendererUnreadableAnswer = "renderer unreadable answer";
    public const string RendererEmptyAnswer = "renderer empty answer";
}
