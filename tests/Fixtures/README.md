# Hudini's example documents

Depot's tests read these: `Depot.Interface.Tests` reads every `.uix` here, and `depot-render`'s tests draw every
`.uix` and `.uis` file. They're covered by Hudini's BSD 3-Clause license, in [LICENSE](./LICENSE), not by Depot's
GPL.

The `.uix` files are Hudini's examples (`Demo/Sources/hudini-demo/Resources/` and its `city/` folder, Hudini
commit `5f5e565`), which follow the
[UIScript spec](https://github.com/tonytins/VentureOS/blob/main/docs/uiscript/README.md)'s Hudini subset. Their lists
and text areas show their own scroll bars, with no slider or buttons beside them to scroll them, and their sliders
choose values.

The two `.uis` files, `debugbar.uis` and `lemonadestand.uis`, are Maxis interface scripts from Hudini's demo at
`37aa4d3`, which Depot only shows.

Refresh them by copying the files again when Hudini's examples change. If Hudini ever lags the spec again, take
VentureOS's copies (`tests/Venture.OS.Common.Tests/Interface/Fixtures/Hudini/`) until it catches up. A new file
here is tested without a code change.
