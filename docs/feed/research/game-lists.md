# How great games show lists of text (and never look like web cards)

For: the Shrine Reddit widget. Two modes: the drifting **river** and the idle **slate**.
Date: 2026-11-04. Principles only. Copy no Nintendo or other studio art, glyphs, fonts or sounds.

How sure we are: the developer quotes and the Disco Elysium, Pentiment and Tunic notes are sourced (links below). The descriptions of how each screen looks and moves come from memory of playing and watching the games. Treat them as good, not frame-exact. gameuidatabase.com blocked our fetch (403), so check screenshots there by hand before building.

---

## The one big idea

Web cards are **boxes in a grid**. Each box has its own border, shadow and round corners, and they all look the same.
Game lists are **one surface with marks on it**. The panel is the object: a slate, a book, a board. Entries are lines of ink or light *on* it. Rules, flourishes, a cursor and the selected entry do the separating. Boxes don't.

If every post has its own rounded rectangle, it's a web list. If the posts are writing on one stone, it's a game.

---

## The examples

### Disco Elysium: the dialogue column (the river to study)
- **Made of:** one tall column at the right edge of the screen. Each line is a speaker name in capitals, a dash, then the text. Skill voices are tinted by their family. There are no boxes. The column sits on a dark, slightly painted ground.
- **Focus:** the reply options sit at the bottom, numbered. The hovered one brightens. Older lines above stay readable but are no longer the point.
- **Arrival:** new text is added at the bottom and the whole column moves up. Reviewers describe a quick swipe in with a typewriter-like whirr.
- **Why it isn't cards:** it's a single ribbon of writing. Rhythm comes from the speaker names, not from borders.
- **Design intent:** Kurvitz says they wanted the text engine "as addictive and as snappy as Twitter". He put it bottom right because that's where you look on a computer, and he argues a column reads best. Players skim for the gist, like tweets.
- Sources: [GamerMatters](https://gamermatters.com/disco-elysiums-text-box-design-is-inspired-by-how-we-use-computers-and-twitter/), [80.lv](https://80.lv/articles/disco-elysium-working-on-ui-design), [Kotaku](https://kotaku.com/disco-elysiums-dialogue-system-is-as-addictive-as-any-s-1841046831), [RPG Codex on the GameSpot interview](https://rpgcodex.net/article.php?id=11309)

### Death Stranding 1 and 2: a social feed that lives in the world
- **Made of:** other players' signs and buildings stand in your world as holograms. Likes don't arrive as a list. They arrive as small counters with a name that rise in a corner of the HUD and fade. Mail from NPCs is a plain list of sender and subject with an unread mark. The menus are cold and minimal. The selected button trails a long line, the strand, and that motif repeats across the game.
- **Focus:** the strand line reaches out from whatever is selected. The selection is *connected*, not boxed.
- **Arrival:** likes trickle in while you play, rise, add up and fade. It's ambient, not a notification wall. In DS2, liking an NPC's post starts a short window where more presses send more likes, and the NPC usually likes you back.
- **Design intent:** Kojima calls likes unconditional love, not currency. There is no dislike.
- **Lesson:** a social feed feels like part of the world when posts are *things that appear in the place* and reactions are *small lights that rise and fade*. It also shows a risk: post-launch they had to make the menu text bigger because players couldn't read it.
- Sources: [Game Informer](https://gameinformer.com/2019/09/16/likes-in-death-stranding-are-unconditional-love-not-currency), [GameSpot social guide](https://gamespot.com/gallery/death-stranding-multiplayer-guide-explaining-how-t/2900-3159/), [GamesRadar DS2 likes](https://www.gamesradar.com/games/action/death-stranding-2-likes), [Fonts In Use](https://fontsinuse.com/uses/67648/death-stranding-video-game), [Automaton: bigger text patch](https://automaton-media.com/articles/newsjp/20191128-107562/)

### Breath of the Wild / Tears of the Kingdom: Adventure Log, Compendium, Memories
- **Made of:** a dark, see-through slate panel with thin lines. The Adventure Log is a list of names on the left and the full text on the right. Main quests carry a small crest, and the Memories section has its own symbol. Compendium pages are mostly one big picture with a short paragraph under it, and the picture is a photo *you* took in the world.
- **Focus:** the selected row lights up with a cool glow and bracket marks. Everything else stays quiet.
- **Arrival:** a short banner when you get a new quest or entry. In the log, finished quests are dimmed and tagged, not removed.
- **Why it isn't cards:** it's one device, the slate. The rows are text on glass, and the glow does the selecting.
- Sources: [Zelda Wiki: Adventure Log](https://zeldawiki.wiki/wiki/Adventure_Log), [Zelda Dungeon on the log's voice](https://www.zeldadungeon.net/daily-debate-should-the-adventure-log-in-breath-of-the-wild-have-been-localized-from-links-perspective/) (the Japanese version reads as Link's diary; English reads like a guide)

### Outer Wilds: the Ship Log
- **Made of:** Map mode shows the places you found with entries under them. Rumor mode is a web of picture tiles joined by arrows. A tile that still holds a secret wears a small marker.
- **Arrival:** when you sit at the ship's computer, new findings are *played back* to you one line at a time with a terminal type-on. It's a ritual, not a badge.
- **Why it isn't cards:** the links between entries carry the meaning, and the log is the ship's own computer.
- Sources: [New Horizons ship-log guide](https://nh.outerwildsmods.com/guides/ship-log), [SFRA Review](https://sfrareview.org/2020/12/13/50-4-32/), [GDC: curiosity-driven design](https://www.gdconf.com/news/attend-gdc-and-learn-how-outer-wilds-nailed-curiosity-driven-game-design)

### Hades: the Codex and the Fated List
- **Made of:** a list of names in tabs on the left. On the right is a page with a painted portrait and lore text, set in a dark frame with gold metal trim. Entries *grow*: new paragraphs unlock as you get to know a character.
- **Arrival:** a small HUD note says the Codex has something new, and the new entry is marked in the list.
- **Lesson:** an entry that fills in over time feels written, not served.
- Source: [Nat Rowley on Hades' responsive underworld](https://medium.com/@Nat.Rowley/how-hades-creates-a-responsive-underworld-915715a7c2a)

### Hollow Knight: Hunter's Journal
- **Made of:** a list of creature names on the left. On the right is one large drawing on black, a decorated title, a thin filigree rule, then a short note in the Hunter's own voice.
- **Focus:** a cursor ornament sits beside the selected name, and the big drawing swaps in.
- **Arrival:** a "journal updated" moment after a kill. Unfinished entries show what's left to do.
- **Lesson:** one strong picture plus one thin ornament line beats any border.
- Sources: [Hollow Knight Wiki](https://hollowknight.wiki/w/Hunter's_Journal_(Hollow_Knight)), [Fextralife](https://hollowknight.wiki.fextralife.com/Hunter's+Journal)

### Pentiment: text that is being written
- **Made of:** speech written onto the screen stroke by stroke in historical scripts. The script shows who is speaking: peasant hand, scribe hand, printed type. It can change when a character reveals more learning. There are ink blots, and mistakes get corrected. Lettermatic drew over 2,700 glyphs by hand.
- **Lesson:** *how text arrives* is the strongest sign of hand-made. Sawyer says no feature took as much time as the text rendering.
- Sources: [PC Gamer](https://pcgamer.com/how-pentiments-hand-crafted-fonts-give-pen-and-ink-a-voice), [Lettermatic](https://lettermatic.com/custom/pentiment), [Language at Play](https://languageatplay.de/?p=3624)

### Tunic: the manual pages
- **Made of:** pages in the style of an old printed manual, with wear, folds and hand notes. You find them out of order, and the gaps show.
- **Lesson:** a printed, worn surface with *missing* parts feels found, not generated.
- Sources: [Game Developer](https://www.gamedeveloper.com/business/how-tunic-weaves-wondrous-unknowable-worlds-inspired-by-inscrutable-nes-manuals), [80.lv](https://80.lv/articles/tunic-s-developer-on-creating-the-in-game-manual-full-of-mysteries)

### Return of the Obra Dinn: the book
- **Made of:** a 1-bit ink logbook with pages, sketches and blanks you fill in. Correct fates lock in three at a time, with a musical sting.
- **Lesson:** confirming things in batches makes a moment, and a book you turn feels physical.
- Sources: [Wikipedia](https://en.wikipedia.org/wiki/Return_of_the_Obra_Dinn), [Game Developer](https://gamedeveloper.com/design/for-lucas-pope-i-return-of-the-obra-dinn-i-was-a-bunch-of-appealing-design-problems)

### Heaven's Vault
- **Made of:** inscriptions are rows of glyphs. You place guessed words under them, and later finds confirm or correct the guesses.
- **Lesson:** text can sit in a "half-known" state and settle later, like a sub name shown first and the title resolving after.
- Source: [Game Developer](https://gamedeveloper.com/design/how-inkle-developed-its-own-ancient-language-for-i-heaven-s-vault-i-)

### Citizen Sleeper
- **Made of:** one narrow column of text on dark, monospaced and calm, with circular segment clocks as the only ornament. Made by one designer who also did the UI.
- **Lesson:** a column plus a single kind of mark (the clock) is enough.
- Source: [Game Developer: Road to IGF](https://www.gamedeveloper.com/business/how-citizen-sleeper-was-inspired-by-tabletop-rpgs-and-gig-work)

### Stardew Valley: bulletin board and mail
- A corkboard outside the shop with pinned notes, and a daily Help Wanted note. The mailbox shows a sign when post is waiting. Letters open on paper with a short unfold.
- **Lesson:** the board is a *place*, posts are *pinned*, and new mail is shown by a small object changing, not a badge with a number.

### Animal Crossing: New Horizons
- Each morning one announcement is presented by a character, one item at a time. The NookPhone is the in-world device.
- **Lesson:** this is the idle slate in a nutshell: one item, given its moment, then the next.

### Persona 5 and Final Fantasy IX / X
- P5: list rows are torn, tilted shapes, and the highlight is a cut-out that shakes. Motion never stops, and nothing sits on a grid.
- FFIX: framed windows with a pointing-hand cursor that bobs beside the selected line. FFX: windows with gradient fills and bordered edges.
- **Lesson:** a *physical cursor object* (hand, mark, gem) beside the line can replace the hover box.

### Majora's Mask: Bombers' Notebook
- A grid of faces against a three-day timeline, with stamps when you finish an event.
- **Lesson:** stamps and seals mark state ("read", "voted") better than coloured pills do.

### Dredge, Spiritfarer, Inscryption (short)
- Dredge: ink drawings on paper pages for each fish, with odd variants beside the normal one.
- Spiritfarer: soft hand-drawn letters and keepsakes.
- Inscryption: the rulebook is a book on the table that you flip.
- Shared lesson: a known object (page, letter, book) holds the text.

### MMO chat and the WoW quest log
- WoW chat lines stay for a set time, then fade. Hovering brings them back. These are real API settings: `SetTimeVisible` and `SetFadeDuration` ([Warcraft Wiki](https://warcraft.wiki.gg/wiki/API_ScrollingMessageFrame_SetTimeVisible)). The classic quest log is a parchment list with headers you can fold, and level tags beside each quest.
- **Lesson:** lines that fade when nobody's looking are a calm way to show a flow.

### Cyberpunk 2077 / Deus Ex
- News comes through in-world screens, radio and newspapers or e-books you pick up. It's a ticker on a TV in the scene, not an overlay.
- **Lesson:** a ticker feels in-world when it's clearly coming out of *a device in the scene*.

---

## 12 patterns to borrow

Each one says what to do in (a) the river and (b) the idle slate, and which game it comes from.

1. **One surface, no per-post boxes.** *(BotW slate, Disco Elysium, Citizen Sleeper)*
   a) Posts are lines on one stone strip. They're split by a thin glowing rule or a small mark, never a rounded card each.
   b) The slate *is* the stone. The post is carved or lit text on it, with no inner card.

2. **The name leads the line.** *(Disco Elysium's speaker names)*
   a) Each post starts with the sub name in small caps, tinted by a family colour (gaming, news, hobby), then a dash, then the title.
   b) The sub name is engraved small above the big title, like a speaker tag.

3. **A cursor object, not a hover box.** *(FFIX hand, Hollow Knight ornament, DS strand)*
   a) On hover, a small brass mark slides to the left of the post and the post rises. The brass frame closes in from that mark.
   b) The brass mark rests at the slate's edge and points at what's live (title, or "open").

4. **Selection reaches out with a line.** *(Death Stranding strand)*
   a) The hovered post trails a faint Sheikah-blue line back to the river's edge, so it reads as still tied to the stream.
   b) When crossfading, a thin line draws from the old post to the new one before the swap.

5. **How text arrives is the hand-made part.** *(Pentiment, Outer Wilds playback)*
   a) New posts enter at the river's source with the title drawing in left to right, a fast glow-trace. Not a fade-in of a finished block.
   b) The next post's title traces in after the crossfade, at about reading speed, then settles.

6. **One big picture, one rule, short text.** *(Hollow Knight journal, BotW Compendium)*
   a) Photo posts show a small vignetted thumbnail that sits *in* the stone, set in, not floating.
   b) Idle slate layout: big photo, one filigree rule (our own drawing), title, sub and age. Nothing else.

7. **Ambient reactions rise and fade.** *(Death Stranding likes)*
   a) Vote changes show as tiny numbers rising off a post like fireflies, then fading.
   b) On the slate, the vote count rolls up once when the post arrives, then stays still.

8. **Old lines fade when nobody looks; hover brings them back.** *(WoW chat, Disco's older lines)*
   a) Posts dim as they drift toward the river's end. Hovering the river lifts them all back to full.
   b) The slate dims slowly over its hold time. A mouse move brings it back to full.

9. **Entries grow; they aren't served whole.** *(Hades Codex, Heaven's Vault)*
   a) A post can arrive as just sub and title. Score and comment count settle a beat later, quietly.
   b) Clicked-open view: body first, then comment 1 traces in, then comment 2, like Codex paragraphs unlocking.

10. **State as stamps and seals.** *(Bombers' Notebook, Obra Dinn)*
    a) Read posts get a small pressed seal and drop slightly in brightness. No coloured pills.
    b) A post you opened on the slate gets a seal in the corner as it leaves.

11. **The panel is a known object in the room.** *(Stardew board, Inscryption book, Deus Ex screens)*
    a) The river has a source and a mouth: it comes out of a carved spout or crack and sinks into the frame at the far end. It's never a scroll area cut off by a hard edge.
    b) The slate is a tablet resting in a brass bracket. The crossfade looks like the stone *re-lighting*, not a slide changing.

12. **One item, its moment, then the next.** *(Animal Crossing announcements, Outer Wilds ship log, Obra Dinn sting)*
    a) Every N posts, one standout (top post of the hour) slows down briefly as it passes the middle of the river.
    b) A fixed rhythm: light up, title traces, hold, dim, a short pause on empty stone with fireflies, next post. A soft chime only on the first post after a long idle, never on every one.

---

## Sound (optional, keep it quiet)
- Disco uses a quick whirr when text arrives. Outer Wilds uses terminal ticks on playback. Obra Dinn uses a sting on confirmation.
- For us: a near-silent stone tick when a post opens in place, and nothing for the river's drift. Sound only marks your own actions or rare moments, never the stream itself.

## Traps these games warn about
- Small text on a dark slate: Death Stranding had to patch in bigger text. Keep titles at least the size of normal reading text.
- Too much motion: Persona 5 gets away with it because the whole game moves. On a desktop, keep motion slow and let idle mean calm.
