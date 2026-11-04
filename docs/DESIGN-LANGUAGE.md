# ShrineDock's design language

These are Joshua's rules for how anything in the Shrine kit looks (dock, strip, Start, Tasks, Settings, menus, mixer, widgets, the Reddit feed). He set them in many conversations. **Read this before drawing anything new.** If something here and a sheet disagree, ask him.

## The materials (in this order, outside in)
Every panel is made of the same four things. They come from his own art (`art/frame.png`, and the Rainmeter `clock.png` / `pc.png` it was cut from).

| Layer | Where (1x px, from the outside) | What it is |
|---|---|---|
| Dark edge | 0–1 | almost black green `#0C100F`, never pure black |
| **The rail** | 2–7 | **bronze bars of uneven lengths with dark brown joints between them**, like bamboo or stacked bronze pieces (details below) |
| Dark gap | 8–9 | `#19221F` |
| Cyan line | 10 | `#5FADD3`, 1 px, the light inside the frame |
| Stone | 11+ | deep navy slate with faint contour lines and a soft cyan glow near the edge (frame.png's centre) |

**Corner seals:** his spiral seals, cut from frame.png: dark and bronze rings spiralling round a cyan gem. They sit **on** the corner curve, never on a square corner.

### The rail, measured (his words: "braided", "dark faded")
- Bars run **11 to 44 px** long. Joints between them are **1 to 7 px** wide, colour about `#584220`: dark brown, not black.
- Each bar has its **own brightness** (red channel about 160–208) and is lit across its width: brightest in the middle (about 4 px in), darker at the bottom edge.
- It is **not** a twisted rope with stripes, and **not** a smooth shiny gradient. Both were tried on 2026-11-04 and rejected.
- **Always use the real rail.** `tools/make-frame-soft.py` samples it from frame.png. Never redraw it by hand.

## Shape
- **Soft corners always.** No hard squares, no square ends on bars or tracks, no sharp boxes. The frame's outer radius is 33.5 px, so the curve runs through the seal centres.
- Slider tracks and fills are pills. Buttons are round seals.
- No boxes inside boxes, no card per item, no pill buttons with text, no helper text.

## Quality
- **Smooth, high quality.** Art is made at 2x and shown at half size with smooth scaling (`frame-soft.png`). It must never look blocky or show pixel steps.
- Keep **his textures** (rail, stone, seals, icons). Upgrade how they're drawn, never what they are.
- A rejected example of how not to do it: enlarging pixel art with no smoothing.

## Colour
- Ice `#C4F2FF` (text, lit icons), Cyan `#38C6FF` (glow, fills), Amber `#EEA040` (muted / warning / "wants you").
- Bronze for things drawn in it: `#DDB870` light, `#B8924C` middle, `#6E5226` dark. Edges are dark bronze/wood, never black outlines.
- **No flat shiny bronze** on rails or frames. Bronze rails are always the bars-and-joints rail.
- Glow only on something that's switched on. One light, from the top.

## Icons
- **App icons:** the app's own art. Games use their real game icon, **whole** (he chose BotW's full icon over a face crop), set in a soft bronze frame with round corners. Never a hard dark square.
- **Kit icons:** made from the kit's materials. The mixer's PC is a bronze-bezelled monitor with the stone as its screen and cyan gem corners (`art/mixer-pc.png`). Glowing cyan line icons like `folder.png` are also in the language.
- Thin flat line glyphs drawn in code were rejected for the mixer ("sucks").

## The cursor
- The row cursor in menus and Settings is the **Shrine gauntlet** (sheet 24 B, his pick): a bronze cuff with the rail's joints and a cyan gem, and ivory fingers lit from the top. It is `art/finger.png` at 2x, shown at 20x13 with a soft shadow and no glow, and it nudges.

## Layout rules
- **Line things up, like a table.** In the mixer, the name column is as wide as the longest name showing, no wider. Every bar starts there and takes the rest, so all bars match. No dead gaps.
- Measure alignment from real pixels before showing him anything.

## Motion and behaviour
- Smooth motion only: no jumps, no bounce. Ease in and out.
- Clicking outside or pressing Esc closes anything that's open, smoothly.
- A full-screen screen is left alone: nothing we open on the other screen covers it or keeps its keys (`FullScreen.cs`).

## Where this applies
- **Built with it (2026-11-04):** Start, Tasks, Settings, every Shrine menu and the mixer, all through `SettingsPanel.SoftFrame`.
- **The Reddit feed:** its layout is decided (R26 · Smooth river, see CLAUDE.md). R26 shows a brass frame only on hover or click. **His call is still open:** whether that frame becomes this rail. Ask him before building phase B.
- **Still on the old square pixel frame:** the strip's window preview (`StripWindow.PreviewFrame`) and its rail overlay.
- **The Rainmeter widgets** (clock, PC) are the source art. They still have square corners.
