import Hudini
import Raylib

/// Where Hudini places everything a document draws, so Depot can select and move what it shows.
///
/// Positions are in the picture's pixels, from its top-left corner. A window's picture includes the title bar
/// Hudini's `WindowFrame` draws, so its controls sit below it.
public struct FrameMap: Codable, Sendable, Equatable {
    /// The largest side of a picture, in pixels.
    public static let maximumSide: Float = 8192

    /// What the document describes.
    public enum Kind: String, Codable, Sendable {
        /// A `<Window>`, drawn in a `WindowFrame`.
        case window
        /// A `<Panel>`, pinned to a screen edge, with no title bar.
        case panel
    }

    /// A rectangle in the picture.
    public struct Rect: Codable, Sendable, Equatable {
        public var x: Float
        public var y: Float
        public var width: Float
        public var height: Float

        public init(x: Float, y: Float, width: Float, height: Float) {
            self.x = x
            self.y = y
            self.width = width
            self.height = height
        }
    }

    /// One visual or control.
    public struct Element: Codable, Sendable, Equatable {
        /// Its name in the document.
        public var name: String
        /// Its element in the document, such as `Button` or `Visual`.
        public var element: String
        /// Where Hudini draws it.
        public var frame: Rect
        /// Whether it starts hidden, so the picture doesn't show it.
        public var hidden: Bool
    }

    public var kind: Kind
    /// The picture's size.
    public var width: Float
    public var height: Float
    /// The area the document's own content is drawn in: below the title bar for a window, everything for a panel.
    public var content: Rect
    /// The visuals, then the controls, in the order they're drawn.
    public var elements: [Element]
    /// Images the document names that couldn't be loaded, which Hudini outlines in red.
    public var missingImages: [String]
    /// Strings the document names that depot-render can't look up, which show their table and index.
    public var missingStrings: [String]
    /// Attributes Hudini didn't recognize and ignored, as `element.attribute (line n)`.
    public var ignoredAttributes: [String]
    /// What a `.uis` file held that Hudini couldn't represent, as `line n: message`.
    public var notes: [String]

    /// Works out where `view`, made from `script`, draws everything. This needs no window.
    public init(script: UIScript, view: UIView) throws {
        kind = script.panelEdge == nil ? .window : .panel
        let titleHeight = kind == .window ? Float(WindowFrame.titleHeight) : 0
        width = view.windowSize.x
        height = view.windowSize.y + titleHeight
        guard (1...Self.maximumSide).contains(width), (1...Self.maximumSide).contains(height) else {
            throw RenderError.unsupportedSize(width: width, height: height)
        }
        content = Rect(x: 0, y: titleHeight, width: width, height: view.windowSize.y)

        let origin = Vector2(x: 0, y: titleHeight)
        func element(_ name: String, _ tag: String) -> Element? {
            guard let bounds = view.bounds(named: name, at: origin) else {
                return nil
            }
            return Element(
                name: name,
                element: tag,
                frame: Rect(x: bounds.x, y: bounds.y, width: bounds.width, height: bounds.height),
                hidden: view.isHidden(named: name)
            )
        }
        elements = script.visuals.compactMap { element($0.name, "Visual") }
            + script.controls.compactMap { element($0.name, Self.tag(of: $0.type)) }
        missingImages = view.missingImages
        missingStrings = view.missingStrings
        ignoredAttributes = script.ignoredAttributes.map { "\($0.element).\($0.attribute) (line \($0.line))" }
        notes = script.conversionNotes.map { "line \($0.line): \($0.message)" }
    }

    /// The element a control is written as.
    static func tag(of type: UIScript.ControlType) -> String {
        switch type {
        case .text: "Text"
        case .formattedText: "FormattedText"
        case .button: "Button"
        case .textField: "TextField"
        case .textArea: "TextArea"
        case .list: "List"
        case .slider: "Slider"
        case .progressBar: "ProgressBar"
        case .region: "Region"
        }
    }
}
