# depot-render

`depot-render` draws a UIScript (`.uix`) or Maxis `.uis` document with [Hudini](https://github.com/tonytins/Hudini),
writes the picture as a PNG, and reports where Hudini placed each visual and control as JSON. Depot draws its canvas
with it, and anything else can too: a script that keeps screenshots of a game's interfaces, a test that checks
nothing overlaps, or another editor.

It needs a display, even though its window stays hidden, since raylib needs one to draw. Without a desktop, run it
on a virtual display such as Xvfb.

## Drawing one document

```sh
depot-render <document> --output <file.png> [--theme depot|depot-light|standard] [--title <text>] [--format uix|uis] [--appearance <file>]
```

| Option | What it does |
| --- | --- |
| `<document>` | The document to draw. |
| `-o`, `--output` | Where to write the PNG. Required. |
| `--theme` | The look: `depot` (the default), `depot-light`, or `standard`, Hudini's own. These are Hudini's `InterfaceTheme.depot`, `.depotLight`, and `.standard`. |
| `--title` | The title a window's bar shows. Defaults to the document's file name. |
| `--format` | How the document is written. Defaults to `uis` for a `.uis` file, and `uix` for anything else. |
| `--appearance` | A Hudini [appearance](https://github.com/tonytins/Hudini/blob/main/docs/UISCRIPT.md#appearances) the document is read with, as a game reads its windows: its images and styles, which the document may name without declaring, and its scroll bar art. A `.uis` file is read without it. |

It prints the [frame map](#the-frame-map) on standard output, and anything wrong on standard error.

| Exit status | Meaning |
| --- | --- |
| 0 | Drawn. |
| 1 | It couldn't draw: the document or the appearance couldn't be read or Hudini refuses it, its size can't be drawn, the PNG couldn't be written, or there's no display. |
| 64 | The command line doesn't make sense, such as no document or no `--output`. |

## Serving requests

```sh
depot-render --serve
```

With `--serve`, it keeps its window open and reads one JSON request per line from standard input, answering each
with one line on standard output, so a caller can draw as often as someone types without starting raylib each
time. It runs until standard input ends.

A request is an object with these fields:

| Field | |
| --- | --- |
| `document` | The document's text, for one that isn't saved. |
| `path` | A document on disk. Give `document` or `path`, not both. |
| `format` | `uix` or `uis`. Defaults as on the command line. |
| `theme` | `depot`, `depot-light`, or `standard`. Defaults to `depot`. |
| `title` | The title a window's bar shows. Defaults to the file name, or nothing. |
| `directory` | The folder image paths start from. Defaults to the document's folder. |
| `appearance` | A Hudini appearance file the document is read with, as `--appearance` is. |
| `output` | Where to write the PNG. Required. |

```json
{"document": "<UIScript version=\"1\"><Window width=\"200\" height=\"100\"/></UIScript>", "theme": "depot", "output": "/tmp/canvas.png"}
```

Each answer is `{"frames": {...}}`, the frame map, or `{"error": "..."}`, a sentence saying what's wrong. A request
that fails is answered with its error, and the next line is read as usual. Blank lines are skipped.

## The frame map

Positions are in the picture's pixels, from its top-left corner. A window's picture includes the 30-pixel title bar
Hudini's `WindowFrame` draws, so its controls sit below it.

```json
{
  "kind": "window",
  "width": 200,
  "height": 130,
  "content": {"x": 0, "y": 30, "width": 200, "height": 100},
  "elements": [
    {"name": "ok", "element": "Button", "frame": {"x": 10, "y": 40, "width": 80, "height": 24}, "hidden": false}
  ],
  "missingImages": [],
  "missingStrings": [],
  "ignoredAttributes": ["Button.sparkle (line 3)"],
  "notes": []
}
```

| Field | |
| --- | --- |
| `kind` | `window` for a `<Window>`, drawn in a `WindowFrame`; `panel` for a `<Panel>`, drawn on a plain fill. |
| `width`, `height` | The picture's size. |
| `content` | Where the document's own content is: below the title bar, or all of a panel. |
| `elements` | The visuals, then the controls, in the order Hudini draws them, so later ones are in front. `element` is the element's name in the document, such as `Button` or `Visual`; `hidden` is set for one that starts hidden, which only a `.uis` file has, and the picture doesn't show. |
| `missingImages` | Images the document names that couldn't be loaded, which Hudini outlines in red. |
| `missingStrings` | Strings the document names, which `depot-render` can't look up yet. |
| `ignoredAttributes` | Attributes Hudini didn't recognize, as `element.attribute (line n)`. |
| `notes` | What a `.uis` file held that Hudini couldn't represent, as `line n: message`. |

Toolbar items and the title field aren't in `elements`, since Hudini doesn't say where it draws them.

## Limits

- A document is read only up to 4 MiB, and a picture is from 1 to 8192 pixels on each side.
- Text is drawn in raylib's default font; a game loads its own.
- String references show their table and index, as Hudini shows a string its host can't find.
- Image paths are read as files relative to `directory`, then to the appearance's folder, where an appearance's own
  images are. Images that aren't found are outlined in red.
- Rounded corners are drawn without antialiasing, which a game can turn on.
