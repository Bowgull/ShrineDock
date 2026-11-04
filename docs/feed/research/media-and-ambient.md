# Media-first and ambient feeds: what to borrow

Researched 2026-11-04 for the ShrineDock Reddit feed (Sheikah slate look: drifts up, folds when idle, grows a post in place, momentum that settles on a post).

Method: GitHub search sorted by stars, then READMEs and a few reviews. No headless browsers. The GitHub search limit ran out partway, so "reddit visualization", "zen reader" and "video autoplay" searches did not run. Stars and dates are from 2026-11-04.

Short verdict: almost nothing on GitHub is a good *ambient* Reddit feed. The best motion ideas come from slideshow viewers (redditp family), TikTok-style swipers, screensavers, and two non-Reddit readers (Current and Reeder). The map and 3D projects give good "place" ideas, not feed ideas.

---

## 1. Slideshow viewers

### redditp — github.com/ubershmekel/redditp
- 288 stars, pushed 2026-11-04 (alive).
- Idea: turn any Reddit page into a full-screen slideshow. Now mainly a browser extension that runs on Reddit's own page, inside your logged-in session.
- Clever: one-key controls. A = auto-advance, T = hide title, P = hide controls, F = full screen, G = skip rest of a gallery, M = sound, C = comments. Hiding the chrome is a first-class feature.
- Fails: it is a viewer, not a glance tool. Nothing for text posts. Timing is a gear menu, not something that adapts.

### redditpx — github.com/saihgupr/redditpx
- 3 stars, pushed 2026-09-28. Fork of jeffjose's redditpx, which forks redditp. Svelte.
- Clever:
  - UI starts hidden. Cursor hides with it. UI comes back only when the mouse moves (optional).
  - A thin horizontal timeline at the bottom auto-scrolls to keep the current item centred. You always see "where am I" without a list.
  - Hover preview on the grid.
  - Two axes: left/right = posts, up/down = inside an album.
- Fails: tiny project, no motion design beyond the carousel.

### reddit-slideshow (ismaelpadilla, 8 stars, 2023), UWP-RedditSlideshow (4 stars, 2017), reddit-slides (2010)
- Same idea, smaller. Nothing new.

### reddit.tv — github.com/octatone/reddit.tv
- 62 stars, last push 2017. Dead.
- Idea: subreddits as TV channels. Video posts play back to back, Reddit API only.
- Clever: "channels" is a strong mental model. You flick channels, not scroll.
- Fails: abandoned. Embeds break.

### reddit-gallery-viewer — github.com/JamesIves/reddit-gallery-viewer
- 10 stars, pushed 2026-08. Angular + RxJS.
- Idea: a never-ending stream of media with virtual scrolling.
- Lesson: the README says it **no longer works** because Reddit locked down open API access. Any feed must plan for auth and rate limits.

## 2. TikTok / Tinder style swipers

### reddit-swipe — github.com/mahmudjaber/reddit-swipe
- 1 star, pushed 2026-10-14. Small, but the best-documented swiper.
- Idea: one post fills the screen. Swipe up for the next.
- Clever:
  - Galleries become a sideways carousel with dots and a counter, inside the vertical feed.
  - A single image sits on a **blurred copy of itself** as a backdrop. No empty bars.
  - Videos autoplay muted (browsers require it). One speaker button turns sound on.
  - **Videos load only when within about two swipes.** A spinner shows while buffering.
  - Subreddit chips at the top filter the feed.
- Fails: phone-shaped. One post per screen is too much for a side widget.

### SwipeIt — github.com/ivanbruel/SwipeIt
- 440 stars, last push 2017. iOS, Tinder-style cards (ZLSwipeableView).
- Really an architecture demo (MVVM vs MVC). Card stack idea is fine, little else.

### diaporama (Flutter, 23 stars, 2020), redditSwipe (2015), Memeswipe (2026, 1 star)
- Same card-swipe pattern. Memeswipe mixes Reddit and Lemmy.

## 3. Screensavers, kiosks, tiny screens, e-ink

### ShittyRandomPhotoScreenSaver (SRPSS) — github.com/Basjohn/ShittyRandomPhotoScreenSaver
- 6 stars, pushed 2026-11-03. Windows, custom OpenGL engine.
- Idea: a photo screensaver with live widgets (Reddit, music, Steam, weather, Gmail) over the pictures.
- Clever:
  - Two subreddit widgets sit on top of the slideshow.
  - **Clicks pass through by default.** You hold Ctrl to show an "interaction halo", then click. Perfect fit for a desktop widget that must not steal clicks.
  - Clicking a post opens its comments, not the linked article.
  - Edit mode with snapping, undo, and ten saved layout slots.
- Fails: kitchen-sink. No Reddit-specific motion.

### Suspended Animation — github.com/ankitshekhawat/SuspendedAnimation
- 4 stars, 2020. macOS screensaver.
- Idea: plays cinemagraphs (still photo, one small moving part) from r/cinemagraphs and r/perfectloops.
- Clever: picks subreddits where the content *is already calm*. Downloads files locally first, so playback never stutters. Reads the subreddit's RSS feed, not the API.
- Fails: no transitions between clips yet.

### Reddit-Roku-Screensaver (2013), reddit-screensaver for Linux (2016), opus1269/screensaver (Chrome, 27 stars, 2023)
- Photo screensavers with Reddit as a source. Ken Burns pan, crossfade. Nothing new.

### oleddit — github.com/baranonen/oleddit
- 21 stars, 2020. Reddit on a 0.96 inch OLED on a Raspberry Pi.
- Clever: uses pixel fonts (VCR OSD Mono, Coder's Crux). Shows how little you need: a title and a sub name.

### reddit-for-eink (7 stars, 2018), Reddle for KOReader (2026), reddit2eink (2020)
- Read-only proxies that strip Reddit down for e-ink. Lesson: text-only, high contrast, no motion still reads well.

### navapp (1 star, 2018)
- Kiosk that loops URLs and pulls subreddit pictures. Nothing new.

## 4. Maps, 3D, games

### map-of-reddit — github.com/anvaka/map-of-reddit
- 856 stars, pushed 2025-12. The biggest project here.
- Idea: subreddits drawn as countries on a map. Built from 176 million comments; two subs are close when the same people comment in both.
- Clever: custom WebGL renderer with a streaming SVG parser, so a huge map stays smooth. "Show Related" dims everything except a sub's first and second-degree neighbours.
- (From memory, not re-checked: labels appear as you zoom in, like a real atlas.)
- Fails: a place to explore, not a feed. Author warns that closeness on the map can be layout noise.
- Borrow: the "Sheikah map" feel. A sub could have a home on a small region map; new posts ping on their region.

### Infinite-Art-Gallery — github.com/nowaythisworks/Infinite-Art-Gallery
- 37 stars, 2022. Three.js.
- Idea: walk an endless gallery. r/Art posts hang in frames with plaques.
- Clever: **paced arrival.** Each second there is a 10% chance a new piece appears, then a 5 second cooldown. New art spawns about 30 metres ahead, so it is already in place when you reach it.
- Fails: its middle server falls over under load.

### Emberfall — github.com/git-agent-swarm/emberfall (0 stars, 2026, Devvit game)
- Real posts become glowing monuments in the level.
- Clever mapping: **score = height, comment count = glow brightness.** The day's colour grade comes from the top post's thumbnail.
- Borrow: show "hot" with glow, not with numbers. That is very Sheikah.

### PlaceViewer (383 stars, VR r/place), replace (WebGL r/place scrubber), leapReddit3D (2013, CSS3D)
- r/place timelapses and an old 3D browser. Not feeds.

### Cover Flow / carousel
- Searches found nothing real for Reddit. Only scroll-snap test repos.

### News tickers
- "Reddit ticker" on GitHub means stock tickers from r/wallstreetbets. No real Reddit headline ticker exists.

## 5. Readers with great motion (non-Reddit)

### Current (Terry Godier, Feb 2026, iOS/Mac, closed source) — terrygodier.com/current
- Idea: a river, not an inbox. Items arrive, linger, dim, then drift away. Nothing is marked read. No unread counts.
- Clever:
  - Each source has a **half-life**: Breaking about 3 h, News about 18 h, Essay about 3 days, Evergreen about a week. A busy site's posts leave fast; a rare essay stays.
  - Dimming is the main age cue. Less active people fade to greyscale.
  - Drag a card: it compresses a little, haptics speed up near the release point, a warm glow shows at the edge. Past the point it flies off, the rest settle into the gap, then a short undo.
  - Five slow sine-wave lines drift behind everything at **30 fps**. Transitions are 0.15 to 0.45 s. Reduce Motion stops all of it.
  - "Counting was the problem."
- Fails: one plain list of cards. The idea is better than the look.

### Reeder (new, 2024+) — macstories.net review
- Idea: one timeline for RSS, video, podcasts, social. No unread counts. It remembers your position; new items fill in **above** you, synced across devices.
- Clever: every media type gets the same item shape (source + favicon, then preview). Video plays inline and can pop out. A partial ring around the play button shows how far you got.
- Fails: users say position memory is flaky; "Later" list is hard to manage.

### Readwise Reader
- Triage modelled on Superhuman's game-like inbox clearing. Teal dots mark unseen feed items. Nothing documented about motion. Little to borrow beyond "small dot = new".

### River-of-news readers (Infoscope, rivelin, lylina)
- Tiny projects. Infoscope's aim, "reduce information anxiety", matches Current.

---

## The 10 strongest ideas for ShrineDock

1. **Posts age by half-life (Current).** Each sub gets a speed. r/news fades in hours; a slow sub's post lingers for days. Old posts dim to grey-blue then drift off the top. No unread counts, ever.
2. **Glow = heat (Emberfall).** Upvotes and comments become brightness of the Sheikah blue rim, not numbers. A hot post literally glows. Numbers only show when the post grows open.
3. **Paced arrival (Infinite Art Gallery).** New posts do not dump in. They arrive one at a time with a cooldown, so the drift never jumps while he glances.
4. **Click-through by default, hold a key to touch (SRPSS).** The widget lets clicks fall to the desktop. Holding Ctrl (or hovering for a beat) lights a halo and makes it touchable. Stops stray clicks while gaming.
5. **Chrome that hides itself (redditpx).** Titles and controls fade out when idle; the cursor hides over it. Mouse movement brings them back. This is the "fold when idle" state.
6. **A "where am I" strip (redditpx timeline).** A thin row of tiny glyphs along one edge, current post centred. Fits the slate frame and replaces a scrollbar.
7. **Blurred self as backdrop (reddit-swipe).** When a post grows open, its image fills the slate with a darkened, blurred copy of itself behind the sharp one. Never grey bars.
8. **Two axes (redditpx, reddit-swipe).** Up/down = posts. Left/right inside a grown post = gallery pages, with Sheikah dots and a counter.
9. **New items fill in above, position kept (Reeder).** When he comes back, he lands where he left off; newer posts wait above, marked with a small glyph. Matches the upward drift.
10. **Calm sources and calm background (Suspended Animation, Current).** Offer a "calm mode" fed from cinemagraph/loop subs, and a very slow background motion (Current's sine lines → faint Sheikah circuit lines) at low frame rate.

Honourable mentions: subreddits as **channels** (reddit.tv) for a quick flick between feeds; a **region map** of his subs (map-of-reddit) where new posts ping like shrine markers; **pixel fonts** for tiny sizes (oleddit).

## Technical tips the projects document (or clearly imply)

- **Video autoplay must start muted.** Browsers block sound until a user gesture (reddit-swipe). One clear speaker toggle; remember the choice.
- **Reddit video audio is a separate track.** reddit-swipe uses Reddit's HLS stream on iOS Safari and a synced audio track elsewhere. redditpx had to fix v.redd.it audio. Plan for this from day one.
- **Load media only near view.** reddit-swipe loads video within about two swipes. For a slow drift: preload the next 2 to 3 images, keep video as a poster frame until the post grows open.
- **Download before showing (Suspended Animation).** For an always-on widget, cache media locally so the drift never stalls on the network.
- **Low frame rate for ambient motion.** Current runs its background lines at 30 fps. The drift can run even slower; settle animations at full rate only when he touches it.
- **Short transitions.** Current uses 0.15 to 0.45 s. Respect Reduce Motion (stop all ambient motion).
- **Compositor-only animation (general practice, not from these READMEs).** Move things with `transform` and `opacity` only. Blur on a pre-made smaller image, not a live `filter: blur` over moving content. Pause everything when the widget is hidden or a game is in front.
- **Virtual scrolling (JamesIves).** Keep only a handful of post elements alive and recycle them as they drift off.
- **Hide the cursor with the UI (redditpx).**
- **API access is fragile.** JamesIves' viewer broke when Reddit closed open access. redditp moved to running inside Reddit's own logged-in page. Suspended Animation uses RSS feeds. Have a fallback source.
- **Big visual data needs a custom renderer.** map-of-reddit wrote its own WebGL renderer and streaming parser. Not needed for a feed, but shows where DOM stops scaling.

## Sources
- https://github.com/ubershmekel/redditp
- https://github.com/saihgupr/redditpx
- https://github.com/octatone/reddit.tv
- https://github.com/JamesIves/reddit-gallery-viewer
- https://github.com/mahmudjaber/reddit-swipe
- https://github.com/ivanbruel/SwipeIt
- https://github.com/Basjohn/ShittyRandomPhotoScreenSaver
- https://github.com/ankitshekhawat/SuspendedAnimation
- https://github.com/baranonen/oleddit
- https://github.com/blchinezu/reddit-for-eink
- https://github.com/anvaka/map-of-reddit
- https://github.com/nowaythisworks/Infinite-Art-Gallery
- https://github.com/git-agent-swarm/emberfall
- https://terrygodier.com/current
- https://www.techcrunch.com/2026/02/19/current-is-a-new-rss-reader-thats-more-like-a-river-than-an-inbox/
- https://macstories.net/review/reeder-a-new-approach-to-following-feeds
- https://readwise.io/reader/update-aug2026
