# ShrineDock design-language audit, and where the Reddit sheets leave it

2026-11-04. Read-only audit. Sources: ShrineDock C# (SettingsPanel, ShrineMenu, StartMenu, TasksPanel, MixerView,
VolumePopup, StripWindow, Program, ClockWindow, Arrival), the art PNGs (sampled pixel by pixel), his approved
screenshots, the 20 Reddit sheets' CSS, and his verdict memories.

---

## A. ShrineDock design-language spec

### A1. Colour tokens (exact, from the code)

| Token | Hex | Role |
|---|---|---|
| Cyan | `#38C6FF` | The one "powered" colour: ▶ cursor, lit seal eye, slider fill end, active pip, glows |
| Ice | `#C4F2FF` | Labels and titles (window titles, row labels, name plates) |
| Text | `#E8F6FF` | App names in Start / Tasks |
| White | `#FFFFFF` | Values only (picker value, time), always with a cyan glow |
| Dim | `#7FA8BF` | Inactive tab text, "Off" |
| Sub | `#6D93AB` | Second lines in Start / Tasks |
| Hint | `#5F86A0` | Notes under labels, hint line |
| Clay | `#B8924C` | Brass borders: active tab, switch well when on, button rim, name-plate rim |
| ClayHi | `#DDB870` | Section heads, ‹ › arrows, key names in the hint line, divider centre |
| ClayLo | `#6E5226` | Notches, rim shadow end |
| Rim top | `#F0CE88` | Lit top of the menu rim (gradient F0CE88 → B8924C → 6E5226, top to bottom) |
| Well | `#06121C` (`#04101A` in Start/Tasks) | Carved insets: switch well, slider well, search well |
| WellEdge | `#10202A` | The 2 px edge round a well; inactive tab border |
| Slate pane | `#122C42` → `#06101A` at 0xF6 alpha | Menu pane, top to bottom |
| Rule | `#1C3446` | 1 px line above the hint foot |
| Amber | `#EEA040` (Pale `#FFD9A0`) | ONLY risk: Close window, End?, muted. Never decoration |
| Outline | `#0C1110` / `#0B0F0E` | The pixel art's outer line (greenish near-black, never pure black) |
| Firefly | `rgb(234,255,200)` core, `rgb(214,255,176)` halo | The garden's and clock's fireflies |

Frame art slate (frame.png centre): `#082039`–`#08243E`, alpha 240 (94%), with an inner glow from `#14466B` at the
cyan line fading to `#08223A` within ~12 px.

### A2. Type

- **Exo 2** for everything you read as UI. Window titles: Italic Bold 24 (Settings "Shrine"), 22 ("TASKS"), with a
  cyan glow (blur 8, opacity .7) and a short ClayHi rule 44×2 fading out on each side.
- Row labels: Exo 2 Medium 14.5, Ice. Menu rows: Exo 2 Regular 14, Ice.
- Values: Exo 2 **Italic** SemiBold 13–14, white, cyan glow blur 6. Italic = "this is a live value".
- Section heads and tabs: Exo 2 SemiBold 11–11.5, UPPERCASE, ClayHi (tabs Dim when off).
- **Figtree** Regular 11–12 only for small second lines (notes, hint foot, weather). Never for titles.
- Name plates (dock, strip): Exo 2 Italic SemiBold 13, Ice. The dock's labels are pre-drawn PNGs (label-*.png).
- No letter-spacing anywhere in the code. No Philosopher anywhere in ShrineDock.

### A3. Shape and size rhythm

- Corner radii are small and few: rows 7, inner line 8, plates/tiles/task rows 8–9, menu pane 10. Pills (17, 12)
  only for wells and the End seal. Seals and knobs are circles. Nothing in the C# is above 10 except wells.
- Border widths: 2 px is the house line (tabs, wells, buttons, task rows, seals). 1.5 px for small plates. 2.5 px
  for the menu rim. 1 px for the cyan inner line (`#38C6FF` at 0x60).
- Rows: 42 px tall (settings, tasks). Settings panel 452 wide, controls column 196. Insets 26 left for the cursor.
- Gaps: 4 between tabs, 8–12 between groups, margin 14–16 round a window for its shadow.

### A4. Materials (what things are made of)

1. **The frame** (frame.png, 440×232, nine-sliced at slice 32, **1:1, nearest-neighbour**). Its edge, outside in:
   1 px outline `#0C1110`, 1 px `#1C2624`, a 6 px brass tube ramp (`#876833 #98773C #AD8846 #B18C48 #A27F41
   #937239`: dark, light in the middle, dark, so it reads round like bamboo), dark segment joints every ~40 px,
   2 px `#18201E`, 1 px cyan `#60AED5`, then slate with an inner cyan glow and faint contour lines. Corners are
   **concentric ring seals** (black/brass rings round a square cyan eye), pixel-hard, 32 px, covering the corner.
   The frame's corners are square under the seals. It is never scaled.
2. **The dock/strip tile** (dock-tile.png 144, strip-tile.png 104): a smoother, anti-aliased rounded brass bezel,
   lit from above (top `#D4B16D`, sides `#AA884B`, bottom `#805F29`), round gem bolts with a top-left glint, slate
   face with a fine grid texture and contour lines. A "hot" twin is the same art, brighter face. Drawn at its
   own size; the dock shows it at 85%, Samara and Aang stay 1:1.
3. **The menu pane** (ShrineMenu.Pane, code not art): slate gradient, 2.5 px brass rim lit top / dark bottom,
   radius 10, a 1 px cyan inner line 16.5 px in, and 20 px menu-seal.png pixel seals in the four corners.
4. **Wells**: carved = dark fill `#06121C` + 2 px `#10202A` edge. Fill bars inside run `#155A80` → Cyan.
5. **The seal knob** (Seal()): radial Ice → Cyan → ClayHi → ClayLo: the frame's seal in miniature.
6. **Clay plate** (Act button, active tab, All plate): `#3A2A12` → `#22180A` vertical, 2 px Clay rim, cream text
   `#FFECC4`. The All plate adds a 1 px dark-wood edge `#1A1208` round it.

### A5. Light, depth, glow

- **One light, from above** (top-left on round things). Rims are lit top, shaded bottom. Gem glints sit top-left.
- **One drop shadow per window**, straight down: settings blur 22 depth 10 opacity .6; menus blur 18 depth 5.
  Pieces inside a window do not get their own drop shadows.
- **Glow = power.** Cyan glow only on: the cursor, the value text, a lit seal, the active pip, a hovered menu
  row's text. Amber glow only on a risky row. Brass glow (ClayHi, blur 10, .3–.45) only on the active tab and a
  hovered button. Nothing glows at rest unless it is "on".

### A6. Focus and states

- **Settings**: the ▶ cursor (cyan triangle 9×12, glow blur 6) nudges 0→3 px every 500 ms, plus a band
  `#38C6FF` at 0x33 fading to 0x0A left to right. Odd rows get a 2% white stripe.
- **Menus**: a 3 px cyan bar appears left of the text, the row fills cyan at 0x40, the text glows. Amber for risky.
- **Tasks**: 2 px cyan border at 0xCC + 0x24 fill.
- **Dock / strip**: only the tile under the mouse lifts (scale, 200 ms CubicEase out), swaps to its hot art,
  and its name plate appears after his delay (≥300 ms). Running apps: a 4 px cyan pip, radius 2, glowing.
- **Press-again toggles**: switches and lit buttons, never Undo links. Risk asks once (amber "End?").

### A7. Motion

- Easing is **CubicEase EaseOut** for arrivals and lifts, EaseIn for leaving, SineEase for breathing. **No springs,
  no overshoot.**
- Durations: menu fade 90 ms; popups lift 180–240 ms + fade 150–160 ms; dock rise 320 / sink 260 ms; strip 300 /
  240 ms; hover lift 200 ms; switch knob 140 ms; fold arrow 220 ms; row fold-away 340 ms EaseInOut; bars 900 ms;
  arrival fade 700 ms Sine. Ambient: fireflies breathe .08↔.85 on their own slow beats; second hand sweeps.
- Esc or a click outside closes everything, at once.

### A8. Words

- Short, sentence case for labels; UPPERCASE ClayHi only for section heads and tabs.
- The hint foot (Figtree 11.5, keys in ClayHi SemiBold, rest in Hint) exists in Settings. **He banned it for the
  feed** (Round 7: no helper text, ever). So the hint foot is a settings-panel thing, not a widget thing.
- **Labels are doors**: the dock label, the "All" plate, "Windows' Task Manager ›" are the action itself.

### A9. The rules that make it feel hand-made

1. Real art at its true pixel size, sliced, never stretched or resampled.
2. One outline colour (`#0C1110`, greenish black), never `#000`, never a CSS hairline.
3. A brass ramp with a highlight in the middle (a tube), plus dark joints at irregular-looking but fixed spacing.
4. One light from above. Every rim and glint agrees.
5. A small, closed palette: two blues for text, one cyan for power, one brass family, one amber for danger.
6. Glow means "on". Brass means "you can touch this". Amber means "careful".
7. Few radii (7–10), one stroke (2 px).
8. One shadow per window. Depth inside a window comes from wells (carved in), not from stacked shadows.
9. Short eased motion, no bounce. Things lift 2–4 px, never fly.
10. Every popup is a game menu: framed window, cursor, rows. Not a card.

---

## B. Sheet by sheet

Legend: ✔ honoured, ✘ broke. CSS cited from the sheet.

| Sheet | His verdict | Broke | Honoured |
|---|---|---|---|
| **01** feed shapes | "really poor, plain, linear, flat, intrusive, harsh sharp edges, AI slop" | ✘ radius 3px cards (A3: square to him); ✘ a web list (thumb + meta + title) in a frame (A9.10); ✘ invented palette (`#8FE3FF`, `#B8893C`, `#0B4F74`); ✘ no depth, all `linear` motion | ✔ Exo 2 |
| **02** stone tablets | "looks right, feels right, designed like AI slop"; later "loved its lighting and softness" | ✘ 5 stacked shadows on one tablet (`inset 0 1px…, inset 0 -3px…, 0 12px 26px…, 0 2px 4px…, 0 0 0 1px…`) (A5); ✘ glow on every tablet's ring at rest; ✘ pill buttons `999px`; ✘ spring overshoot `cubic-bezier(.2,1.25,.35,1)` (A7); ✘ invented "REDDIT" orb crest with letter-spacing 4px; ✘ Ken Burns zoom; ✘ blur play button | ✔ ONE light: `radial-gradient(130% 90% at 25% 0%…)`; ✔ contour (pc.png) + feTurbulence grain .13; ✔ inner rune ring `inset: 5px` 1px at .20; ✔ tucked `margin-top: -6px`, staggered 16px. These four are what he loved: they read as material, not UI |
| **03** tablets polish | "close… but disconnected and a bit flat; 02 had a great soft feel"; liked the 1.5-post fold | ✘ 31 box-shadows, 44 insets; ✘ clip-path bevels (sharp); ✘ crest plaque | ✔ one light, recessed picture windows, glow only on powered things, pixel sigils (`pixelated` ×2) |
| **04** living stone | "slapped next to each other"; crest "extremely low-grade" | ✘ nothing holds the cards (no frame, no river) (A4); ✘ thumbtack pins; ✘ boxes in boxes for comments; ✘ 15 px crest | ✔ momentum scroll; knob palette `#0c1110 / #bc9650 / #7fd8ff`; outline colour right |
| **05** structures | Options sheet; led to "one framed viewer vs loose vs Cover Flow" | Same card CSS as 04 (hex list identical) | ✔ Asked him to choose the holder |
| **06** hanging tablets | Fed into 07 | ✘ invented objects (cords, ema roofs) (A9.1) | ✔ outline `#0c1110`, brass `#bc9650` |
| **07** tablet shapes | "corny, don't make sense, still AI slop"; "low-quality pixel bolts" | ✘ invented shapes (A9.10); ✘ glass `backdrop-filter`; ✘ 48 inline SVGs; ✘ ~30% contrast; ✘ "Reddit in a costume" (icon + r/sub · 3h + vote column) | ✔ smooth bolts in the clock's real brass `#f0ce8c/#b08842/#937239`; ✔ no bounce |
| **08** shrine panels | "looking better, getting closer"; edges must be MUCH softer | ✘ each post in a whole square-cornered frame.png (boxes stacked); ✘ square picture well `border: 2px` no radius; ✘ uppercase letter-spaced sub label; ✘ helper foot | ✔ the only sheet using `frame.png 32 fill / 32px` at **1:1, pixelated** (A9.1); ✔ ▶ cursor + cyan band (A6); ✔ exact SettingsPanel palette |
| **09** soft feed | (no recorded words; led to R10) | ✘ `dock-tile.png 40 fill / 26px` = **0.65×** resample of pixel art (A9.1); ✘ a frame per post again | ✔ soft rounded rim he asked for; ✔ words/glyph controls, title is the door |
| **R10** one frame | Likes: "cleaner, posts float over the garden, no big box". Low quality: "brass frame thin with clip-art bolts, cards flat" | ✘ frame = a **4 px CSS gradient ring** (`.lens .ring { padding: 4px … linear-gradient(165deg,#fff1cf…#6e5124) }`) vs his 12 px rail; ✘ 24 px smooth SVG bolts with glints (clip-art); ✘ cards `linear-gradient(180deg,#0D1F2F,#0A1825)` + one 7% top line = flat, no grain, no contour (A4); ✘ radius 16/22 | ✔ no box round the feed; ✔ one focus thing that travels (A6: one cursor); ✔ only the focused post wears brass (glow/brass = meaning) |
| **R11** hover frame | "VERY close" | ✘ same thin ring and bolts | ✔ brass ONLY on hover (A5: brass = touchable now); ✔ no ▶ arrow |
| **R12** lit tablets | (no recorded words; led to R13) | ✘ 5-shadow stack back (copied from 02); ✘ ring glows on every post at rest | ✔ 02's light, contour, grain; ✔ no helper text |
| **R13/R14** frosted | "box is the slop"; couldn't tell comments from titles; More/Less too hidden | ✘ `backdrop-filter: blur(16px) saturate(1.25)` (no glass anywhere in ShrineDock; and DWM can't do it on the real desktop); ✘ low contrast, flat hierarchy | ✔ snug at rest, part on hover |
| **R15** encasing | Wanted "all four" combined | ✘ 22 box-shadows, 23 gradients, 8 backdrop-filters; ✘ dock-tile at 18px/22px (0.45×, 0.55×) | ✔ showed real choices |
| **R16** all four | → R17 | ✘ `clip-path: var(--cut)` polygon slate (sharp); ✘ corner brackets from 8 linear-gradient bars (janky) | ✔ river lines, rising slate idea |
| **R17** depth | "closer than ever" | ✘ `border-radius: var(--soft)` set by `Math.random()` per card (fake hand-made, A9.3); ✘ 33 box-shadows; ✘ CSS brackets, pill grip | ✔ three depths in an open post (title raised, body, sunken channel = a **well**, A4.4); ✔ burnished 9 px band `#b8955a→#4f3a1b`, light on top edge only (A5) |
| **R18** shrine kit | **APPROVED**, "closer, but bugged and a little thick" | ✘ **bent corner pieces I invented** (corner-tl.png: a flat brass arc, no ramp, no joints, so the corner doesn't match his rail) instead of his square corner + seal; ✘ 40 px corner art + 12 px rail + 29 px knob + contour face at .58 = thick; ✘ `.rail` class clash bug; ✘ R10's whole CSS still underneath, overridden 8 times (`sheet 10 … R18` layers) | ✔ rail cut from frame-rail.png at **1:1 pixelated**; ✔ his real seal knob 29 px; ✔ contour face; ✔ context menu in his rail |
| **R19** remaster | Current | ✘ rail redrawn as SVG rects with **fractional strokes** (2.4, 5.4, .9, 1.6 px) and `stroke-dasharray: 2.2 46` = machine-even dashes, soft mush (A9.1–3); ✘ knob redrawn as a **spiral** (his are concentric rings), sub-pixel strokes .6/.7; ✘ knobs n2–n4 **mirrored** with `scaleX(-1)/scale(-1)`, so 3 of 4 glints face the wrong way (A5: one light); ✘ cyan `filter: blur(1.2px)` glow on the frame at rest of focus; ✘ web tooltip (`rgba(6,16,26,.94)`, radius 8, box-shadow) instead of his name plate (1.5 px Clay rim, Exo 2 italic Ice); ✘ easing `cubic-bezier(.32,.72,0,1)` is iOS's sheet curve, not his CubicEase | ✔ R10's float-over-garden; ✔ contour .22 + grain .07 back on cards; ✔ photos recessed (inset vignette = a well); ✔ brass only on hover; ✔ amber mark only for new-for-you |
| **R20** deep dive | Picked A River + B Slate | (research board, own palette `#141c1c`) | ✔ "colour only means something", one thing in focus, no spinner |

---

## C. The pattern

**What earns "closer":**
- His **real art at its true size**. R08 (frame.png 1:1) and R18 (frame-rail + knob 1:1) are the two biggest jumps
  forward. Both are the only sheets that use his PNGs without resampling.
- **Less on screen.** R10, R11, R19: no box round the feed, brass only where the mouse is. That is ShrineDock's
  own rule: the dock sleeps as a seam and only the tile under the mouse lifts.
- **One focus thing.** A single travelling frame (R10/R11) = the ▶ cursor in a Zelda menu. Every time a sheet
  had one thing lit and the rest calm, he moved toward it.
- **Material light, not UI light.** 02's top-left radial, contour lines, grain, and tucked overlap. That's the
  frame.png slate (inner glow, contour) and the garden (grain, fireflies).
- **Wells.** The sunken comment channel (R17) is a ShrineDock well. It earned praise.

**What earns "slop":**
- **Inventing a material** he didn't make: ema roofs, cords, frosted glass, bent corner pieces, a spiral knob,
  CSS gradient rings, stretched dock-tiles. Every invented material got "slop", "corny" or "low quality".
- **Resampling his pixel art.** dock-tile at 0.65×/0.55×/0.45× (09–R17) and SVG-redrawn rails (R10, R19) turn
  crisp pixel brass into thin, soft, "clip-art" brass. This is the single most repeated technical fault.
- **Stacked shadows and glow at rest.** ShrineDock: one shadow per window, glow only when "on". Sheets: 5 shadows
  per card, 30+ per sheet, a glowing ring on every post. That's the web-card tell.
- **Web furniture.** Pills, tooltips with blur, "r/sub · 5h" meta lines, vote columns, helper text, Ken Burns.
- **Random as fake hand-made.** `Math.random()` radii. Hand-made in his kit means hand-placed, fixed pixels.
- **Layered patch sheets.** R18/R19 still carry R10's full CSS with 2–8 override layers. Bugs (the `.rail` clash)
  and "a little thick" both come from patching, not drawing.

Blunt version: he isn't reacting to the layout. He's reacting to **material fidelity**. Whenever the brass and
slate are his real pixels, at 1:1, lit from above, he says "closer". Whenever they're CSS imitations of his
pixels, he says "slop", even when the layout is good.

---

## D. Ten rules for the next sheet

1. **Use his PNGs at 1:1, `image-rendering: pixelated`, whole multiples only.** No `border-image … / 26px` on a
   40-slice, no SVG redraws. Evidence: R08 and R18 were the jumps; R10's ring "thin, clip-art"; 09–R17 resampled.
2. **If the brass must be smoother, draw it as new pixel art in his ramp** (`#876833 → #B18C48 → #937239`, outline
   `#0C1110`, 6 px tube, joints), at one fixed size, and show it as a labelled option. Never fractional SVG
   strokes. Evidence: R19's 2.4/5.4/.9 px strokes; his "my pixel art but a smooth upgrade" + pixel-art-crisp.md.
3. **Corners are his seals, never bent rails.** Square rail ends hidden under the concentric-ring seal, as in
   frame.png and the clock. Evidence: R18's invented corner-tl.png arc; "a little thick".
4. **One light, from above. Never mirror a lit asset.** Rotate the knob or draw four, so every glint sits
   top-left. Evidence: R19 knobs n2–n4 flipped; dock-tile and menu rim are lit top / shaded bottom.
5. **One shadow for the whole widget; depth inside comes from wells.** Max one drop shadow (blur 18–22, down) on the
   widget, the risen post may add one. Comments, pictures: carved wells (`#06121C` fill, 2 px `#10202A` edge).
   Evidence: 5-shadow stacks in 02/R12/R17; R17's sunken channel praised.
6. **Glow only means "on".** Cyan glow on the focused thing only; amber only for new-for-you and a cast downvote;
   brass glow only on hover. Nothing glows at rest. Evidence: A5; rings glowing on every post (02, R12) = slop.
7. **Keep R10's float and R11's hover brass.** No box round the feed at rest. One brass thing travels to the post
   under the mouse, like the ▶ cursor. Evidence: R10 "cleaner", R11 "VERY close".
8. **Slate = frame.png's slate**: `#082039`–`#08243E`, contour lines, inner glow from the edge, grain. Not a flat
   CSS gradient. Evidence: R10 "cards flat"; 02's contour + grain loved.
9. **His motion, not iOS's.** CubicEase out, 200–320 ms lifts, 90–160 ms fades, 2–4 px lifts, no springs, no
   random radii. Replace `cubic-bezier(.32,.72,0,1)` with ease-out cubic (`cubic-bezier(.33,1,.68,1)`).
   Evidence: A7; he rejected the bounce (round 5).
10. **Draw the sheet fresh, from his kit, with no override layers and no web furniture.** No pills, no blurred
    tooltips (use his name plate: radius 8, 1.5 px Clay rim, `#0A1A28` at 0xF0, Exo 2 Italic SemiBold 13 Ice),
    no helper text, no meta dots. Run a `?measure` check for edges and centres before he sees it. Evidence: the
    R18 `.rail` bug; measure-alignment.md; Round 7 "no helper text".

### Open tensions to ask him about (not mine to settle)

- He picked **Philosopher** titles and **Figtree 15.5 body** for reading. ShrineDock uses neither for headings.
  Fine as his choice, but it is the one place the feed openly leaves the kit.
- ShrineDock uses UPPERCASE ClayHi section heads; he banned all-caps on the feed.
- His "smooth upgrade" wish versus his "pixel art always" rule. Rule 2 is my way to satisfy both: new pixel art
  at a bigger fixed size, not vectors.
