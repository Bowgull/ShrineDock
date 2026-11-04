# Instructions for Claude sessions in ShrineDock

Read this first. Joshua often runs these sessions **from his phone**, as a Claude cloud session. The cloud machine is Linux. His real PC is a Shadow cloud PC running Windows, and it's only on for limited time.

## Talking to Joshua
- Keep replies short and plain, with no walls of text. Put detail in files.
- When something's his call, ask with a couple of clear choices.
- Never say something works unless you checked it.

## Set up (cloud / Linux / Mac)
```bash
# .NET 10 SDK, if `dotnet --version` doesn't show 10.x
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
export PATH="$HOME/.dotnet:$PATH"
dotnet build        # works off Windows thanks to EnableWindowsTargeting in the .csproj
```

## What you CAN do here
- Write and change the code, and **compile it** (`dotnet build`).
- **Unit-test the logic.** ShrineDock is WPF, which only runs on Windows. So put logic in **WPF-free classes** (no `System.Windows` usings) and test them in a test project (`ShrineDock.Tests`, xUnit, `net10.0`, linking those files). Good targets:
  - parsing `reddit-feed.json`
  - the post queue
  - drift and easing maths, and seamless recycling (no visible jump)
  - the Plates brass pattern
  - layout numbers: glyph centring, whole-pixel snapping
  - **per-frame work budgets**: prove a drift frame does almost nothing.
- **Show Joshua design sheets on his phone.** Sheets are HTML. Publish a sheet as a **private claude.ai Artifact page** and give him the link. If that tool isn't there, tell him where the file is.

## What you CANNOT do here (wait for the Shadow)
- Run or show the widget. WPF doesn't run on Linux, so there's no dev server for it.
- **The CPU gate.** It measures his Shadow drawing the widget on its own desktop: ShrineDock plus `dwm.exe`, 3 × 20 s, middle run. The limits are: drifting **under 3% of one core**, still **under 1%**, about 80 MB more memory at most. A Linux machine's numbers mean nothing for this.
- Game sleep, full-screen quiet, the real `%APPDATA%\Aang` link with Samara, and shipping.

When code is ready for those, say so plainly: "Ready for a Shadow test: turn the Shadow on and run X." Never test on the Shadow while `Cemu.exe` or `Wow.exe` is running. Full-screen Samara is fine.

## Current job: the Reddit feed widget, phase A
Everything is in `docs/feed/`. Read these:
- `build-plan.md`, especially **"Replan after R26"**: keep, change, scrap, and phases A–F.
- `research/shrinedock-audit.md`: his kit's real colours, fonts and rules.
- `research/slop-tells-and-craft.md`: **the anti-slop checklist. Grade every sheet on it before showing him.**
- `research/lightweight.md`: why the old web widget cost 17% CPU, and how to stay light.

**The approved design is R26 · Smooth river.** It's in the private repo `Bowgull/mockups`, at `shrinedock/mockups/shrine-reddit/reddit-smooth.html`, with its art in `art/` beside it. Clone that repo to read it, and lift exact values from its CSS and JS.

**Phase A (core only; hover, brass and opened posts are phase B):**
1. A feed window in `Feed*.cs` (`FeedWindow`, `FeedModel`…), growing from `FeedSpike.cs`. Desktop layer, non-layered DWM window cut to its shape, position kept like the clock (WidgetKeeper). **Off by default** behind a switch until phase F.
2. **Smooth, non-stop drift** at 9 px/s. It eases to a halt in about 600 ms under the mouse and eases back when the mouse leaves. The wheel scrolls with an eased seek. When stopped, it settles on a whole pixel. Recycling is seamless. Aim for 60 fps through Windows.UI.Composition (the compositor runs it, so there's no per-frame app work); 30 fps is the fallback. The spike measured 3.1% at 60 fps without Composition.
3. Data from `%APPDATA%\Aang\reddit-feed.json`, written by Samara. Its shape is in the AangApp repo, `src/Shell/src/reddit.ts` and `reddittaste.ts`. Images come from `reddit-cache`. New posts arrive from a queue, one at a time, and the title draws in.
4. Three post shapes:
   - **photo**: the title, then a full-width picture set into the stone, toned down at rest
   - **text**: two faded lines
   - **link**: a site line

   Each has a sub icon, name and age line. A faint brass rule goes between posts, and a small knot between groups. A feathered dark ground, soft top and bottom fades, and older posts dimmer near the bottom.
5. Type: bundle **Philosopher 700** (titles) and **Fira Sans 400/500** (everything else), both OFL. Title `#F3EAD5`, Philosopher 20 / 1.28. Body `#A9B8C0`, Fira 14.5 / 1.65. Meta `#8EA6B3` 13. Age `#68869A`.

## His design rules (never break)
- **Read `docs/DESIGN-LANGUAGE.md` first:** the kit's materials (the bars-and-joints rail, stone, spiral seals, cyan line), soft corners, smooth 2x quality, icons, colours and layout rules. Build every panel with `SettingsPanel.SoftFrame`.
- No card per post, no boxes in boxes, no pill buttons, no helper text, no all-caps on the feed.
- Soft, rounded shapes, never sharp corners. Smooth motion only: no jumps, no bounce.
- Glow only on something that's switched on. One light, from the top. Never flip a lit thing to reuse it.
- **Measure alignment** (glyph centre against button centre, gaps, whole pixels) before showing anything. He spots anything 1 px off.
- Use his real art and icons. The Samara button is Samara's own lantern icon, never a drawn stand-in.
- Buttons: **inlaid** brass + / − (more or less like this), brass **studs** for up and down votes.

## Repo rules
- This repo is **private** and stays private.
- Other sessions work here too (dock, Start, strip). Touch only your own files, keep feed code in `Feed*.cs`, never `git reset --hard`, and commit only your paths.
- Commit as `Joshua Bocas <256685449+Bowgull@users.noreply.github.com>`, never his personal email.
- In the app, catch every error. Never let a dialog pop up on his screen.
