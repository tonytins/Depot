# Hudini's example documents

Depot's tests read these: `Depot.Interface.Tests` reads every `.uix` here, and `depot-render`'s tests draw every
`.uix` and `.uis` file. They're covered by Hudini's BSD 3-Clause license, in [LICENSE](./LICENSE), not by Depot's
GPL.

The `.uix` files are VentureOS's copies of Hudini's examples (`Demo/Sources/hudini-demo/Resources/` and its `city/`
folder, Hudini commit `c7e6037`), which VentureOS keeps up with the
[UIScript spec](https://github.com/tonytins/VentureOS/blob/main/docs/uiscript/README.md) where Hudini lags:

- On 2026-11-24 the spec dropped scrolling by hand: a `<Slider>` or button with `scrolls`, and `direction`, since
  lists and text areas show their own scroll bars. `chatdialog`, `movelot`, `optionspanel`, and `city/help` lost
  their scroll sliders and buttons ahead of Hudini, their lists and text areas widening into the room.
- On 2026-11-25 `<Slider>` came back for choosing a value, as Hudini reads it, so `optionspanel`'s volumes and
  `city/budget`'s figures are value sliders again.

The two `.uis` files, `debugbar.uis` and `lemonadestand.uis`, are Maxis interface scripts from Hudini's demo at
`37aa4d3`, which Depot only shows.

Depot follows the spec's Hudini subset rather than Hudini's current release, so refresh the `.uix` files from
VentureOS's copies (`tests/Venture.OS.Common.Tests/Interface/Fixtures/Hudini/`), not from Hudini's demo, while
Hudini lags. A new file here is tested without a code change.
