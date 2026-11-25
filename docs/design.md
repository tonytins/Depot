# Depot: design

> **Status:** working title, built 2026-11-25 · **Spun off from:** [Roundhouse](https://github.com/tonytins/VentureOS/blob/main/docs/design/2026-11-18-roundhouse-design.md), VentureOS's IDE

Depot is a designer for the UIScript documents [Hudini](https://github.com/tonytins/Hudini)'s games read. This note
says what it is, why it's built the way it is, and what it leaves out, for anyone working on it.

## Why

- **Hudini needed its own designer.** Roundhouse designs UIScript for VentureOS, and draws its canvas with
  VentureOS's code. A game draws with Hudini, in Swift and raylib, with its own themes and font, so Roundhouse's
  canvas would only ever be an approximation of what the game shows.
- **Faithful by construction.** Depot's canvas is drawn by Hudini itself, through `depot-render`, so what you design
  is what the game draws.

## Goals

1. Open a `.uix` document and see it as Hudini draws it, beside its text, redrawn as you type.
2. Design it visually: select, move, and resize; an outline and an inspector; a library to add from; Undo and Redo
   for every change.
3. Save without loss: comments, attribute order, and spacing stay as written, apart from what was changed.
4. Show Maxis `.uis` files, which Hudini reads, without changing them.
5. Run on Linux, macOS, and Windows.

## Non-goals

- Running a game's logic. Hudini apps are Swift programs; Depot draws their interfaces, not their behavior.
- Writing `.uis` files.
- The parts of UIScript Hudini doesn't read: menus, sidebars, panes, shelves, grids, and status items. Roundhouse
  designs those for VentureOS.

## Anti-goals

Depot fails, even if it works, if:

- **The canvas drifts from Hudini.** It's drawn by Hudini, in Hudini's themes, so it can't.
- **Saving damages files.** Rewriting, reordering, or dropping what it doesn't understand, even once.
- **The designer makes documents Hudini refuses.** Every designer edit is checked against Hudini's subset before
  it's made.
- **It becomes the only way to write a document.** The text is always beside the canvas, and every change is a
  change to it.

## How it's built

```
┌──────────────── Depot (C#, Avalonia) ────────────────┐        ┌──── depot-render (Swift) ────┐
│ text editor ─┐                                       │  JSON  │                              │
│              ├─ UIDocument (Depot.Interface) ────────┼──────▶ │ Hudini reads and draws it    │
│ designer ────┘   held to HudiniSubset                │ line in│ in a hidden raylib window    │
│   DesignMap ◀── frame map ◀──────────────────────────┼─────── │ PNG + frame map              │
│   canvas    ◀── PNG                                  │line out│                              │
└──────────────────────────────────────────────────────┘        └──────────────────────────────┘
```

- **Depot** is the shell, in C# with Avalonia, taken from Roundhouse: the text editor, the designer (`DesignMap`,
  `FrameDrag`, `FrameEdit`, `DesignEdits`, and the inspector), and the window around them.
- **Depot.Interface** is Depot's own copy of VentureOS's `Venture.OS.Interface`: the UIScript reader, `UIDocument`,
  which edits a document's text in place so nothing else changes, and `UIScriptSchema`. `HudiniSubset` narrows them
  to what Hudini reads.
- **depot-render** is a small Swift program over Hudini. Depot keeps it running with `--serve`, sends it the text on
  each pause in typing, and gets back a picture and a frame map: where Hudini placed each control. See
  [its reference](depot-render.md).
- **The frame map matters as much as the picture.** Roundhouse's designer takes frames from VentureOS's layout;
  Depot's take them from Hudini's, matched to the document's elements by kind and name, so selection lines up with
  what's drawn.

Drawing takes a moment, so the designer doesn't wait for it. While you drag, the selection follows the pointer at
once and the picture catches up; the text changes once, when the drag ends. While typing, only the newest text waits
to be drawn, and older requests are dropped.

## Decisions

| Date | Decision | Why |
| --- | --- | --- |
| 2026-11-25 | A C#/Avalonia shell from Roundhouse, with a Swift renderer, rather than a fork drawing with VentureOS's painter or a designer written on Hudini. | The canvas has to be Hudini's own, and nearly everything else (lossless editing, the designer, the text editor) already existed in C#. |
| 2026-11-25 | Depot keeps its own copy of `Venture.OS.Interface`, rather than a shared package. | So it can evolve with Depot. The UIScript spec keeps the format in step, and the VentureOS HIG the look; nothing in code ties the copies together. |
| 2026-11-25 | Documents are held to the spec's Hudini subset, not to Hudini's current release. | Where Hudini lags, such as the scroll sliders the spec dropped, Depot follows the spec and Hudini catches up. |
| 2026-11-25 | `.uis` files open read-only. | Hudini reads them for The Sims Online's interfaces; Depot shows them, and leaves writing them alone. |
| 2026-11-25 | The default look is Hudini's `InterfaceTheme.depot`, which Depot wears too. | CityLimits' blue-slate and amber, softened after Adwaita by way of the VentureOS HIG. It lives in Hudini so a game can draw exactly what Depot shows. See [Depot's look](hig.md). |
| 2026-11-25 | Save writes the text as typed, even text Hudini refuses, and warns. | As Roundhouse does, so work in progress can always be saved. Designer edits can't leave the subset. |

## What's not there yet

- Text is drawn in raylib's default font, since a game loads its own and Depot doesn't know which.
- String references show their table and index; Depot doesn't read string tables yet.
- Toolbar items and the title field are selected from the outline, since Hudini doesn't report where it draws them.
- Keyboard shortcuts use Ctrl, including on macOS.
- Corners on the canvas are drawn without antialiasing.
