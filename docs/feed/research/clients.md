# Reddit and Lemmy clients: feed UI deep dive

Checked 2026-11-04. Stars and last push come from the GitHub API that day.
Feature notes come from each README where I could read it (marked "README").
The rest is from known app behaviour (marked "known"). GitHub rate-limited me
part way, so a few details are from memory, not re-checked today.

Focus: how the feed looks and moves. Not the API (already covered in
`research_notes/Reddit feed safe reading/`).

---

## 1. The projects

### Web front ends

**Redlib** (fork of Libreddit) - https://github.com/redlib-org/redlib
- 3,967 stars, pushed 2026-10-22. Live and maintained.
- Libreddit itself: 5,199 stars, last push 2025-02-15. Effectively dead; Redlib took over.
- Feed: three layouts, `card`, `clean`, `compact` (README). Card is Reddit-redesign style. Compact is one line per post with a small thumbnail.
- Settings worth noting (README): blur spoiler, blur NSFW, autoplay videos off by default, hide awards, hide score, hide sidebar, "wide" mode, 18 themes.
- Votes are read-only. Score is a small number at the left of the card. Comment count is a link.
- No infinite scroll. Paging with "Next" buttons. No JavaScript needed for the core.
- Lesson: "hide score" and "hide awards" switches show how much noise a feed carries that you can drop.

**Teddit** - https://github.com/teddit-net/teddit
- 284 stars, last push 2023-07-31. Abandoned.
- Old-Reddit look: dense rows, square thumbnail on the left, title, then a grey meta line (sub, user, time, comments).
- Lesson: the old-Reddit row is still the most info per pixel. Good for a narrow widget.

**troddit** - https://github.com/burhan-syed/troddit
- 623 stars, archived (last push 2025-07-28). The best desktop-web feed ideas in this list.
- README: single column, multi-column masonry grid, or plain rows. All with infinite scroll.
- Three card styles: Original (full text), Compact (no body text), Media (no text and no padding at all, just the picture).
- Filter by post type: images, video/GIF, links, self posts.
- "Gallery view": open a post, then move through the feed with arrow keys. Comments sit to the side. Portrait media goes side by side with comments automatically.
- Hover a video to play it. Optional autoplay when it scrolls into view. Optional sound on hover.
- Lesson: the arrow-key "next post" view and hover-to-play are exactly right for a desktop widget.

**reeddit** - https://github.com/berbaquero/reeddit
- 258 stars, pushed 2026-07-20. Minimal, elastic, read-only web reader. Two panes: list on the left, post on the right.

**lurker** - https://github.com/oppiliappan/lurker
- 327 stars, pushed 2026-02-20. Self-hosted, read-only. Very plain list. Shows the "calm text-only" end of the scale.

**viewer-for-reddit** - https://github.com/gregrickaby/viewer-for-reddit
- 58 stars, pushed 2026-11-02. "No ads or algorithms." Modern card list. Small but active.

### Browser extension

**RES (Reddit Enhancement Suite)** - https://github.com/honestbleeps/Reddit-Enhancement-Suite
- 4,547 stars, pushed 2026-11-03. Still maintained (in maintenance mode, old Reddit only).
- Known feed tweaks:
  - Keyboard navigation: `j`/`k` move a highlight box down and up. The focused post gets an outline. `a`/`z` vote, `x` expands media, `Enter` opens comments. This is the classic focus model.
  - "Never Ending Reddit": infinite scroll, with a thin page divider ("Page 2") between batches so you know where you are.
  - Inline image expando: a small button next to the title opens the picture or video right in the list, no new page.
  - "Show images" button opens every expando on the page at once.
  - Filteraddit: hide posts by keyword, sub, domain, or flair.
  - Comment navigator and "hide child comments" to collapse whole threads.
  - Visited posts dimmed (old Reddit greys visited titles; RES keeps it).
  - "New comments" highlight: comments that arrived since your last visit get a coloured edge.
  - Night mode.
- Lesson: RES invented most desktop feed habits. The moving focus box and "new since last visit" marks are the two to steal.

### Android, Reddit

**Infinity for Reddit** - https://github.com/Docile-Alligator/Infinity-For-Reddit
- 5,539 stars, pushed 2026-11-04 (today). The most popular open client.
- Known layouts: Card, Card 2, Card 3, Compact, Gallery (grid of thumbnails), plus a full-screen "card view swipe" mode.
- README: "Lazy mode" auto-scrolls the feed slowly so you can read without touching it.
- Swipe left/right on a post for actions you pick (vote, save, hide).
- Read posts can be dimmed or hidden. Option to mark as read on scroll past.
- Fixed bar at the bottom for votes, comments, save, share. Score sits between the arrows.
- NSFW and spoiler blur. Video autoplay with mute by default.
- Comments: tap to collapse, coloured depth lines on the left (one colour per level).
- Lesson: "Lazy mode" is the closest thing to an always-on ambient feed. And mark-read-on-scroll is how you keep a feed fresh without effort.

**RedReader** - https://github.com/QuantumBadger/RedReader
- 2,718 stars, pushed 2026-10-10.
- README: swipe posts left and right for actions you set. Image and comment precaching. Built-in image viewer and video player. Night mode and pure black. Strong screen-reader support.
- Known: very flat, text-first list. Thumbnail on the right. A small coloured vertical bar shows vote state. Comment count is a tappable box on the right edge that opens comments directly, while tapping the post opens the link.
- Lesson: two tap targets per row (content vs comments) is simple and clear. Precaching makes opening feel instant.

**Slide** - https://github.com/Haptic-Apps/Slide
- 1,771 stars, archived (last push 2024-05-31). Slide-iOS also archived.
- Known: subreddits are pages you swipe left and right between, like tabs. Big image cards. "Shadowbox" mode: full-screen, swipe through posts one at a time, media first, comments pulled up from the bottom.
- Lesson: one-post-at-a-time "shadowbox" is the right model for a widget that cycles.

**Dawn** - https://github.com/Tunous/Dawn
- 445 stars, archived. Fork of Dank (Saket's well-known app).
- Known from Dank: swipe a post to vote/save with an icon that grows as you drag. Opening a post pulls it up as a sheet over the feed. Dragging the sheet down dismisses it and drops you back at the same scroll spot. Very smooth.
- Lesson: open as an overlay, close back to exactly where you were.

**pineapple** - https://github.com/galaxygoldfish/pineapple
- 221 stars, pushed 2026-10-31. Material You design. Small, modern, active.

**geddit** - https://github.com/kaangiray26/geddit-app
- 922 stars, last push 2024-08-23. Uses public JSON, no login. Plain card list.

### iOS and Mac

**Apollo** (closed, shut down 2023)
- Famous patterns, all known:
  - Long-swipe vs short-swipe on a post: short swipe = upvote, longer swipe = downvote. Colour changes as you pass the threshold.
  - Comments collapse by tapping anywhere on the comment, with a smooth fold. Swipe a comment for actions.
  - "Jump bar": a floating button that jumps to the next top-level comment.
  - Tiny haptic ticks on every action.
  - Thumbnail on the right in compact mode, big media in large mode.
  - Read posts dimmed.
  - Very short, springy transitions; nothing ever "pops".

**Sync for Reddit** (closed, shut down 2023)
- Known: Card, Small card, List, and a "Slides" layout. Floating action button to hide all read posts in one go. Strong use of a bottom sheet for comments.

**Boost for Reddit** (closed, shut down 2023)
- Known: very configurable cards. Swipe to hide. Pull a post right to reveal its comments. "Hide read posts" button. Post-type badges on thumbnails (play icon for video, stack icon for gallery).

**Narwhal** (closed)
- Known: the "one post at a time" client. Each post fills the screen. Swipe to the next. Comments scroll beneath. Clean typography, little chrome.
- Lesson: for a small always-on widget, one post at a time is a strong default.

**Winston** - https://github.com/lo-cafe/winston
- 1,251 stars, last push 2025-03-02. Stalled.
- Known: very styled native iOS. Heavy theming (every colour, radius and padding is editable). Smooth card transitions.
- Lesson: user-editable themes, which Joshua said he wants.

**Curiosity (RedditOS)** - https://github.com/Dimillian/RedditOS
- 4,022 stars, last push 2023-10-23. SwiftUI client for macOS.
- Known: three columns: sidebar of subs, post list, post detail. Mac-native. Shows how a desktop Reddit reads when the list stays put and the detail pane changes.

**Acorn** - https://github.com/alizahid/acorn
- 234 stars, pushed 2026-11-03. Active mobile client, React Native.

### Windows

**Baconit** - https://github.com/QuinnDamerell/Baconit
- 236 stars, last push 2022-08-17. Windows 10 client.
- Known: Fluent design. Had a "subreddit on your lock screen / live tile" feature, rotating posts as a desktop-ambient display. The only one here that tried the always-on idea.

**Carpeddit** - https://github.com/itsWindows11/Carpeddit
- 90 stars, last push 2023-07-01. Fluent design, Windows.

### Terminal

**rtv** - https://github.com/michael-lazar/rtv - 4,641 stars, last push 2023-02-20.
**reddix** - https://github.com/ck-zhang/reddix - 970 stars, pushed 2025-12-06.
- Both: keyboard-only, one highlighted row, `j`/`k` move, `l` opens, `h` goes back.
- Lesson: the cleanest focus model there is. A single highlighted row and nothing else moving.

### Lemmy and PieFed

**Voyager** - https://github.com/aeharding/voyager
- 1,830 stars, pushed 2026-10-28. The Apollo of Lemmy, by design.
- README: comment thread collapsing, "a bunch of swipe gestures", swipe to hide posts, single comment thread context view.
- Known: large and compact modes, Apollo-style short/long swipes, "mark read on scroll" and "auto-hide read posts", jump button for comments, collapse by tapping the comment.

**Thunder** - https://github.com/thunder-app/thunder
- 1,028 stars, pushed 2026-09-17.
- README: Card view and Compact view, with a live preview in settings while you change them. Custom swipe actions for posts and comments. "Show read posts" toggle.
- Lesson: a live preview of the post style inside settings. You see the change as you make it.

**Photon** - https://github.com/Xyphyn/photon
- 558 stars, pushed 2026-10-12. Web.
- README: "compact" and "cozy" layouts, infinite scroll, exportable themes as JSON.
- Known: soft, rounded, quiet design. Desktop-first. Hover reveals actions.

**Jerboa** - https://github.com/LemmyNet/jerboa
- 1,331 stars, pushed 2026-11-02. Official Lemmy Android app.
- Known: Card, Small card, List views. Comment depth bars. Simple Material look. Less polish, but clean.

**Artemis**
- Could not find an open repo by that name today (search was rate-limited). Treat it as closed or unverified. Known as an Apollo-style client with clean cards.

**Blorp** - https://github.com/Blorp-Labs/blorp - 136 stars, pushed 2026-06-11. Web, iOS and macOS. Lemmy and PieFed.
**Raccoon for Lemmy** - https://github.com/LiveFastEatTrashRaccoon/RaccoonForLemmy - 106 stars, pushed 2026-11-02. Kotlin Multiplatform.

---

## 2. Patterns worth stealing for a small always-on widget

Ranked roughly by how much they fit ShrineDock.

1. **One post at a time, with "next" and "back".**
   Best: Narwhal, Slide's shadowbox, troddit's gallery view.
   A small widget should not try to be a full list. Show one post big, peek the next one.

2. **A single moving focus marker.**
   Best: RES (`j`/`k` box), rtv/reddix.
   One highlighted post. Arrow keys or wheel move it. In Sheikah style this is the glowing blue outline that slides, not a flash.

3. **Calm auto-advance ("lazy mode").**
   Best: Infinity's Lazy mode, Baconit's rotating tile.
   The feed moves by itself slowly when idle, stops the moment the mouse is on it.

4. **Mark read as you pass, and dim it.**
   Best: Infinity, Voyager, Apollo.
   Seen posts fade a step. A "hide read" button clears them all at once (Sync, Boost).

5. **"New since you last looked" marker.**
   Best: RES new-comment edge; Never Ending Reddit page divider.
   A thin line or small glow between new posts and ones you have already seen. Better than a number badge.

6. **Media first, text optional (three card styles).**
   Best: troddit (Original / Compact / Media), Redlib (card / clean / compact), Thunder (live preview).
   Let one post show as full card, title only, or picture only.

7. **Hover to play video, muted.**
   Best: troddit. Autoplay off by default (Redlib, Infinity).
   Never make noise in an always-on widget unless asked.

8. **Post-type badge on the thumbnail.**
   Best: Boost (play icon, gallery stack icon), Infinity.
   One small glyph says "video", "gallery", "link", "text". In Sheikah style: rune-like glyphs.

9. **Open as an overlay, close back to the same spot.**
   Best: Dank/Dawn (sheet over the feed), Apollo.
   Opening never loses your place. Click outside or Esc closes it smoothly (matches Joshua's standing rule).

10. **Two click targets per post: content vs comments.**
    Best: RedReader (comment box on the right edge), old Reddit/Teddit.
    Picture opens the picture. Comment count opens the thread.

11. **Comments: depth bars plus tap-to-fold.**
    Best: Apollo, Voyager, Infinity (colour per depth level).
    Tap anywhere on a comment to fold it and its replies. Depth shown by thin lines, which can become brass lines in this theme.

12. **Jump to next top-level comment.**
    Best: Apollo's jump button, RES comment navigator.
    One key or button. Saves endless scrolling inside a small box.

13. **Blur spoilers and NSFW until clicked.**
    Best: Redlib (switches), Infinity.
    Safe for an always-visible desktop.

14. **Drop the noise: hide score, awards, sidebar.**
    Best: Redlib's HIDE_SCORE / HIDE_AWARDS.
    For a calm widget, score can be a small faint number or gone.

15. **Editable themes, previewed live.**
    Best: Winston (everything editable), Photon (export theme as JSON), Thunder (live preview).
    Matches Joshua's "editable themes, not one baked look".

## 3. What to avoid

- Big swipe gestures. They are phone habits. On desktop use wheel, arrows and hover.
- Number badges for new posts. RES-style dividers or a soft glow are calmer.
- Autoplay with sound. Nobody does it well.
- Masonry grids in a narrow widget. troddit's grid only works wide.
- Paging buttons (Redlib, Teddit). Fine for privacy front ends, wrong for an ambient feed.
