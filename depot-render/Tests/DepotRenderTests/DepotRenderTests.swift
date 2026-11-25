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
    return try FrameMap(script: resolved.script, view: UIView(script: resolved.script, theme: resolved.theme))
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
