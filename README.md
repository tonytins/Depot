# Depot

Depot is a designer for the [UIScript](https://github.com/tonytins/VentureOS/blob/main/docs/uiscript/README.md)
documents that [Hudini](https://github.com/tonytins/Hudini)'s games read. It's a spin-off of Roundhouse,
VentureOS's IDE, and draws its canvas with Hudini itself, so what you design is what the game draws.

Depot is at its very start: the window is empty, and `depot-render` builds but can't draw yet.

## Layout

- `src/Depot`: the app, in C# with Avalonia, for Linux, macOS, and Windows.
- `src/Depot.Interface`: Depot's own copy of the UIScript reader and the document model it edits through, taken
  from VentureOS's `Venture.OS.Interface`.
- `depot-render`: a Swift package that draws a document with Hudini, for the canvas.

## Building

Depot needs the .NET 10 SDK; `depot-render` needs Swift 6.3 and a Hudini checkout at `../../../Hudini`, beside
VentureOS, along with raylib's native development libraries.

```sh
dotnet build Depot.slnx
dotnet run --project src/Depot
(cd depot-render && swift build)
```
