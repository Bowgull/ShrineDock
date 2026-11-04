# ShrineDock

The Shrine desktop's dock, top strip, clock and widgets. It's a WPF app (.NET 10, C# only, no XAML) and runs on Joshua's Shadow PC (Windows).

This repo is **private**. It stays private: it holds his own art and paths.

## Working from the Mac

The Mac can write and **compile** ShrineDock, but it can't **run** it. Use the Shadow only for the steps marked *Shadow* below.

```bash
# once: install the .NET 10 SDK (https://dotnet.microsoft.com/download)
git clone https://github.com/Bowgull/ShrineDock.git
cd ShrineDock
dotnet build        # EnableWindowsTargeting in the .csproj makes this work off Windows
```

### What the Mac can test
- **It compiles.** `dotnet build` catches every code error, missing name and type mistake.
- **Logic tests.** Anything that isn't drawing, run as plain unit tests. Keep that logic in WPF-free classes so it can be tested:
  - reading `reddit-feed.json`
  - the post queue
  - the drift and easing maths
  - seamless recycling
  - the Plates brass pattern
  - layout numbers (glyph centring, whole-pixel snapping)
- **The design sheets.** The HTML mockups (in the Aang repo's `mockups/`) open in a Mac browser. Measure centres and gaps there, the same way as on the PC.
- **Samara's Reddit reader.** Its tests live in the AangApp repo (`reddit.ts`, `reddittaste.ts`) and run in Node on the Mac.

### What needs the Shadow
- How a window actually looks on the Windows desktop (DWM, the desktop layer, see-through edges).
- **The CPU gate**: the feed must drift at under 3% of one core and sit still at under 1%. Measure ShrineDock plus `dwm.exe`, 3 × 20 s, and take the middle run.
- Game sleep, full-screen quiet, and the real `%APPDATA%\Aang` bridge with Samara.
- Shipping: install through his ShrineDo task.

Never test on the Shadow while `Cemu.exe` or `Wow.exe` is running. Full-screen Samara is fine.

## Rules
- Other sessions work in this repo too: dock, Start, strip, feed. Keep each piece in its own files (the feed is `Feed*.cs`), never `git reset --hard`, and commit only your own paths.
- Never pop a dialog on his screen. Catch every error.
