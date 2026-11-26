import Foundation
import Hudini
import Raylib

/// Draws documents with Hudini into PNGs, in a hidden raylib window that stays open between documents, so fonts
/// and the GL context are made once.
///
/// raylib has one window per process, so make one renderer, on the main thread, and `close()` it when done.
@MainActor
public final class Renderer {
    private var isOpen = false

    /// Opens the hidden window, which needs a display even though nothing shows.
    public init() throws {
        SetTraceLogLevel(.error)
        SetConfigFlags([.windowHidden])
        InitWindow(1, 1, "depot-render")
        guard IsWindowReady() else {
            throw RenderError.noDisplay
        }
        isOpen = true
        // Out of the way, so no title-bar button or control is drawn hovered.
        SetMousePosition(-10_000, -10_000)
    }

    /// Closes the window. The renderer draws nothing after.
    public func close() {
        guard isOpen else {
            return
        }
        CloseWindow()
        isOpen = false
    }

    /// Draws the document `request` names into its output, and returns where everything went.
    public func render(_ request: RenderRequest) throws -> FrameMap {
        guard isOpen else {
            throw RenderError.drawingFailed("the renderer is closed")
        }
        let resolved = try request.resolve()

        var textures: [Texture2D] = []
        defer { textures.forEach(UnloadTexture) }
        // The appearance's fonts, loaded once for the theme and the view and unloaded after drawing. They're read
        // again for every document, so a font file changed on disk shows on the next draw.
        var fonts: [UIScript.FontResource: Font] = [:]
        defer { fonts.values.forEach(UnloadFont) }
        let loadFont = { (resource: UIScript.FontResource) -> Font? in
            if let font = fonts[resource] {
                return font
            }
            guard let font = Self.loadFont(resource, in: resolved.appearanceDirectory) else {
                return nil
            }
            fonts[resource] = font
            return font
        }
        let theme = resolved.appearance.map { resolved.theme.withFonts(from: $0, loadFont: loadFont) } ?? resolved.theme
        let view = UIView(
            script: resolved.script,
            theme: theme,
            loadImage: { resource in
                // An inherited image is relative to the appearance's folder, a window's own to the document's.
                guard let texture = Self.loadTexture(resource, in: resolved.imageDirectory)
                    ?? Self.loadTexture(resource, in: resolved.appearanceDirectory) else {
                    return nil
                }
                textures.append(texture)
                return texture
            },
            loadFont: loadFont
        )
        resolved.show(in: view)
        let map = try FrameMap(script: resolved.script, view: view)

        let target = LoadRenderTexture(Int(map.width), Int(map.height))
        guard IsRenderTextureValid(target) else {
            throw RenderError.drawingFailed("raylib couldn't make a \(Int(map.width)) by \(Int(map.height)) picture")
        }
        defer { UnloadRenderTexture(target) }

        BeginTextureMode(target)
        ClearBackground(Color(r: 0, g: 0, b: 0, a: 0))
        let bounds = Rectangle(x: 0, y: 0, width: map.width, height: map.height)
        switch map.kind {
        case .window:
            WindowFrame(view: view, title: resolved.title).draw(in: bounds)
        case .panel:
            // The game draws a panel's background; this is the plain one Hudini's demos use.
            DrawRectangleRec(bounds, theme.panel)
            view.draw(at: Vector2(x: 0, y: 0))
        }
        EndTextureMode()

        var image = LoadImageFromTexture(target.texture)
        defer { UnloadImage(image) }
        // A render texture is stored bottom row first.
        ImageFlipVertical(&image)
        guard ExportImage(image, resolved.output.path) else {
            throw RenderError.drawingFailed("raylib couldn't write \(resolved.output.path)")
        }
        return map
    }

    // A font path is the host's to interpret; depot-render reads it as a file, relative to the appearance's folder.
    // Mipmaps keep thin strokes when text is drawn smaller than the font is loaded, as Hudini's demo does.
    private static func loadFont(_ resource: UIScript.FontResource, in directory: URL?) -> Font? {
        let url = resource.path.hasPrefix("/") ? URL(fileURLWithPath: resource.path)
            : directory?.appendingPathComponent(resource.path)
        guard let url, FileManager.default.isReadableFile(atPath: url.path) else {
            return nil
        }
        var font = LoadFontEx(url.path, Int32(resource.size), nil, 0)
        guard IsFontValid(font) else {
            return nil
        }
        GenTextureMipmaps(&font.texture)
        SetTextureFilter(font.texture, .trilinear)
        return font
    }

    // An image path is the host's to interpret; depot-render reads it as a file, relative to a folder.
    private static func loadTexture(_ resource: UIScript.ImageResource, in directory: URL?) -> Texture2D? {
        guard let path = resource.path, !path.isEmpty else {
            return nil
        }
        let url = path.hasPrefix("/") ? URL(fileURLWithPath: path) : directory?.appendingPathComponent(path)
        guard let url, FileManager.default.isReadableFile(atPath: url.path) else {
            return nil
        }
        let texture = LoadTexture(url.path)
        guard IsTextureValid(texture) else {
            return nil
        }
        return texture
    }
}
