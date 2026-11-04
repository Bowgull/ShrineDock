# Phase A plan (native core) — proposal, not yet approved

Spec: R26 `reddit-smooth.html` (mockups repo). Values below are lifted from its CSS/JS.

## Files (all new, feed-only)
| File | WPF? | What |
|---|---|---|
| `FeedData.cs` | no | Parse `%APPDATA%\Aang\reddit-feed.json` → `FeedPost` records (kind photo/text/link, sub, icon, age, title, lede, domain, image path). Bad/missing file → empty, never throws. |
| `FeedQueue.cs` | no | Holds what's shown vs waiting. New posts join one at a time; dedupe by id; caps memory. |
| `FeedMotion.cs` | no | The river maths, frame-free: drift 9 px/s; ease to 0 with τ = 0.2 s (≈600 ms) and back; wheel seek τ = 0.14 s, gain 0.9 × deltaY; whole-pixel settle; recycle when the 2nd post's top passes −40 px (offset += step, so no jump). Written as closed-form position(t) so Composition can be handed keyframes instead of ticking. |
| `FeedLayout.cs` | no | Numbers: 420 wide, padding 34/28, post padding 13/15/15, gaps 24 (rule) / 46 (knot), rule inset 36, knot 9 px at −28, picture 158 h (−3 px sides, r14), lede max 48 px, fades 30 top / 100 bottom, dim rows at >440 (.66) and >560 (.42). Whole-pixel snapping. |
| `FeedArt.cs` | yes | Bakes each post once into a frozen bitmap (title draw-in = second bitmap, faded in). Fonts loaded from bundled files. Photo toned (saturate .5, brightness .78) at bake time, decoded at display width. |
| `FeedWindow.cs` | yes | Grows from `FeedSpike.cs`: non-layered DWM window, region once, bottom layer, tool window, every error logged and swallowed. |
| `FeedComposition.cs` | yes (WinRT) | The mover: posts are `SpriteVisual`s over the window via `DesktopWindowTarget`; drift is a compositor animation, so the app does nothing per frame. A WPF mover (the spike's, 30 fps) stays as fallback behind a setting so the Shadow can measure both in one sitting. |
| `fonts/` | | Philosopher-Bold, FiraSans-Regular/Medium (OFL, licences included). |
| `ShrineDock.Tests/` | | xUnit, net10.0, links the WPF-free files: parsing, queue, motion (no jump on recycle, settle to whole px, ease reaches 0 in ~600 ms, per-frame budget), layout snapping. |

## Shared-file touches (small, flagged)
- `ShrineDock.csproj`: exclude `ShrineDock.Tests/**`, copy `fonts/**`, and (for Composition) TFM `net10.0-windows10.0.19041.0`. **The TFM change affects the whole app** → needs his OK.
- `Program.cs`: one line — start `FeedWindow` only if `Settings.Feed` is true (new field, default false).
- WidgetKeeper: add "Feed" to the widgets it keeps (needs a hwnd getter, like the clock).

## Order
1. Tests project + FeedData/FeedQueue/FeedMotion/FeedLayout, all tested here.
2. FeedArt + FeedWindow with the WPF mover (compiles here).
3. Composition mover.
4. "Ready for a Shadow test": run both movers through the gate (3 × 20 s, middle run, ShrineDock + dwm).

## Blockers / risks
- **Feed JSON shape**: `src/Shell/src/reddit.ts` is in the AangApp repo, which this session can't reach (only public `Bowgull/aang`, Sept, no reddit code). Need it before step 1's parser.
- **Composition can't be run here at all.** First Shadow run may fail outright; the WPF fallback makes that run still useful.
- Composition has no text: every post is baked as a bitmap in WPF and uploaded. Hit-testing (phase B) stays in WPF.
