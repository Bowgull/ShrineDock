# ShrineDock Reddit feed: build plan

Approved design: **R18 · Reddit in the Shrine kit** (Samara › Mockups › ShrineDock › Reddit feed; file `mockups/shrine-reddit/reddit-shrine-kit.html`). Every decision behind it is in memory (`shrine-reddit-feed-interview`). This plan turns that sheet into the real thing on your desktop.

## The shape of it

```
 Samara (signed in to Reddit)                         ShrineDock (on your desktop)
 ┌───────────────────────────────┐   reddit-feed.json  ┌─────────────────────────────────┐
 │ every 30 min: read your Reddit │ ──────────────────▶ │ Feed widget (the R18 page,      │
 │ + trending, rank and mix it,   │   + cached images   │ hosted in a desktop-layer       │
 │ save pictures, write the feed  │                     │ window next to the clock)       │
 │                                │ ◀────────────────── │ votes, more/less, hide sub,     │
 │ sends votes, learns your taste │   reddit-ask.json   │ open in Samara, read time       │
 └───────────────────────────────┘                     └─────────────────────────────────┘
```

- **Samara does all the Reddit work**: reading, ranking, learning and voting. It's the only program that touches Reddit, and it uses your own signed-in session.
- **ShrineDock shows the widget**: placement, Lock widgets, game sleep, away and full-screen quiet, and the Feed settings tab.
- **They talk through two files** in `%APPDATA%\Aang`, the same way the music player already works: `reddit-feed.json` (Samara to widget) and `reddit-ask.json` (widget to Samara).
- **"Open in Samara"** uses the existing open-urls drop.

## Phases

| # | Phase | What gets built | Est. |
|---|---|---|---|
| 0 | **Proof first** | A throwaway see-through widget window in ShrineDock showing a web page on the desktop layer, with the garden blurred behind it (Windows acrylic) and sleeping in games. This is the one real technical risk (see below), so it's tested before anything else. | 4 h |
| 1 | **Samara reads Reddit** | A 30-minute job using your session: your front page and subs, then trending (news, science, movies, tech, AI, music) so it never runs dry. Spoilers are blurred (BotW topics) and NSFW is hidden. The top 2 comments come with each post. Pictures and videos are saved locally (150 MB cap, oldest out first). All of it is written to `reddit-feed.json`. Includes tests on saved real Reddit replies. | 8 h |
| 2 | **Taste and mixing** | Learns from what you open and how long you read, More/Less like this, Hide sub, and your own Reddit upvotes and saves (a head start on day one). Mixes about 6 yours, 3 trending and 1 discovery in every 10, with no sub twice within five, compared within each sub. Writes the amber "new for you" picks and their why-line. The taste file stays on your PC and is never committed. | 8 h |
| 3 | **The widget page** | **GATE: drift must cost under 3% of one core, measured, before Phase 3 counts as done (it was 17% in Phase 0).** R18 rebuilt from real data. Cards are built safely (titles can't inject code). There's one animation loop that sleeps when idle. The smooth drift runs on the graphics card. You get hover-rise, the brass arcs, the open post with comments and votes, video on the post in focus, sizes, sounds, and the right-click menu. | 10 h |
| 4 | **ShrineDock host** | The Feed window on the desktop layer. Its position is saved like the clock's, and Lock widgets works. Drag the top rail to move it. It sleeps when a game is in front, goes quiet after 3 minutes away (new: idle detection) and hides during full-screen video (new). Votes and asks are passed to Samara. | 8 h |
| 5 | **Feed settings tab** | A new tab in your settings panel: drift speed, the favourites-vs-new dial, blocked subs and words, see-through, size (Small/Medium/Large), sound. This also fixes the panel's hard-coded six-tab width. | 4 h |
| 6 | **Ship and check live** | Put ShrineDock under private source control and write down its build-and-install step (it has neither today). Ship Samara with a what's-new line. Install through your ShrineDo task. Check it live on your desktop, never while you're in a game. | 4 h |

**Total: about 46 hours of work,** in this order. Phases 1 and 3 could run side by side in separate sessions once phase 0 passes.

## Risks, said plainly

1. **See-through web content in ShrineDock.** ShrineDock is WPF, and WPF's web control (WebView2) doesn't show through to the desktop in the usual way. Phase 0 tests the known workaround: a composition-hosted WebView2 with a transparent background, plus Windows acrylic for the real garden blur. **Fallback if it fails:** Samara hosts the widget window itself (Electron does transparency easily), and ShrineDock only keeps its position and quiet rules. The look stays the same either way.
2. **Reddit account risk.** Reading with your cookies every 30 minutes is light, and votes are sent only when you click. That keeps the risk small, but not zero. Reddit's rules don't allow unapproved automated access.
3. **ShrineDock has no source control.** A bad edit can't be rolled back. Phase 6 fixes this, but I'd start a private repo for it at the very beginning.
4. **Samara rules.** Every Samara commit goes to a public repo, so the taste data, your sub list and the cached feed stay in `%APPDATA%`, never in code. Samara changes must also behave sensibly on the Mac: there the feed job just stays off unless you want it.

## Decisions I'll take unless you say otherwise
- Comments: the top 2 for each post are fetched with the feed, so opening a post is instant.
- Drift: Glide only, at the speed set in the Feed tab. Step mode is dropped (you never picked it).
- Real sub emblems: Reddit's own icons, cached locally, with a soft brass rune when a sub has none.

## Phase 0 result (2026-11-03): PASS, with one job for Phase 3

Tested with a throwaway WPF window (WebView2, transparent page, whole window as DWM glass), on his real desktop:

- **See-through: works.** R18's frame, rails and knobs draw crisp, and the desktop shows around and through them. The window is cut to the frame's rounded shape plus the knobs, so clicks around it fall through.
- **Desktop layer: works.** It stays the bottom-most window.
- **Game sleep: works.** It hides and suspends the page: 0.5% of one core.
- **Blur behind: dropped.** Windows' official acrylic turns into a grey box while the widget is inactive (always, for a desktop widget) and ignores the rounded shape. The older blur call is undocumented. R18 has no blur anyway (a 58% tint), so none is needed.
- **Cost:** 0.5% of one core when still, but **about 17% while drifting**. Phase 3 must bring the drift down to a few percent (the target is under 3%): one compositor-only animation, no per-frame script, and no region updates every frame. If that fails, slow the drift or move it in steps.
- **Memory:** about 450–580 MB across WebView2's 7 processes. That's the price of a web widget, so Phase 4 shares one WebView2 environment for everything ShrineDock hosts.
- **ShrineDock** is now in a private local git repo (`Documents/Shrine Desktop/ShrineDock`, never pushed).

---

## Replan after R26 (2026-11-04)

**The design is now R26 · Smooth river** (`mockups/shrine-reddit/reddit-smooth.html`, approved 2026-11-04). It replaces R18. The widget is drawn **natively in ShrineDock**: WebView2 is gone from the widget, because Joshua said weight and reliability come first.

### Keep (already built, carries straight over)
- **Phase 1, Samara reads Reddit** (`src/Shell/src/reddit.ts`): it's in Samara 1.48.0, which is installed and running. It reads every 30 minutes, gently, with an RSS fallback, the top 2 comments and a 150 MB cache.
- **Phase 2, taste and mixing** (`reddittaste.ts`): opens, read time, more/less, his upvotes and saves, the dial, discovery picks with a why-line, and votes through `reddit-ask.json`.
- **The two-file bridge** (`reddit-feed.json` out, `reddit-ask.json` back) and **Open in Samara** through the open-urls drop.
- **Phase 0 lessons**: a desktop-layer window cut to its shape so clicks fall through elsewhere, game sleep (hide and suspend, 0.5%), no acrylic.
- **The native spike** (`ShrineDock/FeedSpike.cs`, f84db0c and 2001e51): a non-layered DWM window, a drift with eased stops, seamless recycling, and the measuring method. It grows into the real widget.
- **R23's Plates brass** (his rail's measured shades and uneven joints) and R26's three-window trick: the frame is built once per post and grows by layout alone.
- **Every design decision** in memory (`shrine-reddit-feed-interview`).

### Change
- **Phase 3 + 4 become one native build.** The HTML sheets are the spec, not the code. Philosopher and Fira Sans ship with ShrineDock as font files.
- **Drift is continuous and smooth** (not stepped glides). This reverses the old "Glide only" decision. Natively it measured 3.1% at 60 fps and 2.5% at 30 fps. The target is 60 fps under 3%, through Windows.UI.Composition so the compositor runs it; 30 fps is the fallback.
- **Away, not mouse-leave, makes the still slate**: after 3 minutes with no input. Leaving the feed only folds an open post after 4 s, and the river keeps drifting.
- **Buttons**: inlaid + and − for more/less like this, brass studs for up and down votes, Samara's real lantern icon (unlit, lit on hover). Every glyph is measured centred and placed on whole pixels.
- **Long posts** read inside the opened post (262 px), with soft edges and a brass reading line. Comments and votes stay in view.
- **Video**: native playback (WPF MediaElement, muted, only the post in focus). If that turns out heavy, video opens in Samara instead.

### Scrap
- The WebView2 widget, the Phase 0 proof page and the shared WebView2 environment idea.
- R18's pixel rail, spiral knobs and corner arcs. R26's vector Plates and round knobs replace them.
- Stepped glides (R24) and folding to the slate when the mouse leaves.
- The right-click More/Less menu: it was too hidden, and the inlaid +/− replace it.

### New phases
| # | Phase | Est. |
|---|---|---|
| A | Native core: the window, smooth drift under 3% (Composition), recycling, posts from `reddit-feed.json`, fonts, three post shapes | 10 h |
| B | Hover rise, Plates brass, inlaid +/−, Samara icon, opened post (three depths, long-read, comments, stud votes to `reddit-ask.json`) | 10 h |
| C | Away slate, quiet in games and full-screen video, saved position, Lock widgets, top-rail drag | 5 h |
| D | Feed tab in ShrineDock Settings (dial, block list, size, speed, sound) | 4 h |
| E | Video in the post in focus | 3 h |
| F | Ship and check live, never while he's playing | 3 h |

**About 35 hours.** Other sessions are also committing to ShrineDock (the dock, Start), so the feed work stays in its own files (`Feed*.cs`) and is built one change at a time.

### Reddit sign-in
He says Samara is signed in. I couldn't read his `%APPDATA%` from here to confirm it (my view of it is a sandboxed copy). The first live read will show it: `reddit-feed.json` gets `signedIn` and `user`.
