# Depot

Depot is a designer for the [UIScript](https://github.com/tonytins/VentureOS/blob/main/docs/uiscript/README.md)
documents that [Hudini](https://github.com/tonytins/Hudini)'s games read. It's a spin-off of Roundhouse,
VentureOS's IDE, and draws its canvas with Hudini itself, so what you design is what the game draws.

Depot is at its very start: the window is empty, but `depot-render` draws documents.

## Layout

- `src/Depot`: the app, in C# with Avalonia, for Linux, macOS, and Windows.
- `src/Depot.Interface`: Depot's own copy of the UIScript reader and the document model it edits through, taken
  from VentureOS's `Venture.OS.Interface`.
- `depot-render`: a Swift package that draws a document with Hudini, for the canvas.

## depot-render

`depot-render` draws a UIScript (`.uix`) or Maxis `.uis` document with Hudini, writes the picture as a PNG, and
prints where Hudini placed each visual and control as JSON, so an editor can select what the picture shows.

```sh
depot-render budget.uix --output budget.png                       # in the depot look
depot-render budget.uix --output budget.png --theme depot-light   # or standard, Hudini's own
```

With `--serve`, it keeps its window open and reads one JSON request per line from standard input, answering each
with one line, `{"frames": {...}}` or `{"error": "..."}`. A request names a document by `path`, or carries its
text in `document`:

```json
{"document": "<UIScript version=\"1\">...</UIScript>", "theme": "depot", "output": "/tmp/canvas.png"}
```

It also takes `format` (`uix` or `uis`), `title` (for a window's bar), and `directory` (where image paths start).

What it can't show yet:

- Text is in raylib's default font; a game loads its own.
- String references show their table and index, as Hudini shows a string its host can't find.
- Image paths are read as files relative to the document. Images that aren't found are outlined in red.
- It needs a display, even though its window stays hidden. Without a desktop, use Xvfb.
- Rounded corners are drawn without antialiasing, which a game can turn on.

## Building

Depot needs the .NET 10 SDK; `depot-render` needs Swift 6.3 and a Hudini checkout at `../../../Hudini`, beside
VentureOS, along with raylib's native development libraries.

```sh
dotnet build Depot.slnx
dotnet run --project src/Depot
(cd depot-render && swift build && swift test)
```

`depot-render`'s tests read Hudini's demo documents from the same checkout.

## License

Depot is licensed under the [GNU General Public License, version 3](LICENSE).
