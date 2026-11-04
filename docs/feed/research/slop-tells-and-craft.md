# Slop tells and craft: river, slate, card, frame

Researched 2026-11-04 for the Shrine Reddit feed widget.
Plain English. Each claim says where it came from.
Lines marked **[my read]** are my own reading of the games, not a quote from a source.

---

## The short version

AI slop is not one bad choice. It is the *average* choice, made everywhere at once.
Same radius, same shadow, same gap, same glow, same fade-in, on every box.
Crafted UI makes a few choices on purpose, makes them unequal, and lets the content be the loudest thing.

Anthropic's own frontend-design skill says it best: "Spend your boldness in one place", and "remove one accessory" before finishing. It also says structure (borders, dividers, labels, numbers) must "encode meaning, not decorate". ([Anthropic frontend-design SKILL.md](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md))

---

## Where the tells come from

- Models learn the average of the web, which is Tailwind defaults and SaaS templates. ([Why your AI keeps building the same purple gradient website](https://prg.sh/ramblings/Why-Your-AI-Keeps-Building-the-Same-Purple-Gradient-Website))
- The tells move. After purple gradients came cream + terracotta, and near-black + one acid accent. Anthropic's skill now lists both as defaults to avoid. ([SKILL.md](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md))
- **Warning for us:** "dark slate + one glowing cyan accent" sits very close to the "near-black with a single accent" and "dark mode with glowing accents" tells. Impeccable warns glow "can turn a dark interface into a wall of neon". ([impeccable.style/slop](https://www.impeccable.style/slop)) The Sheikah glow must be rare and earned, or the whole theme reads as slop.

Main tell catalogues used:
- [Impeccable: the visible tells of AI design](https://www.impeccable.style/slop) (Paul Bakaus, built on Anthropic's skill)
- [Pixelslop AI slop pattern catalog](https://cdn.jsdelivr.net/npm/pixelslop@0.3.8/dist/skill/resources/ai-slop-patterns.md)
- [Anthropic frontend-design SKILL.md](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md)
- [Noqta: AI design slop, overused UI patterns](https://noqta.tn/en/blog/ai-design-slop-overused-ui-patterns-fix-2026), [925 Studios](https://www.925studios.co/blog/ai-slop-design-tells), [Capital and Compute: fix it tell by tell](https://capitalandcompute.net/blog/fix-ai-slop-design/)

Caveat: these lists are mostly from people who sell skills and fixes. No measured study exists. They agree with each other, though, and they match what Joshua keeps rejecting.

---

## 1. River (the scrolling feed)

### (a) Slop tells
- **A stack of identical cards.** Same size, same structure, same gap. "Gives every point the same weight." ([Impeccable](https://www.impeccable.style/slop); Pixelslop #7, S11)
- **Monotonous spacing.** "Equal gaps everywhere make it hard to tell what belongs together." ([Impeccable](https://www.impeccable.style/slop); Pixelslop #21)
- **Every item wrapped in a container.** "Container-heavy layouts with little bare content." (Pixelslop #23) This is Joshua's "boxes in boxes".
- **Fade-and-slide-up on every row, hover lift on every card.** Anthropic's skill names these exactly as things that "read as AI-generated". ([SKILL.md](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md))
- **Template meta strings.** "A · B · C" middle-dot lines, tracked-out ALL-CAPS eyebrows over every title, monospace for small labels. ([SKILL.md](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md)) A Reddit row of `r/sub · 4h · 2.1k` in mono caps is exactly this.
- **Pulsing "live" dot and auto-scrolling.** A pulsing dot "draws attention even when nothing changes"; marquees "make people read at the page's pace". ([Impeccable](https://www.impeccable.style/slop))
- **Unread badges and counters.** Not a visual tell, but a feel tell: the feed becomes a chore list.

### (b) What crafted rivers do
- **Separate with space and type, not boxes.** Refactoring UI: resist reaching for a border; "increase the separation" or use a background shift instead. ([Refactoring UI, Medium](https://medium.com/refactoring-ui/40c736799886))
- **Uneven rhythm.** Group tight inside an item, loose between items, looser between "moments" (like a time break). Proximity carries meaning. **[my read]** Newspapers do this: a lead story, then briefs, then a rule, never one repeated tile.
- **Mixed density by content.** Reeder's 2024 timeline mixes articles, photos, video and posts in one stream, and MacStories calls it "considered" because each type gets its own shape. ([MacStories review](https://www.macstories.net/reviews/reeder-a-new-approach-to-following-feeds/)) An image post, a text post and a link post should not wear the same box.
- **A river, not an inbox.** Current (Terry Godier, 2026) has no unread count. Items arrive, linger, and fade away. You release them, you don't clear them. ([TechCrunch](https://www.techcrunch.com/2026/02/19/current-is-a-new-rss-reader-thats-more-like-a-river-than-an-inbox/), [Godier](https://terrygodier.com/current)) Reeder also dropped read/unread and just keeps your place, with new items filling above it. ([MacStories](https://www.macstories.net/reviews/reeder-a-new-approach-to-following-feeds/))
- **Age shown by material, not numbers.** **[my read]** Following Current's idea, older items can dim, lose their glow, or "weather". That turns time into texture, which fits the shrine theme.
- **Separators as ornament, used rarely.** A small brass knot or Sheikah glyph between *groups* (e.g. "this morning" / "earlier"), not between every row. Anthropic: dividers must encode meaning. ([SKILL.md](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md))
- **One motion moment.** Anthropic prefers "a single orchestrated moment" over scattered effects. ([SKILL.md](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md)) Emil Kowalski: things seen often get little or no animation. "Think about what the user wants to achieve and how often they will see an animation." ([You don't need animations](https://emilkowal.ski/ui/you-dont-need-animations)) So: new post arriving = one soft moment. Scrolling and hovering = nearly nothing.

---

## 2. Slate (the single featured item panel)

### (a) Slop tells
- **Hero-metric template.** Big number, small label, supporting stats, gradient accent. Both Anthropic and Impeccable name it. A slate showing "▲ 12.4k upvotes" big is this.
- **Badge or eyebrow above the title.** "A small pill above the headline can look clickable." ([Impeccable](https://www.impeccable.style/slop))
- **Soft spotlight / radial halo behind the content.** "A faint glow behind a section adds emphasis without explaining what matters." ([Impeccable](https://www.impeccable.style/slop))
- **Glassmorphism.** Frosted panel, glow border, blur. ([Impeccable](https://www.impeccable.style/slop); Pixelslop #2)
- **Accenting one word** in the title in another colour or italic. ([SKILL.md](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md))
- **Everything centred.** (Pixelslop #17)

### (b) What crafted slates do
- **Fewer things, more room.** BotW's team said: "only essential UI". Show info only when needed. The title menu uses empty space instead of enlarging options. ([CEDEC 2017 BotW UI talk, translated](https://gist.github.com/idbrii/e39fe96279aa1670319bfa521d907399))
- **Simple UI, made high quality.** Nintendo's stated method: "Display simple UI and make it appear high quality." They shrank fonts and elements, then used animation only for key moments (hearts flash white on damage). (same source)
- **Warm off-white, not pure white.** BotW's text colour is "Zelda White", slightly yellow. Text is "borderless, simply colored text with zero frills". (same source)
- **The content is the hero.** Game UI Database founder Edd Coates contrasts BotW with Persona 5: BotW's transparent, minimal panels let Hyrule be the focus. ([80.lv on Game UI Database](https://80.lv/articles/game-ui-database-collecting-references-to-inspire-designers/)) For Reddit: the post's image or first lines are the hero, not the vote count.
- **Real depth, not fake glow.** Apple TV's focused items are 2 to 5 real layers (background, middle, foreground) and the text sits on the top layer. ([Apple HIG, images](https://developer.apple.com/design/human-interface-guidelines/images)) Depth comes from layers moving at different rates, not from a halo.
- **Museum-label discipline.** Beverly Serrell's exhibit-label rule: short, focused, concrete, and consistent in design. ([Serrell, Exhibit Labels, summary](https://www.librarything.it/work/210)) **[my read]** A wall label is title, one line of context, small source line, then lots of wall. That is a good slate.
- **Asymmetry with a reason.** Persona 5 uses angles and contrast so "relevant information sits on the same side", and a central line draws the eye. Its designers admit early builds were "impossible to read" and needed tuning. ([Redbrick](https://www.redbrick.me/behind-the-mask-producing-a-games-visual-identity/), [PC Gamer](https://www.pcgamer.com/games/rpg/persona-series-director-says-atluss-flashy-menus-are-actually-really-annoying-to-design)) Lesson: lean the slate off-centre on purpose, then test it can still be read at a glance.

---

## 3. Card (one item in the feed)

### (a) Slop tells
- **Icon tile above heading.** "An icon in a rounded square above a heading is a familiar AI feature-card pattern." ([Impeccable](https://www.impeccable.style/slop))
- **One radius on everything** (8 to 20px), and "extreme border-radius" that makes every card look alike. ([Impeccable](https://www.impeccable.style/slop); Pixelslop #10)
- **Same soft shadow on every box** (`rgba(0,0,0,.1)`). ([SKILL.md](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md); Pixelslop #10, S6)
- **Ghost card:** "1px hairline plus a wide soft shadow." ([Impeccable](https://www.impeccable.style/slop))
- **Side-stripe accent:** "Thick colored stripe on one side" of a rounded card. ([Impeccable](https://www.impeccable.style/slop)) Easy to fall into with a subreddit colour bar.
- **Cards inside cards.** Five layers of padding and shadow around the same content ("Cardocalypse"). ([Impeccable](https://www.impeccable.style/slop))
- **Flat type hierarchy.** Title and body look too alike. ([Impeccable](https://www.impeccable.style/slop))
- **Same hover zoom on every image.** (Pixelslop #26)
- **"→" on every link.** ([SKILL.md](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md))

### (b) What crafted cards do
- **Often there is no card.** Type hierarchy does the grouping. Bigger, brighter title; smaller, dimmer meta; space below. Refactoring UI: fewer borders, more space, background shifts. ([Refactoring UI](https://medium.com/refactoring-ui/40c736799886))
- **Radius and elevation follow importance.** Vary them by hierarchy instead of one value for all. ([Capital and Compute](https://capitalandcompute.net/blog/fix-ai-slop-design/); [SKILL.md](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md))
- **Each content type gets its own shape.** **[my read]** An image post can be the image, cut into the stone. A text post can be a quote. A link post can be a slim line. Reeder proves mixed types read fine when each is considered. ([MacStories](https://www.macstories.net/reviews/reeder-a-new-approach-to-following-feeds/))
- **One light, one bevel.** In pixel UI, raised = light top-left edges, dark bottom-right; pressed = inverted and label moved down 1px. ([Wayline game UI pixel lesson](https://www.wayline.io/learn/game-ui-art/2)) Pick one light direction and keep it for the whole scene. Avoid "pillow shading" (bright centre, dark all round, no light source). ([Slynyrd Pixelblog 6](https://www.slynyrd.com/blog/2018/6/15/pixelblog-6-light-and-shadow))
- **Shadows shift hue, not just darkness.** Otherwise colours go "washed out or muddy". (same Slynyrd / pixel sources) Slate shadow should lean blue-violet, brass shadow lean brown-red.
- **Hover is immediate and small.** Emil: high-frequency things get little or no motion, under 300ms. ([Emil Kowalski](https://emilkowal.ski/ui/you-dont-need-animations)) Rauno: feedback should respond at once, under the pointer. ([Rauno, Invisible details of interaction design](https://rauno.me/craft/interaction-design))

---

## 4. Frame (the decorative border around the widget)

### (a) Slop tells
- **Glow border as decoration.** "Blur effects, glass cards, and glow borders used as decoration." ([Impeccable](https://www.impeccable.style/slop))
- **Thick coloured border on a rounded box.** Called "the single most recognizable signature of AI-generated UI" by one designer roundup. ([search summary of Noqta / 925 Studios](https://noqta.tn/en/blog/ai-design-slop-overused-ui-patterns-fix-2026))
- **Decorative grid lines or stripes** filling empty space. ([Impeccable](https://www.impeccable.style/slop))
- **Full ornate border everywhere, at every level.** **[my read]** Stock "fantasy UI kit" frames (gold rim on every panel, every button, every bar) are the game-UI version of the identical card grid. Search results for ornate game frames are almost all stock packs ([example](https://stock.adobe.com/images/medieval-game-menu-frames-ui-elements-buttons-gold-silver-and-red-banners-with-ornate-rims-empty-royal-gui-bars-for-rpg-or-arcade-borders-web-design-interface-cartoon-2d-vector-illustration-set/501262900)). That is what "corny" looks like.
- **A frame that fights the content.** A pixel UI guide: UI must be legible before beautiful; a frame that makes contents hard to see "has failed". ([search summary, sprite-ai pixel UI](https://www.sprite-ai.art/gallery/ui))
- **Soft blurry edges on pixel art** from non-whole-number scaling. ([Wayline](https://www.wayline.io/learn/game-ui-art/2))

### (b) What crafted frames do
- **Ornament is reserved for the special thing.** Nintendo gave the Sheikah Slate an ancient feel by *adding decoration to it only*, to set it apart from the rest of the UI, which stays plain. ([CEDEC 2017](https://gist.github.com/idbrii/e39fe96279aa1670319bfa521d907399)) So: one ornate outer frame, plain inside. Never ornate inside ornate.
- **Nine-slice, with real corner art.** Corners fixed, edges tile, centre fills. The slice inset must fully hold the corner art. ([Coherent Labs 9-slice guide](https://guide.coherent-labs.com/layout-assets-and-styling/graphics--shapes/advanced-ui-shapes/); [Wayline](https://www.wayline.io/learn/game-ui-art/2)) **[my read]** Crafted frames put the weight in the corners and let the long edges go quiet or even break, so the frame reads as an object, not a stroke.
- **Material honesty.** Outer Wilds built its UI from real NASA panels and real fuel gauges: rounded corners to sit with round parts, thin lines to separate functions, white-on-black like a real visor display. They ruled out anything "futuristic-looking" and cut clutter until no element stood out too much. ([Mobius Digital: Concepting UI](https://www.mobiusdigitalgames.com/news/concepting-ui)) For us: brass should behave like brass (warm highlight, dark worn recesses), slate like stone (matte, chipped), glow like light coming *out of carved lines*, not painted on top.
- **Each frame fits its subject.** Hades: "each menu is designed to the letter to match the theme of what it's presenting", with its own animation and sound. ([WhatCulture](https://whatculture.com/gaming/hades-14-awesome-details-you-totally-missed?page=5)) Supergiant hires a dedicated UI illustrator for menu screens, not a kit. ([Supergiant job post](https://www.supergiantgames.com/blog/job-opportunity-ui-illustrator-for-hades-ii))
- **One light direction for the whole widget.** Top-left is the common choice. Highlights on the lit edges, shadow opposite. ([Slynyrd](https://www.slynyrd.com/blog/2018/6/15/pixelblog-6-light-and-shadow))
- **Whole-number pixel grid.** An 8px unit at 3x = 24px; a stray 11px piece goes soft. ([Wayline](https://www.wayline.io/learn/game-ui-art/2)) Matches the "pixel art, crisp" rule.
- **Wear and imperfection.** **[my read]** Hand-made frames are not mirror-symmetric to the pixel. One corner has a chip, the brass has a worn spot where a hand would touch, glyphs are slightly uneven. That kills "a bit perfect". BotW even captured and distorted real textures for menus (Game Over text, gear screen), rather than drawing clean vectors. ([CEDEC 2017](https://gist.github.com/idbrii/e39fe96279aa1670319bfa521d907399))
- **Text sits on the surface, not in a box on it.** BotW text is borderless with no plates behind it. ([CEDEC 2017](https://gist.github.com/idbrii/e39fe96279aa1670319bfa521d907399)) **[my read]** Where the texture is busy, darken the stone locally (a soft shadow "pool") instead of adding a panel.

### Games to study (for looking, not quoting)
Search each on the [Game UI Database](https://www.gameuidatabase.com/) (55,000+ screens, filterable by texture and pattern).
- **BotW / TotK:** plain translucent panels, decoration only on the Slate, warm white text.
- **Hades:** each screen themed to its subject; ornament heavy but one frame per screen.
- **Outer Wilds:** real-object materials; restraint by iteration.
- **Persona 5:** asymmetry with a reason; also a warning about legibility.
- **Hollow Knight:** minimal HUD at the edges so the world shows; inventory praised for proximity, colour and type. (secondary sources only)
- **Stardew / Sea of Stars / Octopath / Chained Echoes / Eastward:** pixel frames with one light source, chunky corners, quiet edges. I found no artist breakdown for these; look at the screens directly.

---

## Anti-slop checklist (grade a mockup)

Score 1 point per "yes". 13+ is good. Under 10, redo.

1. **One bold thing.** Is there exactly one place that is clearly the star, with everything else quiet?
2. **No box in a box.** Can you count at most two levels of container (outer frame, then content)? No framed card inside a framed panel.
3. **Uneven spacing on purpose.** Are gaps inside an item clearly smaller than gaps between items, and those smaller than gaps between groups?
4. **Items are not clones.** Do image, text and link posts have different shapes?
5. **Ornament is rare.** Is the decoration only on the outer frame and maybe one special element, not on every row?
6. **Glow is earned.** Does the Sheikah glow appear only on one live or selected thing? Cover the glow with your thumb: does the design still work?
7. **One light direction.** Do every bevel, highlight and shadow agree on where the light comes from?
8. **Shadows have colour.** Are shadows tinted (blue for slate, brown for brass), not grey/black at 10% everywhere?
9. **Corners carry the frame.** Are the frame's corners the detailed part, with edges quiet, and not one even stroke all round?
10. **Some wear.** Is there at least one deliberate imperfection (chip, worn brass, uneven glyph) so it is not "a bit perfect"?
11. **Type does the grouping.** Can you tell title, body and meta apart by size, weight and brightness alone, with the boxes removed?
12. **No template chrome.** No middle-dot meta strings, no ALL-CAPS eyebrows, no mono labels, no pills above titles, no "→" on links, no hero number.
13. **Warm white, not pure white.** Is the main text an off-white (BotW's "Zelda White" idea), and is it sitting on the surface with no plate behind it?
14. **Motion only where it means something.** One arrival moment for new posts; hover and scroll near-instant (under ~200ms, ease-out); no pulsing dots, no bounce, no fade-up on every row.
15. **Crisp on the grid.** Is every pixel piece at a whole-number scale, with no soft or blurry edges?

---

## Sources

Discourse on AI slop
- Anthropic frontend-design skill: https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md
- Impeccable, the visible tells of AI design: https://www.impeccable.style/slop
- Impeccable anti-patterns docs: https://www.mintlify.com/pbakaus/impeccable/concepts/anti-patterns
- Pixelslop pattern catalog: https://cdn.jsdelivr.net/npm/pixelslop@0.3.8/dist/skill/resources/ai-slop-patterns.md
- Why your AI keeps building the same purple gradient website: https://prg.sh/ramblings/Why-Your-AI-Keeps-Building-the-Same-Purple-Gradient-Website
- Noqta, AI design slop: https://noqta.tn/en/blog/ai-design-slop-overused-ui-patterns-fix-2026
- 925 Studios, AI slop tells: https://www.925studios.co/blog/ai-slop-design-tells
- Capital and Compute, fix AI slop tell by tell: https://capitalandcompute.net/blog/fix-ai-slop-design/
- Refactoring UI, practical tips: https://medium.com/refactoring-ui/40c736799886

Motion and feel
- Emil Kowalski, You don't need animations: https://emilkowal.ski/ui/you-dont-need-animations
- Rauno Freiberg, Invisible details of interaction design: https://rauno.me/craft/interaction-design

Game UI
- BotW CEDEC 2017 UI talk (fan translation): https://gist.github.com/idbrii/e39fe96279aa1670319bfa521d907399
- Game UI Database: https://www.gameuidatabase.com/ and 80.lv on it: https://80.lv/articles/game-ui-database-collecting-references-to-inspire-designers/
- Mobius Digital, Concepting UI (Outer Wilds): https://www.mobiusdigitalgames.com/news/concepting-ui
- Persona menus: https://www.redbrick.me/behind-the-mask-producing-a-games-visual-identity/ and https://www.pcgamer.com/games/rpg/persona-series-director-says-atluss-flashy-menus-are-actually-really-annoying-to-design
- Hades menus: https://whatculture.com/gaming/hades-14-awesome-details-you-totally-missed?page=5 and https://www.supergiantgames.com/blog/job-opportunity-ui-illustrator-for-hades-ii
- 9-slice: https://guide.coherent-labs.com/layout-assets-and-styling/graphics--shapes/advanced-ui-shapes/
- Pixel UI bevels and grid: https://www.wayline.io/learn/game-ui-art/2
- Light and shadow in pixel art: https://www.slynyrd.com/blog/2018/6/15/pixelblog-6-light-and-shadow

Lists and featured panels
- Current reader (river, no unread count): https://www.techcrunch.com/2026/02/19/current-is-a-new-rss-reader-thats-more-like-a-river-than-an-inbox/ and https://terrygodier.com/current
- Reeder timeline review: https://www.macstories.net/reviews/reeder-a-new-approach-to-following-feeds/
- Apple HIG, layered images: https://developer.apple.com/design/human-interface-guidelines/images
- Serrell, Exhibit Labels: https://www.librarything.it/work/210

Gaps: I found no first-hand artist breakdowns for Sea of Stars, Hollow Knight, Dredge, Stardew, Octopath or Chained Echoes frames, and no Jen Zee interview on Hades UI specifically. Notes on those games are my reading of the screens and are marked.
