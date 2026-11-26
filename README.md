# Depot

Depot (a working title) is a designer for the [UIScript](https://github.com/tonytins/VentureOS/blob/main/docs/uiscript/README.md)
documents that [Hudini](https://github.com/tonytins/Hudini)'s games read. It's a spin-off of Roundhouse,
VentureOS's IDE, and draws its canvas with Hudini itself, so what you design is what the game draws.

Depot shows a document's text beside the canvas, and redraws the canvas as you type, in the look you choose:
Depot's own, its light twin, or Hudini's standard one. You design on the canvas, and every change is a change to
the text, which you can always edit by hand. It edits and saves UIScript (`.uix`) documents, and shows Maxis
`.uis` files without changing them.

Depot holds documents to the part of UIScript Hudini reads: no menus, sidebars, panes, shelves, grids, or status
items. A document that has one opens as text only, saying why, until it's taken out.

## Using Depot

```sh
dotnet run --project src/Depot -- path/to/document.uix
```

Open (Ctrl+O) opens another document, and Save (Ctrl+S) saves the text exactly as it's written, even text Hudini
won't read, which the status bar then warns of. The status bar also says what Hudini ignores or can't find, and
why a document can't be drawn; while it can't, the last picture stays, faded.

On the canvas:

- Click to select a control, a picture, or the window; the outline selects anything, such as a style or a
  toolbar item, which Hudini doesn't report a frame for.
- Drag to move, or drag a handle to resize. The outline follows the pointer at once, and Hudini's picture as soon
  as it's drawn. The window and panel resize by their right and bottom edges.
- The arrow keys move by a pixel, with Shift by 8. Delete deletes, Ctrl+D duplicates, and Ctrl+] and Ctrl+[ move
  in front of or behind the next control.
- Add adds any control Hudini draws, a picture, a toolbar item, or a title field.
- The inspector edits the selection's attributes; an empty field removes one. Attributes Hudini doesn't read are
  kept under Other.
- Undo (Ctrl+Z) and Redo (Ctrl+Shift+Z) take back any change, typed or designed; a whole drag is one change.

A game can read its windows with a Hudini [appearance](https://github.com/tonytins/Hudini/blob/main/docs/UISCRIPT.md#appearances):
one file holding the images, styles, and scroll bar art they share. A window that names a shared style or image
doesn't read without it, so Depot can read and draw documents with one too. Choose it from the Appearance button
beside Look, or start Depot with it:

```sh
dotnet run --project src/Depot -- --appearance path/to/appearance.xml path/to/document.uix
```

Depot then designs such a window, and draws it with the appearance's scroll bar art, as the game does. A `.uis` file
is read without one, as Hudini reads it.

Depot draws with `depot-render`, which it looks for in this order: the path in `DEPOT_RENDER`, beside Depot itself,
the newer of `depot-render/.build/release` and `depot-render/.build/debug` in the checkout Depot runs from, and the
`PATH`. Without it, Depot edits text but can't draw.

## Layout

- `src/Depot`: the app, in C# with Avalonia, for Linux, macOS, and Windows.
- `src/Depot.Interface`: Depot's own copy of the UIScript reader and the document model it edits through, taken
  from VentureOS's `Venture.OS.Interface`, held to Hudini's subset (`HudiniSubset`).
- `depot-render`: a Swift package that draws a document with Hudini, for the canvas.
- `tests`: the tests for both .NET projects, and `tests/Fixtures`, Hudini's example documents, which every suite
  reads; `depot-render`'s tests are in its package.

## depot-render

`depot-render` draws a UIScript or `.uis` document with Hudini, writes the picture as a PNG, and prints where Hudini
placed each visual and control as JSON. Depot keeps it running with `--serve`, and other programs can use it too.

```sh
depot-render budget.uix --output budget.png                       # in the depot look
depot-render budget.uix --output budget.png --theme depot-light   # or standard, Hudini's own
```

Its options, protocol, and limits are in [its reference](docs/depot-render.md).

## Building

Depot needs the .NET 10 SDK; `depot-render` needs Swift 6.3 and raylib's native development libraries, and builds
against Hudini's `main` branch.

```sh
dotnet build Depot.slnx
dotnet test --project tests/Depot.Interface.Tests
dotnet test --project tests/Depot.Tests
(cd depot-render && swift build && swift test)
```

Both test suites read Hudini's example documents from `tests/Fixtures`. One of Depot's tests draws with
`depot-render` and is skipped when it isn't built or there's no display; run it on a virtual one with
`DISPLAY=:99 dotnet test --project tests/Depot.Tests` while Xvfb serves `:99`.

## Documentation

- [docs/design.md](docs/design.md): how Depot is built, what it leaves out, and the decisions behind it.
- [docs/depot-render.md](docs/depot-render.md): `depot-render`'s command line, protocol, and frame map.
- [docs/hig.md](docs/hig.md): Depot's look, and where it differs from the VentureOS HIG.
- [AGENTS.md](AGENTS.md) and [REVIEW.md](REVIEW.md): how to work on Depot, and how changes are reviewed.

## License

Depot is licensed under the [GNU General Public License, version 3](LICENSE).
