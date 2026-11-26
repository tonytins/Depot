import ArgumentParser
import DepotRender
import Foundation

@main
struct DepotRenderCommand: ParsableCommand {
    static let configuration = CommandConfiguration(
        commandName: "depot-render",
        abstract: "Draws a UIScript or .uis document with Hudini, for Depot's canvas.",
        discussion: """
            Given a document, it writes the picture to --output and prints where Hudini placed everything, as \
            JSON. With --serve, it reads one JSON request per line from standard input and answers each with one \
            line, {"frames": {...}} or {"error": "..."}, keeping its window open between them.

            It needs a display, such as an Xvfb one, even though its window stays hidden.
            """
    )

    @Argument(help: "The document to draw.")
    var document: String?

    @Option(name: .shortAndLong, help: "Where to write the PNG.")
    var output: String?

    @Option(help: "The look: standard, depot, or depot-light.")
    var theme: ThemeName = .depot

    @Option(help: "The title a window's bar shows. Defaults to the document's file name.")
    var title: String?

    @Option(help: "How the document is written: uix or uis. Defaults to its extension.")
    var format: DocumentFormat?

    @Option(help: "A Hudini appearance file to read the document with, as a game reads its windows.")
    var appearance: String?

    @Flag(help: "Read requests from standard input, one JSON object per line.")
    var serve = false

    func validate() throws {
        if serve {
            guard document == nil, output == nil else {
                throw ValidationError("--serve takes its documents from standard input, not the command line.")
            }
        } else {
            guard document != nil else {
                throw ValidationError("Name a document to draw, or use --serve.")
            }
            guard output != nil else {
                throw ValidationError("Say where to write the picture with --output.")
            }
        }
    }

    func run() throws {
        try MainActor.assumeIsolated {
            let renderer: Renderer
            do {
                renderer = try Renderer()
            } catch {
                throw Failure(message: error.localizedDescription)
            }
            defer { renderer.close() }

            if serve {
                Self.serve(with: renderer)
                return
            }
            let request = RenderRequest(
                path: document,
                format: format,
                theme: theme,
                title: title,
                appearance: appearance,
                output: output ?? ""
            )
            do {
                let map = try renderer.render(request)
                let encoder = JSONEncoder()
                encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
                FileHandle.standardOutput.write(try encoder.encode(map) + Data("\n".utf8))
            } catch {
                throw Failure(message: error.localizedDescription)
            }
        }
    }

    /// Answers requests until standard input ends. A request that fails is answered with its error, and the next
    /// one is read as usual.
    @MainActor
    static func serve(with renderer: Renderer) {
        let decoder = JSONDecoder()
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.sortedKeys]
        while let line = readLine(strippingNewline: true) {
            guard !line.allSatisfy(\.isWhitespace) else {
                continue
            }
            var reply = Reply()
            do {
                let request = try decoder.decode(RenderRequest.self, from: Data(line.utf8))
                reply.frames = try renderer.render(request)
            } catch let error as DecodingError {
                reply.error = RenderRequest.explain(error)
            } catch {
                reply.error = error.localizedDescription
            }
            // Encoding these plain values can't fail; if it ever did, the caller still gets an answer for its line.
            let data = (try? encoder.encode(reply)) ?? Data(#"{"error":"depot-render couldn't write its answer."}"#.utf8)
            FileHandle.standardOutput.write(data + Data("\n".utf8))
        }
    }

    /// One answer in --serve: the frame map, or why there isn't one.
    struct Reply: Encodable {
        var frames: FrameMap?
        var error: String?
    }
}

/// A failure reported on standard error, with exit status 1.
struct Failure: Error, CustomStringConvertible {
    let message: String
    var description: String { message }
}

extension ThemeName: ExpressibleByArgument {}
extension DocumentFormat: ExpressibleByArgument {}
