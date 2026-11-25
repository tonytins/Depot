# Project Guidelines

## Project Structure

Depot is a designer for the UIScript documents [Hudini](https://github.com/tonytins/Hudini)'s games read, spun off
from Roundhouse, VentureOS's IDE. [docs/design.md](docs/design.md) explains how it's built and why; read it before
changing how Depot and `depot-render` talk, or what Depot holds documents to.

- `src/Depot` is the app, in C# with Avalonia (.NET 10), for Linux, macOS, and Windows. `MainWindow` holds the text
  editor, the canvas, the outline, and the inspector; `Designer/` the designer's logic; `Rendering/` the conversation
  with `depot-render`; `Documents/` opening and saving files.
- `src/Depot.Interface` is Depot's own copy of VentureOS's `Venture.OS.Interface`: the UIScript reader
  (`UIScript.cs`), `UIDocument`, which edits a document's text in place, `UIScriptSchema`, and `HudiniSubset`, which
  narrows them to what Hudini reads. It references nothing but the BCL and CSTNet.
- `depot-render` is a Swift package over Hudini and raylib-swift that draws a document and reports where everything
  went. Its command line and protocol are in [docs/depot-render.md](docs/depot-render.md).
- `tests/Depot.Interface.Tests` covers `Depot.Interface`, `tests/Depot.Tests` covers the app, driving its window
  off-screen with `Avalonia.Headless`, and `depot-render/Tests` covers `depot-render`. `tests/Fixtures` holds
  Hudini's example documents, which every suite reads.

Never commit build output (`bin/`, `obj/`, `.build/`, `TestResults/`).

## Build and Test

```sh
dotnet build Depot.slnx                              # no warnings; they're errors
dotnet test --project tests/Depot.Interface.Tests
dotnet test --project tests/Depot.Tests
(cd depot-render && swift build && swift test)
dotnet run --project src/Depot -- document.uix       # the app, drawing with the depot-render build beside it
```

- `depot-render` and one of Depot's tests need a display. Without a desktop, start Xvfb on a free display, such as
  `:99`, and set `DISPLAY` for the run; that test is skipped without one.
- Building `depot-render` needs raylib's native development libraries (X11 and GL headers on Linux).
- Tests don't prove the app works. When a change affects what Depot shows or how it responds, run it and look:
  on Xvfb, `import -window root` captures the screen and `xdotool` clicks, drags, and types. Say what you checked
  and what you couldn't.

## Rules

### Never lose work

- Saving writes the editor's text exactly, through a temporary file, so a failed save leaves the file as it was.
  Keep it that way.
- Every change to a document, typed or designed, is a change to the editor's text, so Undo takes it back. Designer
  edits go through `UIDocument` and `MainWindow.Apply`, never around them.
- Unsaved changes are asked about before closing or opening another document.
- A failure is reported in the status bar, in the HIG's plain voice, and Depot carries on. Don't swallow one, or
  show success for something that didn't happen.

### Stay faithful to Hudini

- The canvas is drawn by Hudini, through `depot-render`. Don't draw an approximation in C#.
- Depot holds documents to the spec's Hudini subset (`HudiniSubset`), not to Hudini's current release. A designer
  edit is refused when Hudini wouldn't read the result; the text can say anything.
- `.uis` files are shown, never written.
- Where the selection or an element is depends on `depot-render`'s frame map, not on reading `x` and `y` from the
  document.

### Depot.Interface

- It's Depot's copy and can change freely, but the UIScript spec still governs the format. A change to what it
  reads must agree with [the spec](https://github.com/tonytins/VentureOS/blob/main/docs/uiscript/README.md) and its
  Hudini section; raise a disagreement rather than diverge quietly.
- Change `UIScriptSchema` with the reader, and `HudiniSubset` with the spec's Hudini section.

### depot-render

- Its protocol is for other programs too. Keep additions optional and old requests working; document every field
  in [docs/depot-render.md](docs/depot-render.md).
- Validate requests before reading or drawing: sizes, lengths, and that exactly one document is named.
- Messages go to standard error; standard output carries only the frame map or answers.

### Code

- C#: nullable on, warnings as errors, `///` comments that explain why. Swift: Swift 6 language mode, and the
  package's existing style.
- Keep `MainWindow` the glue. Logic that doesn't need a window goes in `Designer/`, `Rendering/`, `Documents/`, or
  `Depot.Interface`, with tests.
- Tests that touch Avalonia run inside `TestApp.Run`, on Avalonia's thread, including ones that only call a static
  method on a window class.
- A new test should fail without the change it covers; check by breaking the code on purpose, then restore it by
  editing, not with `git checkout`.

### Dependencies

Approved: Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.AvaloniaEdit and AvaloniaEdit.TextMate, at
Roundhouse's versions; CSTNet; xunit.v3, Avalonia.Headless, and Avalonia.Skia for tests; and for `depot-render`,
Hudini, raylib-swift, and swift-argument-parser. The owner's own packages and Apple's are approved too. Anything else
needs the owner's approval. Bump versions in a change of their own.

## Keeping Docs Current

Describe only what works today.

- [README.md](README.md) when using, building, or the layout changes.
- [docs/design.md](docs/design.md) when a decision is made or reversed, with a row in its table.
- [docs/depot-render.md](docs/depot-render.md) when the command line, the protocol, or the frame map changes.
- [docs/hig.md](docs/hig.md) when Depot's look changes, together with Hudini's `InterfaceTheme.depot` and Depot's
  `App.axaml`.
- This file when the layout or the rules change.

State breaking changes, dependency changes, and anything you couldn't verify in the commit message. If you notice a
risk outside your task, report it rather than fixing it quietly.

See [REVIEW.md](REVIEW.md) for the review checklist.

## Personal Configuration

Keep personal preferences and machine-specific guidance in git-ignored `*.local` files or directories, such as
`AGENTS.local.md`, rather than here.
