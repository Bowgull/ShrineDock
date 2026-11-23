# shrinedock ⛩️

the dock, top strip, clock & widgets of my custom windows desktop. it replaces the windows taskbar with something that looks like it came out of an old shrine: bronze rails, dark stone, a little cyan glow.

🚧 still building. rough edges are mine 🙂

**what it does**
- a dock on every screen. click an app and it opens on that screen, not wherever windows feels like putting it
- its own start menu. the windows key opens it on whichever screen my mouse is on
- a top strip with the clock, volume and any app that's trying to get my attention
- a sound mixer with a slider for every app playing sound
- soft cyan borders on every window so the whole desktop matches
- remembers where i left my widgets, even when my second screen drops off and comes back
- leaves full-screen games alone. nothing pops up over zelda
- hides the windows taskbar the second it tries to sneak back

**the wallpaper**
the desktop sits on a live wallpaper, the shrine garden, running in lively wallpaper. on startup shrinedock waits until every screen has its garden before the dock & clock fade in, so the whole desktop shows up at once instead of in pieces.

**what i did**
designed every piece of it. drew it all as mockups first, set the rules (soft corners, no boxes inside boxes, the bronze rail cut from my own art and never redrawn), and tested each part on the real pc before moving on. the full rulebook is in [docs/DESIGN-LANGUAGE.md](docs/DESIGN-LANGUAGE.md). ai tools wrote the code, i said no a lot 🙂

**built with** c#, .net 10, wpf (no xaml), windows shell hooks. runs next to lively wallpaper & rainmeter.

building it yourself: [docs/BUILDING.md](docs/BUILDING.md)
