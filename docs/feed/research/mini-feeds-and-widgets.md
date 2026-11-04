# Mini Reddit feeds and widgets: UI/UX survey

Date: 2026-11-04. For the ShrineDock Reddit feed (Sheikah slate, blue glow, brass frame; drifts, folds to ~1.5 posts when idle, grows a post in place on click).

Method: GitHub API (stars, last push), READMEs, templates/source, and the README screenshots and GIFs looked at directly. No headless browsers.

## One technical fact that shapes the design

Raycast's Reddit extension README (2026) says Reddit has blocked anonymous access to its JSON API. The public Atom (RSS) feed still works, but at about **one request per minute**. oh-my-reddit says the same: anonymous access hits lots of 429/403. Glance warns that VPS IPs get 403.

So: a slow, ambient feed is not just a style choice. It fits the data. Fetch rarely, cache, and release posts slowly from a queue.

---

## The four Joshua found

### Readit (LTSENG01/Readit)
- https://github.com/LTSENG01/Readit · 0 stars · last push 2020-05.
- Static page: a subreddit box at the top, then a 3-column grid of Bootstrap cards. Each card is a big image, then the title, then "by u/name".
- Follows OS dark mode. Animate.css brings new posts in. "Load more" button. Shakes the box on a bad subreddit name.
- Small space: does not fit. On a phone it is just one tall column of cards.
- Verdict: **generic. Do not copy.** It's a photo wall with a card under each image. The only useful idea is the error shake on the input.

### NightClover-code/mini-reddit
- https://github.com/NightClover-code/mini-reddit · 4 stars · last push 2024-07.
- Next.js + Next UI + Tailwind clone. README is setup only, no screenshots.
- Verdict: **stock component-library clone. Nothing to take.**

### JuniFruit/mini-reddit
- https://github.com/JuniFruit/mini-reddit · 1 star · last push 2023-02.
- React/Redux "shorter replica" of Reddit: vote, comment, subscribe, search. Demo at mini-reddit.onrender.com (sleeps when idle).
- Verdict: **full Reddit clone, not a feed. Nothing to take.**

### dvdbng/redditfeed
- https://github.com/dvdbng/redditfeed · 5 stars · last push 2019-07.
- Not a UI at all. It's a PHP service that turns a subreddit into a better RSS feed. It pulls the full article text (Readability), embeds media, adds the top comments, and **drops posts below a score threshold**. It caches articles.
- Verdict: **the idea is good, the UI is absent.** Two things worth taking: (1) a score threshold so only posts that have proved themselves get into an always-on feed, and (2) attach the top comment to the post so "grow in place" has something to show.

---

## Going wide

### Glance (glanceapp/glance) Reddit widget
- https://github.com/glanceapp/glance · ~38,000 stars · pushed 2026-11-02. The best-designed of the dashboard widgets.
- Three styles: `vertical-list` (default), `horizontal-cards`, and `vertical-cards`. The docs say the first two are for wide columns and `vertical-cards` is for small columns.
- Per post (list): title on one line, then a small grey row: "7h · 1,691 points · 231 comments · domain ↗". Header is a small all-caps "/R/TECHNOLOGY". Monospace-ish font, warm title colour on near-black.
- `vertical-cards`: **the thumbnail becomes a dark, faded backdrop behind the text**, not a block above it. Source domain on top in the accent colour, title clamped to 3 lines, then "2h · 115 points". This is the cleverest small-space trick in the survey.
- Folding: `collapse-after: 5` shows 5, then a quiet "SHOW MORE ⌄" text link. `-1` never collapses.
- Read state: the title class is `color-primary-if-not-visited`, so a post you opened turns grey. No badges, just colour.
- No thumbnail? It draws a thin line icon (link / speech bubble / crosspost) in the same slot, so rows stay aligned.
- On narrow screens the comment count hides itself first when there's a link (`forum-post-autohide`). It drops the least useful thing first.
- Refresh: `cache: 5m` style. Errors: **a small red dot next to the widget title**, nothing else. Quiet.
- Verdict: **worth studying.** Calm, dense, no buttons. But it is still "a list of rows", and it doesn't move.

### Homepage (gethomepage) and Dashy
- Homepage: https://github.com/gethomepage/homepage · ~33,500 stars. **No Reddit widget.** You'd use its generic custom-API widget, which renders a key/value block. Nothing to learn.
- Dashy: https://github.com/lissy93/dashy · ~26,900 stars. **No dedicated Reddit widget.** People use its RSS widget, which is a plain list of title + date. Nothing to learn.

### Rainmeter (Windows)
- SmileyDrag0n/RSSddit (https://github.com/SmileyDrag0n/RSSddit) · 11 stars · pushed 2025-06. Reddit RSS skin: "r/sub | Title" on one line, click opens. Dark/light, adjustable font and spacing.
- william1888/reddit-rainmeter-rss · 2 stars · 2018. Same idea, older.
- Verdict: the one-line "r/sub | title" form is the right density for a desktop skin, and that's all these show. They're static text lists with no motion.

### Übersicht (macOS)
- Only tiny counters: z3t0/reddit-karma.widget (karma number), sammosna/RedditMembers.widget (member count). Nobody built a real feed. Nothing to learn, except that a single number is a valid "folded" state.

### KDE Plasma
- omartrj/plasma-reddit-feeder (https://github.com/omartrj/plasma-reddit-feeder) · 3 stars · pushed 2026-03. Plasma 6 feed: subreddit tabs on top, sort dropdown, thumbnails. **You choose which details to show** per post (author, upvotes, comments, tags, NSFW/spoiler). Conventional, but the per-field toggles are a good settings idea.
- **Raindancer118/showerthoughts-plasmoid** (https://github.com/Raindancer118/showerthoughts-plasmoid) · 0 stars · pushed 2026-05. The most "ambient" Reddit widget found. From its QML:
  - Fully transparent, typography only. Italic serif title (Noto Serif, 18-20px), shrinks to fit (`Text.Fit`).
  - A giant 88px quote mark behind the text at 15% opacity, as decoration.
  - Meta line at 11px, white at 45% opacity. Divider at 30%.
  - Changes post on a timer (default 300 s) with a **350 ms fade out, 350 ms fade in** (InOutQuad).
  - **Single click = next post. Double click = open in browser.** It waits 220 ms to tell them apart.
  - Verdict: **worth copying the feel.** One post, big type, slow crossfade, nothing else.

### GNOME Shell
- Searched extensions.gnome.org: no Reddit feed extension exists. Only wallpaper downloaders (Tunous/Waller, Random Wallpaper). Nothing to learn.

### Conky (Linux)
- knjmooney/reddit_pull · 2 stars · 2025-01, jspc/conky_reddit · 2015. A Python script prints titles, Conky draws them as text on the wallpaper. Plain text list, no interaction. Interesting only because Conky text sits *on* the wallpaper with no box at all.

### Raycast
- raycast/extensions `extensions/reddit-search` (https://github.com/raycast/extensions/tree/main/extensions/reddit-search). Search, not a feed. Useful details:
  - Searches on Enter, not per keystroke, to save requests.
  - Caches for 5 minutes. Refresh is a key (Cmd+R).
  - **Shows a live cooldown in seconds** when the rate limit hits, shared across commands. Honest about why it's waiting.
  - Every view has "Show all results on Reddit" as the way out.

### Browser new-tab extensions
- ankitsultana/reddit-new-tab · 8 stars · 2018. **One post per new tab** from a subreddit you pick. Others (melkuo/Tabbit, GOSHROW/reddit-wallpaper-extension) just set a Reddit image as the background.
- Verdict: the "one post, one moment" idea matches the folded state. The UI itself is plain.

### Electron/Tauri desktop readers
- nantaphop/redd · 50 stars · 2017. Work-in-progress three-pane client.
- Leoglme/tauri-reddit-mobile-app · 7 stars, sunaipa5/reddit-windows-app · 5 stars. Full clients.
- kylegrantlucas/readingit, walleXD/zapper, jasonbronson/arjentum: 0 stars, abandoned.
- Verdict: **all generic full-app clones. Nothing for a widget.**

### Menubar / tray
- maxkrieger/alienbox · 16 stars · 2015: menubar inbox checker.
- 0Arian/alienmenu · 1 star · 2020: menubar dropdown that is just a list of subreddit names in caps, plus a bright green input. Generic.
- nov1n/BlogFeed · 4 stars · 2014: Reddit + HN headlines in the tray.
- PhillipTaylor/reddit_monitor (2009), George-lewis/reddit-mailer: tray icon that lights up on new mail.
- Verdict: the only good idea is the oldest one. **The icon itself is the notification.** Nothing to see until something is worth seeing.

### Android widget (for completeness)
- micwallace/reddinator · 37 stars · 2023. Home-screen widget. Per-widget themes, auto-refresh interval you can turn off, choose what a tap does (open in-app view, open link, or open Reddit). Vote from the widget. Functional, not pretty.

### Minimal web readers (the "reddit minimal" crowd)
There are hundreds of Codecademy "Reddit Minimal" React/Redux projects. Almost all are the same: search bar, subreddit sidebar, white cards with vote arrows. **Skip them all.** The few with real design thinking:
- **berbaquero/reeddit** (https://github.com/berbaquero/reeddit) · 258 stars · pushed 2026-07. "Channels" group several subreddits into one stream. **No upvotes or points shown at all.** Read-only. Three elastic layouts. Good proof that scores aren't needed.
- **dhg/Redditate** (https://github.com/dhg/Redditate) · 142 stars · 2021. Toggle between a full view and a list view. Images and video expand inline when clicked. Keyboard navigation.

### Terminal clients (density and keyboard)
- **michael-lazar/rtv** (https://github.com/michael-lazar/rtv) · 4,641 stars · archived 2023. Numbered posts. Per post: bold title, then link in blue, then "14471 pts · 4hr · 899 comments", then author and /r/sub in a third colour. The selected post gets **a thick bar down its left edge**, not a highlighted box. Sort tabs as `[1]hot [2]top`. All key hints live in one bottom bar.
- tonymajestro/reddit-tui · 236 stars · 2025-05. Bubbletea, vim keys, config file for subreddit filters.
- harryfrzz/re-tui · 15 stars · 2026-01. j/k move, l/Enter open or **expand a comment**, h/Esc back or collapse. Clean open/close pairing.
- emarkou/devskim · 28 stars · 2026-05. HN + Reddit + lobste.rs in one feed. **Colour-coded by source.** **Posts you've seen dim automatically.**
- **renatoworks/oh-my-reddit** (https://github.com/renatoworks/oh-my-reddit) · 145 stars · 2026-06. **The best design in the whole survey, and the closest to what Joshua wants.** From its GIF and source:
  - Live thread view: comments arrive one at a time from a queue. **"releaseDelay drains a backlog fast but trickles gracefully when caught up."** More than 8 waiting = fast, otherwise slow.
  - First load is capped "so launch fills quickly rather than trickling for minutes".
  - Each new item fades in over 0.22 s, staggered 28 ms apart. It fades from the background colour, so text never flashes.
  - Only repaints while something is actually fading. Idle = no CPU.
  - Older comments **fade toward grey** as they age; the newest are bright.
  - Each comment has a **thin coloured bar on its left edge**, not a card. Name and score in the accent colour, then the body. A reply shows a one-line "↳ parent: …" quote in italics above it.
  - Footer: "6 shown · 6/min · [activity sparkline] · updated just now · 24 queued", plus a "LIVE" mark at top right. The sparkline uses fixed 30-second buckets so old bars never shift.
  - `p` pauses the flow.
  - Thread list: a "HIGHLIGHTS" section above "HOT", one line per title, truncated with "…", comment count right-aligned in grey. Selected row = left bar + accent colour.
  - Footer key hints separated by middle dots, in dim grey.

---

## The 10 strongest patterns for a small always-on widget

1. **Queue and release, don't refresh.** Fetch rarely (Reddit allows about once a minute anyway), put new posts in a queue, and let them in one at a time. Fast when backed up, slow trickle when caught up. (oh-my-reddit)
2. **Age by fading.** The newest post is brightest. Older ones sink toward the slate colour. Seen posts dim. No "read" badges. (oh-my-reddit, devskim, Glance's visited colour)
3. **One post, big, crossfaded.** The folded state can be a single title in large type that crossfades every few minutes (350 ms out, 350 ms in). (showerthoughts plasmoid, reddit-new-tab)
4. **Image as backdrop, not block.** If a post has a picture, darken it and put it *behind* the title, so it adds mood without taking space. (Glance vertical-cards)
5. **Left-edge bar instead of a box.** Mark the selected or new post with a thin glowing line on its left, not a highlighted card. That suits a Sheikah blue glow. (rtv, oh-my-reddit)
6. **One quiet meta line.** "2h · 115 pts · 40 💬 · domain", small and dim, joined with middle dots. Drop the least useful item first when space runs out. (Glance, rtv)
7. **Show the queue, not a spinner.** A small "3 waiting" or a tiny activity sparkline tells you it's alive. Errors are a single small dot. Rate-limit waits show seconds left. (oh-my-reddit, Glance, Raycast)
8. **Grow in place, with the top comment.** Clicking expands the same post where it sits, showing a little body text and the top one or two comments, with a clear way out to Reddit. (redditfeed's top-comment idea, Redditate inline expand, re-tui's open/collapse pair)
9. **Earned entry.** Only posts above a score threshold, or "highlights", get into an always-on feed, so it isn't noisy. (redditfeed, oh-my-reddit HIGHLIGHTS)
10. **Pause and click rules that are tiny and fixed.** Click = expand, click again = fold (press-again rule). A pause key or hover pauses the drift. Double click or a small ↗ opens in the browser. (showerthoughts, oh-my-reddit `p`)

Bonus: colour-code the source subreddit with a small glyph or tint instead of an "r/name" label on every row (devskim). Only redraw while something moves (oh-my-reddit).

## The 5 most common mistakes ("AI slop" tells)

1. **A vertical stack of identical rounded cards**, each with an image block on top and text below. (Readit, every Codecademy clone, Electron clients.) It reads as a web page in a frame.
2. **The avatar + "u/name · r/sub · 5h" header row** on every post. It costs a whole line and nobody needs the author on an ambient feed.
3. **Vote arrows, pill buttons and action bars** (upvote/downvote, share, save, hide) on a read-only glance widget. They add clutter and invite clicks the widget can't handle well.
4. **A search box or subreddit input as the first thing you see**, often a bright accent colour (alienmenu's green box, Readit's top bar). Settings belong behind a click.
5. **Refresh by reload: spinner, then the whole list swaps.** No sense of time passing, no motion between states, plus "Load more" buttons. The opposite of drift.

Also common: big all-caps subreddit lists as navigation, Bootstrap/Next UI defaults, and the same tiny grey meta text with no hierarchy.

## Short list worth opening

- oh-my-reddit (GIF in README): pacing, fading, footer status.
- Glance `vertical-cards` (docs/images/reddit-widget-vertical-cards-preview.png): backdrop thumbnails in a narrow column.
- showerthoughts-plasmoid `plasmoid/contents/ui/main.qml`: one-post crossfade and click rules.
- rtv demo GIF: density and the left-edge selection bar.
