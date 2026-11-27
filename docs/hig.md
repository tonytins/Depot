# Depot's look

Depot follows the [VentureOS HIG](https://github.com/tonytins/VentureOS/blob/main/docs/hig/README.md), which takes
its colors from GNOME's libadwaita: neutral surfaces, one accent, few layers. It wears the look it gives new
documents, Hudini's `InterfaceTheme.depot` and its light twin `depotLight`, so where Depot differs from the HIG, it's
because that look does. This page lists the differences; anything not listed is as the HIG says.

When the look changes, change it in Hudini, in Depot's `App.axaml`, and here, together.

## Color

| | VentureOS | Depot, dark | Depot, light |
| --- | --- | --- | --- |
| Window | `#222226` | `#1A1E26`, CityLimits' blue-slate | `#FAFAFB`, Adwaita's |
| Title bar | `#2E2E32` | `#262B34` | `#FFFFFF` |
| Fields | `#1D1D20` | `#12151B`, a step darker than the window | `#FFFFFF` |
| Text | white | `#E4E8EE` | `#333338` |
| Accent | GNOME blue, `#3584E4`, or the user's | amber, `#F0BE5A` | amber darkened to `#B07A0E`, so it stands out from white |

- **The surfaces carry a tint in the dark look.** The HIG keeps surfaces neutral and lets only the accent carry
  color. Depot's dark surfaces keep CityLimits' blue-slate, since that's the character of the games Hudini was made
  for. The light look is Adwaita's neutral.
- **Quieter text is stronger.** The HIG draws secondary text at 55% of the text color. Depot draws it at 58% in the
  dark look and 70% in the light, so small text reaches 4.5:1 on every surface, the title bar included. Borders are
  20%, as in the HIG.
- **The selection on the canvas is GNOME blue**, not the accent, so it reads over amber and over any look a document
  is drawn in.

## Shape

- **One corner radius, 6.** The HIG rounds windows at 10 and sidebar items at 5. Hudini has a single radius for
  windows, panels, buttons, and fields alike, and 10 is too round for a button 20 pixels tall.
- **A flat title bar.** Hudini can gloss its title bar; Depot's look doesn't, since the HIG keeps gloss for the menu
  bar, the window buttons, and slider rails.
- **The window buttons are the HIG's traffic lights**: close `#EC5F56`, minimize `#F5B232`.

## Depot's own window

Depot draws its window with Avalonia's Fluent theme, compact, with its colors set to the look above. It follows the
system's light or dark setting. Its controls are Fluent's, not VentureOS's, so buttons, lists, and menus look like
an Avalonia app's in Depot's colors.
