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
        let view = UIView(script: resolved.script, theme: resolved.theme, loadImage: { resource in
            guard let texture = Self.loadTexture(resource, in: resolved.imageDirectory) else {
                return nil
            }
            textures.append(texture)
            return texture
        })
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
            DrawRectangleRec(bounds, resolved.theme.panel)
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

    // An image path is the host's to interpret; depot-render reads it as a file, relative to the document's folder.
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
