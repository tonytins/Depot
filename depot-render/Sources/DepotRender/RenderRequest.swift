import Foundation
import Hudini

/// What to draw and where to put the picture: one line of `depot-render --serve`'s input, or its command line.
///
/// A request names its document by `path`, or carries its text in `document`, which is how Depot draws text that
/// hasn't been saved. Exactly one of the two is given.
public struct RenderRequest: Codable, Sendable, Equatable {
    /// The document's text, for a document that isn't on disk, or isn't saved.
    public var document: String?
    /// A document on disk.
    public var path: String?
    /// How the document is written. Without it, a path ending in `.uis` is a `.uis` file, and anything else UIScript.
    public var format: DocumentFormat?
    /// The look to draw in, `depot` unless given.
    public var theme: ThemeName?
    /// The title a window's bar shows, the document's file name unless given.
    public var title: String?
    /// The folder the document's image paths are relative to, the document's own folder unless given.
    public var directory: String?
    /// A Hudini appearance file the document is read with, as a game reads its windows. Its image paths are relative
    /// to its own folder. A `.uis` file reads without one.
    public var appearance: String?
    /// The tab to show, by name, the document's first unless given. Its toolbar item shows held down, and only its
    /// controls and visuals, and those in no tab, are drawn.
    public var tab: String?
    /// The pane to show, by name, the document's first unless given, as the tab is.
    public var pane: String?
    /// Where to write the PNG.
    public var output: String

    public init(
        document: String? = nil,
        path: String? = nil,
        format: DocumentFormat? = nil,
        theme: ThemeName? = nil,
        title: String? = nil,
        directory: String? = nil,
        appearance: String? = nil,
        tab: String? = nil,
        pane: String? = nil,
        output: String
    ) {
        self.document = document
        self.path = path
        self.format = format
        self.theme = theme
        self.title = title
        self.directory = directory
        self.appearance = appearance
        self.tab = tab
        self.pane = pane
        self.output = output
    }

    /// The largest document drawn, in bytes, far beyond any real interface, so a runaway request is refused rather
    /// than read.
    public static let maximumDocumentSize = 4 * 1024 * 1024

    /// The document read, checked, and ready to draw.
    public struct Resolved: Sendable {
        public let script: UIScript
        public let theme: InterfaceTheme
        public let title: String
        public let imageDirectory: URL?
        /// The appearance's folder, where its images are looked for when the document's folder hasn't them, and
        /// its fonts are.
        public let appearanceDirectory: URL?
        /// The appearance the document was read with, whose fonts it draws in, or `nil` for none or a `.uis` file.
        public let appearance: Appearance?
        /// The toolbar item of the tab to show, or `nil` for the document's first.
        public let tabItem: String?
        /// The pane to show, or `nil` for the document's first.
        public let pane: String?
        public let output: URL

        /// Shows the requested tab and pane in `view`, which is made from `script`.
        public func show(in view: UIView) {
            if let tabItem {
                view.setSelected(true, named: tabItem)
            }
            if let pane {
                view.setSelected(true, named: pane)
            }
        }
    }

    /// Reads the document the request names, refusing a request that doesn't make sense.
    public func resolve() throws -> Resolved {
        guard !output.isEmpty else {
            throw RenderError.invalidRequest("it doesn't say where to write the picture (output)")
        }

        let data: Data
        let pathURL = path.map { URL(fileURLWithPath: $0) }
        switch (document, pathURL) {
        case let (text?, nil):
            data = Data(text.utf8)
        case let (nil, url?):
            do {
                data = try Data(contentsOf: url)
            } catch {
                throw RenderError.unreadable(url.path, reason: error.localizedDescription)
            }
        case (nil, nil):
            throw RenderError.invalidRequest("it names no document (document or path)")
        case (_?, _?):
            throw RenderError.invalidRequest("it gives both a document and a path; give one")
        }
        guard data.count <= Self.maximumDocumentSize else {
            throw RenderError.invalidRequest(
                "its document is \(data.count) bytes, more than the \(Self.maximumDocumentSize) depot-render reads"
            )
        }

        let format = format ?? (pathURL?.pathExtension.lowercased() == "uis" ? .uis : .uix)
        let appearanceURL = appearance.map { URL(fileURLWithPath: $0) }
        let shared: Appearance?
        do {
            shared = try appearanceURL.map { try Appearance(contentsOf: $0) }
        } catch {
            throw RenderError.unreadableAppearance(appearanceURL?.path ?? "", reason: error.localizedDescription)
        }
        let script: UIScript
        do {
            script = try format == .uis ? UIScript(uisData: data) : UIScript(data: data, appearance: shared)
        } catch {
            throw RenderError.unreadableDocument(error.localizedDescription)
        }

        // A tab is shown by selecting the toolbar item that names it, as a click on it would.
        var tabItem: String?
        if let tab {
            guard let item = script.toolbarItems.first(where: { $0.tab == tab }) else {
                throw RenderError.invalidRequest("it shows the tab \"\(tab)\", which the document doesn't have")
            }
            tabItem = item.name
        }
        if let pane, script.sidebar?.panes.contains(where: { $0.name == pane }) != true {
            throw RenderError.invalidRequest("it shows the pane \"\(pane)\", which the document doesn't have")
        }

        return Resolved(
            script: script,
            theme: (theme ?? .depot).theme,
            title: title ?? pathURL?.deletingPathExtension().lastPathComponent ?? "",
            imageDirectory: directory.map { URL(fileURLWithPath: $0, isDirectory: true) }
                ?? pathURL?.deletingLastPathComponent(),
            appearanceDirectory: appearanceURL?.deletingLastPathComponent(),
            appearance: format == .uis ? nil : shared,
            tabItem: tabItem,
            pane: pane,
            output: URL(fileURLWithPath: output)
        )
    }
}

/// How a document is written.
public enum DocumentFormat: String, Codable, Sendable, CaseIterable {
    /// UIScript, the XML format VentureOS keeps the spec of.
    case uix
    /// Maxis' interface script, which Hudini reads and Depot only shows.
    case uis
}

/// A look a document can be drawn in.
public enum ThemeName: String, Codable, Sendable, CaseIterable {
    /// Hudini's standard look, what a game gets when it doesn't choose one.
    case standard
    /// The look Depot gives new documents.
    case depot
    /// The light twin of `depot`.
    case depotLight = "depot-light"

    /// Hudini's theme of that name.
    public var theme: InterfaceTheme {
        switch self {
        case .standard: .standard
        case .depot: .depot
        case .depotLight: .depotLight
        }
    }
}

/// Why a document couldn't be drawn.
public enum RenderError: Error, LocalizedError, Equatable {
    /// The request doesn't make sense.
    case invalidRequest(String)
    /// The document's file couldn't be read.
    case unreadable(String, reason: String)
    /// Hudini refuses the document.
    case unreadableDocument(String)
    /// The appearance couldn't be read, or Hudini refuses it.
    case unreadableAppearance(String, reason: String)
    /// The document's size can't be drawn.
    case unsupportedSize(width: Float, height: Float)
    /// raylib couldn't open its window, usually because there's no display.
    case noDisplay
    /// raylib couldn't make or save the picture.
    case drawingFailed(String)

    public var errorDescription: String? {
        switch self {
        case let .invalidRequest(reason):
            "The request can't be drawn: \(reason)."
        case let .unreadable(path, reason):
            "Couldn't read \(path): \(reason)"
        case let .unreadableDocument(reason):
            "Hudini can't read the document: \(reason)"
        case let .unreadableAppearance(path, reason):
            "Hudini can't read the appearance \(path): \(reason)"
        case let .unsupportedSize(width, height):
            "The document is \(width) by \(height) pixels; depot-render draws from 1 to \(FrameMap.maximumSide) on each side."
        case .noDisplay:
            "Couldn't open a window to draw in. depot-render needs a display, such as an Xvfb one, even though its window stays hidden."
        case let .drawingFailed(reason):
            "Couldn't draw the document: \(reason)."
        }
    }
}

extension RenderRequest {
    /// Says what's wrong with a request line that didn't decode, in words rather than Codable's.
    public static func explain(_ error: DecodingError) -> String {
        func field(_ path: [any CodingKey]) -> String {
            path.map(\.stringValue).joined(separator: ".")
        }
        switch error {
        case let .keyNotFound(key, _):
            return "The request has no \(key.stringValue)."
        case let .typeMismatch(_, context), let .valueNotFound(_, context):
            return "The request's \(field(context.codingPath)) isn't the right kind of value."
        case let .dataCorrupted(context) where !context.codingPath.isEmpty:
            let choices = field(context.codingPath) == "theme"
                ? ThemeName.allCases.map(\.rawValue)
                : DocumentFormat.allCases.map(\.rawValue)
            return "The request's \(field(context.codingPath)) isn't one depot-render knows; it takes "
                + choices.joined(separator: ", ") + "."
        case .dataCorrupted:
            return "The request isn't a JSON object on one line."
        @unknown default:
            return "The request isn't one depot-render understands."
        }
    }
}
