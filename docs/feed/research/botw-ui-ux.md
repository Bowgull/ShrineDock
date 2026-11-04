# BotW / TotK UI and UX, applied to the ShrineDock Reddit feed

Researched 2026-11-04. Goal: take how Breath of the Wild's menus *behave*, not just how they look, and map it onto an always-on desktop Reddit widget (dark slate, Sheikah-style blue glow, brass pixel frame, fireflies). No Nintendo assets, no Sheikah alphabet, no Hylia fonts. We invent our own glyphs.

Confidence key: **[src]** = backed by a linked source. **[play]** = how the game behaves when played, from general knowledge; wikis (zeldawiki, fandom) blocked fetches today, so check these in Cemu if a detail matters.

---

## Part 1. How BotW's UI actually works

### The one rule: "only essential UI"
- Nintendo's CEDEC 2017 UI talk gave the whole concept as "only essential UI." Information shows only when needed. Info is merged so the eye has fewer places to look. [src: Famicast CEDEC part 2, Matt Walker translation gist]
- Empty space is used on purpose. The title menu leaves room instead of making buttons bigger. [src: Famicast]
- UI and fonts were made *smaller*, then given animation so key things still get noticed. Example: hearts flash white when you take damage. Their method: "display simple UI and make it appear high quality." [src: Famicast]
- Text is borderless, plain-coloured, undecorated. [src: Famicast]
- Pro HUD mode strips the minimap, temperature and noise gauges, leaving only hearts. It was added because the overseas branches asked for even less. [src: Famicast; Zelda Universe]
- Nintendo's later recruitment page (Daigo Shimizu): BotW = hide UI when not needed, cut decoration, clarity first. TotK = same clean base, but more emotional warmth. "Spark curiosity, don't force it." [src: Nintenderos, June 2025]

### Colour is reserved for meaning
- One unifying base colour: "Zelda White", a white with a slight yellow tint. Also used on the box and logo. [src: Famicast]
- Everything else is quiet: see-through dark panels, landscape shows through. [src: 80.lv interview with Game UI Database founder]
- Colour carries state, not decoration. On the map, towers and shrines glow **orange** when found but not done, then turn **blue** when completed. A **red X** marks where you died. [src: Stamen map review]
- Pins each get their own colour so you can tell them apart at a glance. [src: Stamen; Zelda Wiki pin page via search]
- Selection uses a warm gold glow; Sheikah tech uses cyan blue. Fan kits measure these as roughly `#3CD3FC` blue and a soft gold inner glow. [src: chaos-xxl DESIGN_PROMPT.md, measured from a CC BY Figma kit]
- Dialogue colours key words (names, places) so you can skim. [play]

### The Sheikah Slate as the frame
- The Slate is diegetic: the menus are "on the tablet Link carries". It was designed with the artists and given extra decoration to feel ancient. [src: Famicast]
- It began as a nod to the Wii U GamePad; later the team said it looks like a Switch in handheld. [src: Zelda Dungeon]
- Second-screen features were cut because they pulled eyes off the game. [src: Zelda Dungeon / Wired]

### Pages and tabs
- Pause menu pages: Adventure Log, Inventory, System. The Map is its own page on its own button. [src: Zelda Wiki via search]
- One pair of shoulder buttons steps between pages; another pair steps between inventory categories. The page slides sideways; you always know where you are because the neighbour page titles peek at the edges. [play]
- Album and Hyrule Compendium live under the camera rune, not as top-level tabs. [play]
- The inventory reopens exactly where you left it. [src: Game Developer UI/UX analysis]

### Adventure Log
- Left: a list grouped as Main Quests (first tab, marked with a wing crest), Side Quests, Shrine Quests. Memories sit in their own section. [src: Zelda Wiki/Zelda Dungeon via search]
- Right: detail for the highlighted quest. The text grows as the quest moves on; old steps stay as a history. [play]
- You can mark one quest on the map. Only one is "active" at a time, on purpose, to stop overwhelm. [src: Game Developer]
- Done quests get a "Completed" stamp and drop to the bottom. [play]
- TotK added a small map snippet, the quest giver's name and key facts to each entry. [src: UX Collective via search; Nintendo Life "26 tweaks"]
- Starting or finishing a quest shows a big centred title banner with a short jingle, then it's gone. [play]

### Hyrule Compendium
- A numbered catalogue (creatures, monsters, materials, equipment, treasure). Numbers are fixed, so gaps show what you're missing. [play]
- Grid of photo tiles. An entry has no picture until *you* photograph the thing. So the catalogue is literally made of your own discoveries. [src: Zelda Dungeon / Zelda Wiki via search]
- Detail page: your photo, number, name, a short description, and what it drops. [play]
- From an entry you can set it as the sensor target. [src: Zelda Dungeon Sensor+; Sportskeeda]
- Critique: photo storage is capped, and clearing it is fiddly (select all, then deselect what to keep). [src: Game Developer]

### Sheikah Sensor (the ping)
- Basic sensor: a small HUD gauge that bleeps when a shrine is near. [src: Zelda Dungeon]
- Sensor+: pick one Compendium entry; the Slate bleeps faster as you get closer. Only one target at a time. [src: Zelda Dungeon; Sportskeeda]
- It reacts to the way you face, so you "sweep" to find direction. [play]
- TotK: a small radar icon beside the minimap does the same job. [src: Sportskeeda]

### Inventory
- Category tabs across the top (weapons, bows, shields, armour, materials, food, key items). Grid of tiles under them. [play]
- A glowing focus box sits on one tile. Name, short description and stats show in a fixed panel, so the grid itself stays clean. [play]
- Newly gained items carry a small "new" sparkle until the cursor passes over them. Tabs with new items carry it too. [play]
- One button cycles sort order. TotK added "most used". [src: Nintendo Life "26 tweaks"; GamePro]
- Moving past the edge of a page carries you to the next category, with a sideways slide. [play; also the edge-wrap in flagrede's recreation]
- Critique: lots of nesting and repeated menu trips (changing clothes 3–5 times in 5 minutes). [src: Game Developer; Zelda Dungeon]

### Map
- 120 tiles that stream in, four fixed zoom steps. The zoom step is softened by a quick blur. [src: Famicast; Stamen]
- Zoomed out, only big region names. Small places appear as you zoom in. [src: Stamen]
- Unrevealed land is a dark blue tech grid, not fog. Climbing a tower reveals terrain, but not what's in it. [src: Stamen; Game Developer]
- Pins: a few, each a different colour, dock to the minimap, vanish when you arrive. Stamps: up to 100, unlabelled, you decide what they mean. A pin can turn into a stamp. [src: Stamen]
- TotK: three layers (sky, surface, depths). Pins on other layers draw differently so you know they're "not here". [src: Zelda Wiki pin page via search; Stamen TotK]

### Loading screens
- BotW used loading time for useful tips. TotK showed your destination on the map "like previewing a trip", turning waiting into anticipation. [src: Nintenderos / Shimizu]

### Text box and dialogue
- Rounded dark box low on screen, speaker name above, text writes on, a small blinking arrow waits for you. Choices appear as a short list beside it. [play; fan kits copy this capsule shape]

### Item-get and notices
- First time you get something: a centred card with its name and description, and a jingle. After that: only a small toast with icon and name at the side. [play]
- Spirit orbs and shrine endings are staged reward moments, which one analyst liked but found heavy after the 50th time (15 s in, 30 s out). [src: Game Developer]
- Lesson: big moment once, small moment every time after.

### Sound
- Over 10,000 sounds were recorded. [src: Zelda Universe, Hikino dev blog]
- The music is sparse piano; silence is part of the design. Environment sounds even duck during fights through a priority system. [src: CEDEC sound talk summary in the gist; Zelda Dungeon video essay]
- Menu sounds are soft wooden/glassy ticks for cursor moves and a brighter chime for confirm; page turns have a whoosh. [play]

---

## Part 2. What GitHub recreations get right and wrong

| Repo | Stars | What it is | Right | Wrong / watch out |
|---|---|---|---|---|
| [chaos-xxl/zelda-hyrule-ui](https://github.com/chaos-xxl/zelda-hyrule-ui) | ~350 | 83 React components from Hunter Paramore's CC BY Figma kit | Measured tokens: double border (dark outer + 1px inner line inset 3px), angular 4px corners, gold glow for selected, blue for tech. Good reference for *values*. | Uses Hylia Serif and Sheikah eye/Triforce marks (Nintendo IP, they admit it). Always-italic Roboto. It's a *component library*, so it copies the look but not the behaviour: no focus memory, no "new" marks, no first-time vs repeat. |
| [flagrede/zelda-botw-starter](https://github.com/flagrede/zelda-botw-starter) + [dev.to series](https://dev.to/flagrede/how-to-replicate-the-zelda-botw-interface-with-react-tailwind-and-framer-motion-part-2-3nd4) | ~90 | Inventory in React + Tailwind + Framer Motion | Arrow keys with edge-wrap into the next page. Slide direction kept in state so pages slide the right way. Typewriter description. Warns the typewriter hook re-renders a lot, keep it low in the tree. | Ships Nintendo's item PNGs. Mouse/keys only, no sound. |
| [alfredbaudisch/GodotDynamicInventorySystem](https://github.com/alfredbaudisch/GodotDynamicInventorySystem) | ~670 | Godot 4 inventory | Paged, animated grids per category; real UI sounds (click, lower-pitch click, page slide); blur shader behind the menu. Closest to the *feel*. | Roboto everywhere; it's a game engine, not a web widget. |
| [masbagal/react-sheikah-ui](https://github.com/masbagal/react-sheikah-ui) | ~50 | Small React kit (button, card, dialogue card, inputs) | Simple dialogue card. | Look only. |
| [geddski/zelda-inventory](https://github.com/geddski/zelda-inventory) | ~25 | Course repo, web inventory | Ships a reference video to match motion against. | Little code in the repo. |
| [sambrannon/botw-css](https://github.com/sambrannon/botw-css) | 5 | Hearts in pure CSS | – | Tiny; uses Hylia font. |
| [JonaRu32/sheikah-slate-os](https://github.com/JonaRu32/sheikah-slate-os) | 2 | Hyprland desktop theme | Same idea as ShrineDock (desktop theme). | Uses rune glyphs. |

**Overall:** every recreation copies the *skin* (glow, borders, font) and skips the *behaviour* (memory, reserved colour, first-time vs repeat, one active target, sound). That's the gap ShrineDock can own. Copying the fan kits' glyphs or fonts would break the "invent our own" rule.

---

## Part 3. 14 behaviours mapped onto the Reddit widget

Fit key: **Glance** = fits the always-on widget. **Open** = only when he opens/expands it. **Too much** = skip or keep rare.

1. **Only essential UI (Pro HUD).** Idle widget shows only a post title, sub and age; score/comments appear on hover. Offer a "Pro" idle mode: just a firefly count of unread. **Glance.**

2. **Colour reserved for meaning.** Base text is one warm off-white. Blue glow = new/unread. Gold = focused or saved. Orange = "seen, not opened" (like a found-but-unfinished shrine), turns calm blue-grey once read. Red only for errors. No decorative colour. **Glance.** This is the most important one.

3. **"New" sparkle until passed over.** Each new post gets a small invented glyph that fades once the cursor or keyboard focus crosses it (not on click). The sub's tab carries a sparkle while any post inside is new. **Glance.**

4. **Compendium entry = a post.** Each post card is a numbered tile (number within today's run, or within the sub). Thumbnail = the "photo". Detail = photo, title, sub, short text. Gaps in numbering make "what I skipped" visible. **Open.**

5. **Adventure Log split for an open post.** Expanded view: list on the left grouped like Main/Side (Pinned picks / Subscribed subs / Discovery), detail on the right. The detail keeps a history line ("12 new comments since you looked"). **Open.**

6. **One active quest.** Only one post or thread can be "tracked" at a time. A tracked thread shows a tiny marker at the widget's edge with its new-reply count. Prevents overwhelm. **Glance.**

7. **Sensor ping for discovery.** He picks one topic (like setting a sensor target). When a matching post appears, a soft single ping and a slow glow pulse on the frame edge, stronger for a closer match. One target only. Never repeated chimes. **Glance**, but keep the sound off by default during games (see his no-tests-while-playing and quiet rules).

8. **Item-get: big once, small after.** First time he saves a post from a new sub: a short centred card with a soft chime ("Added to your shelf: r/xyz"). Every later save: a tiny side toast. Never a full card twice for the same thing. **Glance** (the toast); the card is **Open**-only moment, rare.

9. **Quest-start / complete banner.** Use sparingly: e.g. "Caught up" banner when he reaches the last unread post, shown once, fades in 1.5 s. **Glance**, rare. Daily or more = **Too much**.

10. **Reopen where you left it.** Expanded view remembers sub, scroll position and focused post. Click out closes smoothly (his standing rule) and reopening lands on the same post. **Open.**

11. **Pages slide, edges wrap.** Sub tabs as pages. Arrow keys move focus; moving past the last post slides to the next sub, in the right direction. Neighbour sub names peek at the edges. **Open.**

12. **Map: zoom shows detail.** Collapsed = titles only. Medium = titles + thumbnails. Expanded = full cards with text. Fixed steps, not free resize, with a quick blur between steps. **Glance → Open.**

13. **Stamps = his own marks.** Let him drop a few unlabelled invented glyphs on posts ("later", "show Aang", "funny"); he decides what they mean. Pins (limited, coloured) = posts he wants reminded about; they vanish once opened. **Open.**

14. **Loading screen as anticipation.** While a refresh loads, show one line from a saved post or a tiny preview of the sub it's fetching, instead of a spinner. **Glance.**

15. **Sound: soft ticks, rare chimes, mostly silence.** Cursor move = quiet tick, open = soft whoosh, save = brighter chime. Idle widget = silent. Mute while Cemu/WoW run. **Open** for ticks; **Glance** only for the single sensor ping.

### Too much for an always-on widget
- Typewriter text in the idle view (distracting, and the fan repos note it re-renders a lot).
- Big staged reward scenes more than once (the shrine-exit lesson: 30 s gets old fast).
- Full Compendium grid of all posts always visible.
- Repeating pings or a sensor that bleeps faster and faster on the desktop.
- Dialogue-box speaker framing for every post.

---

## Sources
- [Famicast: CEDEC 2017 part 2, Immersive and impressionable UI](https://www.thefamicast.com/2017/12/part-2-immersive-impressionable-ui.html)
- [Matt Walker CEDEC 2017 translation, image gist](https://gist.github.com/idbrii/e39fe96279aa1670319bfa521d907399)
- [Famicast: CEDEC talks translated, index](https://www.thefamicast.com/2017/12/cedec-talks-translated-making-of-breath.html)
- [Nintenderos: Nintendo explains BotW/TotK UI keys (Daigo Shimizu), 2025](https://www.nintenderos.com/nintendo-switch-2/zelda-breath-of-the-wild-tears-of-the-kingdom-nintendo-revela-las-claves-de-su-interfaz-de-usuario-mapa-cocina-y-mucho-mas/)
- [Game Developer: A UI/UX analysis of Zelda BotW](https://www.gamedeveloper.com/art/a-ui-ux-analysis-of-zelda-breath-of-the-wilds)
- [Stamen: Cartographers play video games, BotW map review](https://stamen.com/cartographers-play-video-games-a-review-of-the-map-in-the-legend-of-zelda-breath-of-the-wild/)
- [UX Collective: TotK, how Nintendo improved and ignored UI issues](https://uxdesign.cc/tears-of-the-kingdom-how-nintendo-improved-and-ignored-ui-issues-843f094b14b2) (blocked today; quoted via search)
- [Nintendo Life: 26 tweaks that make TotK better than BotW](https://www.nintendolife.com/features/26-tweaks-that-make-zelda-tears-of-the-kingdom-better-than-botw)
- [Zelda Universe: Pro HUD](https://zeldauniverse.net/2017/02/08/breath-of-the-wild-will-offer-a-trimmed-down-pro-hud-option/)
- [Zelda Universe: 10,000+ sound effects](https://zeldauniverse.net/2017/06/09/breath-of-the-wild-dev-blog-discusses-the-games-sound-effects-over-10000-recorded/)
- [Zelda Dungeon: Slate and the Wii U GamePad](https://www.zeldadungeon.net/?p=17779)
- [Zelda Dungeon: GamePad features rejected](https://www.zeldadungeon.net/gamepad-features-for-breath-of-the-wild-rejected-due-to-disrupting-gameplay/)
- [Zelda Dungeon: BotW invisible sound design video essay](https://www.zeldadungeon.net/explore-breath-of-the-wilds-invisible-sound-design-in-this-video-essay/)
- [Zelda Dungeon wiki: Sheikah Sensor](https://www.zeldadungeon.net/wiki/Sensor_%2B)
- [Sportskeeda: Sensor+ in TotK](https://sportskeeda.com/esports/how-use-sensor-function-hyrule-compendium-the-legend-zelda-tears-kingdom)
- [80.lv: Game UI Database founder interview](https://80.lv/articles/game-ui-database-collecting-references-to-inspire-designers/)
- [Zelda Wiki: Pin](https://zeldawiki.wiki/wiki/Pin), [Adventure Log](https://zeldawiki.wiki/wiki/Adventure_Log) (blocked to fetch; via search snippets)
- GitHub repos linked in Part 2.
