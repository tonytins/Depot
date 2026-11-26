// These run without a window: reading requests and working out frame maps need none. Drawing is checked by running
// depot-render on a display.

@testable import DepotRender
import Foundation
import Hudini
import Testing

/// Hudini's example documents, in tests/Fixtures, which Depot.Interface's tests read too.
private let examples = URL(fileURLWithPath: #filePath)
    .deletingLastPathComponent()
    .appendingPathComponent("../../../tests/Fixtures")
    .standardizedFileURL

private func exampleDocuments() throws -> [URL] {
    let found = FileManager.default.enumerator(at: examples, includingPropertiesForKeys: nil)?
        .compactMap { $0 as? URL }
        .filter { ["uix", "uis"].contains($0.pathExtension) } ?? []
    return found.sorted { $0.path < $1.path }
}

private func frames(_ request: RenderRequest) throws -> FrameMap {
    let resolved = try request.resolve()
    let view = UIView(script: resolved.script, theme: resolved.theme)
    resolved.show(in: view)
    return try FrameMap(script: resolved.script, view: view)
}

private let button = #"<Button name="ok" x="10" y="20" width="80" height="24"/>"#

@Test func everyHudiniExampleHasAFrameMap() throws {
    let documents = try exampleDocuments()
    #expect(documents.count >= 10, "expected Hudini's demo documents at \(examples.path)")
    for url in documents {
        let map = try frames(RenderRequest(path: url.path, output: "unused.png"))
        #expect(map.width >= 1 && map.height >= 1, "\(url.lastPathComponent)")
        #expect(!map.elements.isEmpty, "\(url.lastPathComponent)")
        for element in map.elements {
            #expect(element.frame.width >= 0 && element.frame.height >= 0, "\(url.lastPathComponent): \(element.name)")
        }
    }
}

@Test func aWindowsControlsSitBelowItsTitleBar() throws {
    let document = #"<UIScript version="1"><Window width="200" height="100"/>\#(button)</UIScript>"#
    let map = try frames(RenderRequest(document: document, output: "unused.png"))

    #expect(map.kind == .window)
    #expect(map.width == 200)
    #expect(map.height == 100 + Float(WindowFrame.titleHeight))
    #expect(map.content == FrameMap.Rect(x: 0, y: Float(WindowFrame.titleHeight), width: 200, height: 100))
    #expect(map.elements == [FrameMap.Element(
        name: "ok",
        element: "Button",
        frame: FrameMap.Rect(x: 10, y: 20 + Float(WindowFrame.titleHeight), width: 80, height: 24),
        hidden: false
    )])
}

@Test func aPanelHasNoTitleBar() throws {
    let document = #"<UIScript version="1"><Panel width="300" height="40" edge="bottom"/>\#(button)</UIScript>"#
    let map = try frames(RenderRequest(document: document, output: "unused.png"))

    #expect(map.kind == .panel)
    #expect(map.height == 40)
    #expect(map.elements.first?.frame == FrameMap.Rect(x: 10, y: 20, width: 80, height: 24))
}

@Test func elementsThatStartHiddenAreMarked() throws {
    // UIScript has no way to start hidden; a .uis file's alternate art does.
    let path = examples.appendingPathComponent("lemonadestand.uis").path
    let map = try frames(RenderRequest(path: path, output: "unused.png"))
    #expect(map.elements.contains { $0.name == "StandOpenArt" && $0.hidden })
    #expect(map.elements.contains { !$0.hidden })
}

@Test func attributesHudiniIgnoresAreListed() throws {
    let document = #"<UIScript version="1"><Window width="200" height="100"/><Button name="b" x="0" y="0" width="10" height="10" sparkle="yes"/></UIScript>"#
    let map = try frames(RenderRequest(document: document, output: "unused.png"))
    #expect(map.ignoredAttributes == ["Button.sparkle (line 1)"])
}

@Test func aRequestNamesExactlyOneDocument() {
    #expect(throws: RenderError.invalidRequest("it names no document (document or path)")) {
        try RenderRequest(output: "out.png").resolve()
    }
    #expect(throws: RenderError.invalidRequest("it gives both a document and a path; give one")) {
        try RenderRequest(document: "<UIScript/>", path: "a.uix", output: "out.png").resolve()
    }
    #expect(throws: RenderError.invalidRequest("it doesn't say where to write the picture (output)")) {
        try RenderRequest(document: "<UIScript/>", output: "").resolve()
    }
}

@Test func anOversizedDocumentIsRefusedUnread() {
    let huge = String(repeating: " ", count: RenderRequest.maximumDocumentSize + 1)
    #expect(throws: RenderError.self) {
        try RenderRequest(document: huge, output: "out.png").resolve()
    }
}

@Test func aDocumentHudiniRefusesSaysWhy() {
    #expect(throws: RenderError.self) {
        try RenderRequest(document: "<NotUIScript/>", output: "out.png").resolve()
    }
}

@Test func sizesOutsideWhatCanBeDrawnAreRefused() throws {
    for size in [#"width="0" height="100""#, #"width="9000" height="100""#] {
        let document = #"<UIScript version="1"><Window \#(size)/></UIScript>"#
        #expect(throws: RenderError.self) {
            try frames(RenderRequest(document: document, output: "unused.png"))
        }
    }
}

@Test func theFormatFollowsTheExtensionUnlessGiven() throws {
    let uis = examples.appendingPathComponent("debugbar.uis").path
    let read = try RenderRequest(path: uis, output: "out.png").resolve()
    #expect(!read.script.conversionNotes.isEmpty || !read.script.settings.isEmpty || !read.script.controls.isEmpty)

    // Read as UIScript, a .uis file isn't XML.
    #expect(throws: RenderError.self) {
        try RenderRequest(path: uis, format: .uix, output: "out.png").resolve()
    }
}

@Test func titlesAndImagesDefaultToTheDocumentsFile() throws {
    let path = examples.appendingPathComponent("city/budget.uix").path
    let read = try RenderRequest(path: path, output: "out.png").resolve()
    #expect(read.title == "budget")
    #expect(read.imageDirectory?.lastPathComponent == "city")

    let titled = try RenderRequest(path: path, title: "Budget", directory: "/tmp", output: "out.png").resolve()
    #expect(titled.title == "Budget")
    #expect(titled.imageDirectory?.path == "/tmp")
}

@Test func themesAreNamedAsOnTheCommandLine() throws {
    #expect(ThemeName(rawValue: "depot-light") == .depotLight)
    #expect(ThemeName.depotLight.theme.panel == InterfaceTheme.depotLight.panel)
    let read = try RenderRequest(document: #"<UIScript version="1"><Window width="10" height="10"/></UIScript>"#, output: "o.png").resolve()
    #expect(read.theme.panel == InterfaceTheme.depot.panel)
}

@Test func badRequestLinesAreExplainedInWords() throws {
    func explain(_ json: String) -> String? {
        do {
            _ = try JSONDecoder().decode(RenderRequest.self, from: Data(json.utf8))
            return nil
        } catch let error as DecodingError {
            return RenderRequest.explain(error)
        } catch {
            return "\(error)"
        }
    }
    #expect(explain("not json") == "The request isn't a JSON object on one line.")
    #expect(explain(#"{"path":"a.uix"}"#) == "The request has no output.")
    #expect(explain(#"{"path":"a.uix","output":"o.png","theme":"pink"}"#)
        == "The request's theme isn't one depot-render knows; it takes standard, depot, depot-light.")
}

// MARK: Appearances

private func temporaryFolder() throws -> URL {
    let folder = FileManager.default.temporaryDirectory.appendingPathComponent("depot-render-\(UUID())", isDirectory: true)
    try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
    return folder
}

@Test func aDocumentReadsWithAnAppearanceAsAGameReadsIt() throws {
    let folder = try temporaryFolder()
    defer { try? FileManager.default.removeItem(at: folder) }
    let appearance = folder.appendingPathComponent("appearance.xml")
    try Data("""
        <Appearance version="1">
          <Image name="thumb" path="scroll/thumb.png"/>
          <ScrollBar width="12" thumb="thumb"/>
          <Style name="chatText" textColor="#FFF99D"/>
        </Appearance>
        """.utf8).write(to: appearance)
    let document = """
        <UIScript version="1"><Window width="200" height="100"/>
          <List name="history" x="0" y="0" width="200" height="100" style="chatText"/>
        </UIScript>
        """

    // Without the appearance, the shared style is a name Hudini can't find.
    #expect(throws: RenderError.self) {
        try RenderRequest(document: document, output: "out.png").resolve()
    }
    let read = try RenderRequest(document: document, directory: "/tmp", appearance: appearance.path, output: "out.png").resolve()
    #expect(read.script.styles["chatText"] != nil)
    #expect(read.script.scrollBar?.width == 12)
    #expect(read.appearanceDirectory?.standardizedFileURL == folder.standardizedFileURL)
    #expect(read.imageDirectory?.path == "/tmp")
}

@Test func anAppearanceHudiniRefusesSaysWhy() throws {
    let folder = try temporaryFolder()
    defer { try? FileManager.default.removeItem(at: folder) }
    let appearance = folder.appendingPathComponent("appearance.xml")
    try Data("<Appearance version='1'><Button name='b'/></Appearance>".utf8).write(to: appearance)
    let request = RenderRequest(document: #"<UIScript version="1"><Window width="10" height="10"/></UIScript>"#,
                                appearance: appearance.path, output: "o.png")

    #expect(throws: RenderError.unreadableAppearance(appearance.path, reason: UIScriptError.unexpectedAppearanceElement("Button", line: 1).localizedDescription)) {
        try request.resolve()
    }
    let missing = RenderRequest(document: #"<UIScript version="1"><Window width="10" height="10"/></UIScript>"#,
                                appearance: folder.appendingPathComponent("none.xml").path, output: "o.png")
    #expect(throws: RenderError.self) {
        try missing.resolve()
    }
}

@Test func theAppearanceFieldDecodesFromARequestLine() throws {
    let line = #"{"document": "<UIScript/>", "appearance": "/games/x/appearance.xml", "output": "/tmp/o.png"}"#
    let request = try JSONDecoder().decode(RenderRequest.self, from: Data(line.utf8))
    #expect(request.appearance == "/games/x/appearance.xml")
}

// MARK: Tabs and a sidebar of panes

private let tabbedWindow = #"""
<UIScript version="1"><Window width="200" height="100"/>
<ToolbarItem name="aTab" title="A" tab="a"/><ToolbarItem name="bTab" title="B" tab="b"/>
<Sidebar name="side" width="120"/><Pane name="p" title="P"/><Pane name="q" title="Q"/>
<Tab name="a"><Text name="inA" x="10" y="10" width="50" height="14"/></Tab>
<Tab name="b"><Text name="inB" x="10" y="10" width="50" height="14"/></Tab>
<Button name="inQ" x="10" y="40" width="50" height="20" pane="q"/>
<Button name="always" x="10" y="70" width="50" height="20"/>
</UIScript>
"""#

@Test func aSidebarWidensTheWindowAndTheContentSitsBesideIt() throws {
    let map = try frames(RenderRequest(document: tabbedWindow, output: "unused.png"))
    let title = Float(WindowFrame.titleHeight)

    #expect(map.width == 320)
    #expect(map.sidebar == FrameMap.Rect(x: 0, y: title, width: 120, height: 100))
    #expect(map.content == FrameMap.Rect(x: 120, y: title, width: 200, height: 100))
    #expect(map.elements.first { $0.name == "always" }?.frame == FrameMap.Rect(x: 130, y: 70 + title, width: 50, height: 20))
}

@Test func theFirstTabAndPaneShowUnlessTheRequestSaysOtherwise() throws {
    func hidden(_ map: FrameMap) -> [String] {
        map.elements.filter(\.hidden).map(\.name).sorted()
    }

    let first = try frames(RenderRequest(document: tabbedWindow, output: "unused.png"))
    #expect(first.tab == "a")
    #expect(first.pane == "p")
    #expect(hidden(first) == ["inB", "inQ"])

    let chosen = try frames(RenderRequest(document: tabbedWindow, tab: "b", pane: "q", output: "unused.png"))
    #expect(chosen.tab == "b")
    #expect(chosen.pane == "q")
    #expect(hidden(chosen) == ["inA"])
}

@Test func aTabOrPaneTheDocumentHasntIsRefused() {
    #expect(throws: RenderError.invalidRequest("it shows the tab \"c\", which the document doesn't have")) {
        try RenderRequest(document: tabbedWindow, tab: "c", output: "unused.png").resolve()
    }
    #expect(throws: RenderError.invalidRequest("it shows the pane \"r\", which the document doesn't have")) {
        try RenderRequest(document: tabbedWindow, pane: "r", output: "unused.png").resolve()
    }
}

@Test func aWindowWithoutTabsOrASidebarMapsAsBefore() throws {
    let document = #"<UIScript version="1"><Window width="200" height="100"/>\#(button)</UIScript>"#
    let map = try frames(RenderRequest(document: document, output: "unused.png"))

    #expect(map.sidebar == nil && map.tab == nil && map.pane == nil)
    // Absent fields stay out of the JSON, so a reader from before them sees the map it always did.
    let json = String(decoding: try JSONEncoder().encode(map), as: UTF8.self)
    #expect(!json.contains("sidebar") && !json.contains("\"tab\"") && !json.contains("\"pane\""))
}

@Test func theTabAndPaneFieldsDecodeFromARequestLine() throws {
    let line = #"{"document":"<UIScript/>","tab":"b","pane":"q","output":"o.png"}"#
    let request = try JSONDecoder().decode(RenderRequest.self, from: Data(line.utf8))
    #expect(request.tab == "b" && request.pane == "q")
}
