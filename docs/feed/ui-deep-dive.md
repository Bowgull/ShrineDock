# Reddit feed UI deep dive (2026-11-04)

What every Reddit feed on GitHub does, and how it fits the ShrineDock feed with Breath of the Wild's UI rules.
Notes: `research_notes/Reddit feed UI deep dive/` (clients.md, mini-feeds-and-widgets.md, media-and-ambient.md, botw-ui-ux.md).

## The short version
- About 80 projects were looked at. Almost none is a good always-on feed. Most are clones made of stock cards.
- **The 4 he found are not worth copying.** Readit and both mini-reddits are generic clones. dvdbng/redditfeed is an RSS tool; two ideas from it are worth taking: a score threshold, and attaching the top comment to each post.
- The best ideas come from 6 places: **oh-my-reddit** (a terminal reader), **Current** (a feed reader where posts age out), **Glance**, the **showerthoughts** KDE widget, **RES**, and **Infinity / Apollo / Voyager**.
- **BotW's rule** (Nintendo's own 2017 talk): *only essential UI*. Keep it small, add life through motion, use colour only to mean something, and track one thing at a time.

## 1. BotW behaviours mapped onto the feed
| BotW does | The feed does | Where it shows |
|---|---|---|
| Colour only means state (map: orange found, blue done) | blue = new, warm = for you/discovery, gold = in focus or saved. Nothing else is coloured | always |
| "New" sparkle that clears when the cursor passes | a small spark on a new post that clears when the mouse passes | always |
| Pro HUD: hide everything you don't need | idle = fold to ~1.5 posts, no controls, no frame | always |
| One quest active at a time | you can follow one thread, and its new comments trickle in | always |
| Sheikah Sensor ping | one quiet ping when your one chosen discovery topic turns up. Muted in games | always, rare |
| Item-get: a big card the first time, a toast after | the first save shows a small ceremony; after that, a tiny mark | on click |
| Map zoom steps: labels appear as you zoom | detail levels in fixed steps: title only → plus picture → open post | always |
| Loading screen previews where you're going | a refresh lets the next post peek in, never a spinner | always |
| Adventure Log: list on one side, detail on the other | open post: title, body, then 2 comments, getting deeper down the page (R18's 3 levels) | open |
| Compendium: numbered entries made of your own finds | saved posts become a "Compendium" page in Settings | later |
| Pages slide in the direction you're going | changing sub or mode slides that way | open |
| Remembers where you were | comes back to the same post, and new posts wait above it | always |

Too much for a widget: typewriter text, big reward scenes every time, a sensor that keeps beeping, an always-visible grid.

## 2. The strongest feed patterns from GitHub
1. **Posts come in from a queue** instead of the list being reloaded (oh-my-reddit). They let posts in one at a time, slowly. This fits Reddit's limits too: from the Shadow, signed-out reading is RSS only, about once a minute.
2. **Posts age at their own speed** (Current): each sub has its own lifespan, and old posts dim and drift off. No unread counts.
3. **Glow means hot** (Emberfall idea): score and comments warm the rim instead of showing numbers.
4. **A glowing left-edge line instead of a box** (oh-my-reddit, rtv). The post in focus gets the bright line.
5. **One focus marker that moves** (RES's j/k box). In our look, a soft blue outline that slides.
6. **Fold to one big post that crossfades** (showerthoughts widget, Narwhal). One click: next post. Double-click: open.
7. **The picture behind the title**, darkened (Glance), as a choice beside the full-width picture.
8. **Clicks pass through to the desktop until you hold Ctrl** (SRPSS screensaver). No stray clicks while gaming.
9. **"2 newer ↑" instead of number badges** (Reeder, RES's divider). Already in R04/R19.
10. **Only good posts get in** (Upvote RSS / dvdbng): a score threshold per sub.
11. **Video plays only when hovered, muted** (troddit, Redlib).
12. **Small post-type glyphs** (Boost's video/gallery/link badge), drawn as our own runes.
13. **Blur spoilers and NSFW** (Redlib, Infinity).
14. **Hide scores and awards** (Redlib's switches). R19 already does.
15. **Themes saved as files with a live preview** (Photon, Winston). Fits his "editable themes" rule.

## 3. The slop tells to never ship
- a stack of the same rounded cards with the picture on top
- an avatar + "u/name · r/sub · 5h" row on every post
- vote arrows and pill buttons always showing
- a search box first
- a spinner, then the whole list swapped; "Load more" / "Next page" buttons
- phone swipe gestures, masonry grids in a narrow column, autoplay with sound

## 4. Three directions (he said redesigning anything is fine)
- **A · River** (R19 evolved): the drift stays. Posts come in from the queue one at a time and age out by sub. The left edge is a glowing line, not a box. Glow means hot. Spark for new. Keeps everything already built.
- **B · Slate** (one at a time): the widget shows one big post, like a Compendium entry, with the next one peeking in. It crossfades every so often, or on click. It barely uses any CPU (no drift), so the 17% problem goes away.
- **C · Sensor** (most ambient): almost invisible. Clicks pass through, and only titles show, glowing faintly. Holding Ctrl wakes it into A. The discovery ping is the only sound.
- **Advice:** A + B's idea. Keep R19's river, and when idle, fold to B's single big post (his liked "1.5 posts" fold, now crossfading instead of drifting). That also fixes CPU: no drift while idle.

## 5. Tech notes
- Animate only position and fade. Run the drift at 30 fps or use stepped moves. Keep only a handful of post boxes alive and reuse them.
- Preload the media for the next ~2 posts. Videos start muted. Reddit's video and sound come as separate files.
- Follow the system's "reduce motion" setting.
- Never copy Nintendo glyphs, fonts or icons. The GitHub BotW UI repos do, so take only their measured colours and spacing ideas.
