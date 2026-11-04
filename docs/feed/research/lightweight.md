# Making the Reddit feed light: why the drift costs 17%, and what to build instead

2026-11-04. Research and code reading only. Nothing was run, launched or killed.

Read for this: `reports/Shrine Reddit feed build plan.md` (Phase 0), `mockups/shrine-reddit/widget-proof.html` (the page Phase 0 actually ran: R18 with a "Phase 0 proof" block at the end of its CSS), `reddit-remaster.html`, and ShrineDock's `ClockWindow.cs` and `WidgetKeeper.cs`.

## The short answer

- The 17% is not one bug. The page asks Chromium for a new frame **60 times a second, forever**. Each frame goes through four programs (page, Chromium's GPU process, Chromium's browser process, Windows' compositor DWM) and redraws the **whole see-through widget** through extra off-screen passes for the fade mask and blend modes.
- The fix is mostly the design itself. **Move in steps, not a constant crawl.** A 400 ms glide every 6 s means the widget animates about 7% of the time. Even at today's 17% while moving, that averages about **1.2% plus the 0.5% idle cost, under 2%**. Remove the per-frame extras and it drops further.
- **Memory is the part WebView2 can't fix.** Chromium is about 7 processes. With tuning, expect roughly 200–350 MB, not "far below 500". A native feed inside ShrineDock adds roughly **20–50 MB** to a process that is already running.
- **Native is not automatically cheap.** His own clock is native WPF, and it costs **about 7% of one core** at 24 fps (memory: `shrine-desktop-interview`). That's because it uses `AllowsTransparency`, a layered window that copies every frame back from the graphics card. A native feed must avoid that path.
- **My recommendation:** build the river and slate natively in ShrineDock as a see-through WPF window *without* `AllowsTransparency` (the same DWM trick Phase 0 used), with stepped motion, and bake all the looks into images. Keep `Windows.UI.Composition` as plan B if WPF misses the gate. No WebView2 in the widget at all. The open post is native too, and anything richer goes to Samara, which "Open in Samara" already does. Prove it with two small spikes before any design work (see section 5).

---

## 1. Why the drift costs 17%

Ranked by how much each is likely to cost. These are likely causes, not measured ones. Only a trace would split the 17% exactly.

1. **Nothing ever sleeps; it runs at 60 frames a second.** A Web Animations transform loop with `iterations: Infinity` keeps Chromium's compositor making frames at the display rate for as long as it drifts. Each frame costs CPU in the GPU process (recording and submitting draw commands), the browser process (frame scheduling) and DWM, even when the page's own code does nothing. VS Code hit the same wall: a blinking cursor done as a stepped CSS animation used **13% CPU while idle**, because Chromium ran the animation at 60 Hz. The fix was a `setInterval` that changes the cursor only twice a second ([vscode#22900](https://www.github.com/microsoft/vscode/issues/22900), [mitigation commit](https://github.com/Microsoft/vscode/commit/dd50ffdb940c37ea9da409c2075fc7beebbfb349), [Habr write-up](https://habr.com/en/articles/402601)). "Compositor-only" means smooth, not free.
2. **The fireflies loop still runs while hidden.** In `widget-proof.html` the canvases are `display:none`, but `fly()` still calls `requestAnimationFrame` every frame. That wakes the page's main thread 60 times a second, even when the widget is still (it's probably most of the 0.5% "still" cost). The garden wallpaper (Lively) already has fireflies, so the widget doesn't need its own.
3. **The whole widget is redrawn through off-screen passes every frame.** The moving rail sits inside `.view`, which has a `mask-image` fade. On top of that come `mix-blend-mode: screen` on the contour overlay and the knobs, and `overlay` on the grain. In Chromium, a non-normal blend mode forces a "render surface", an off-screen texture the subtree is drawn into first. Masks and animated effects do the same ([Chromium render-surface notes](https://grida.co/docs/wg/research/chromium/render-surfaces), [effect optimizations](https://grida.co/docs/wg/research/chromium/effect-optimizations), unofficial but sourced from cc code). So every frame re-composites about 404×800 CSS pixels, times the display scale squared, through two or three passes, then blends it with alpha over the desktop.
4. **New tiles are rastered while it moves.** The rail is the doubled list, thousands of pixels tall. As it slides, Chromium has to raster tiles that come into view. That includes the SVG `filter: url(#garden-tone)` on photos and the brass rim's SVG `blur(1.2px)`. URL and SVG filters are painted, not composited, so they cost raster time rather than staying on the GPU.
5. **Timers, layout and a filter fade every focus change.** A 200 ms `setInterval` reads the drift position. Each focus change runs `setFocus`, which toggles the `dim` class on every post, resizes the lens (height means layout and repaint), reads `offsetHeight` through `sizes()` (a forced layout) and starts a 0.4 s SVG-filter transition on the photo. At slow drift that's every few seconds. It's small but adds up.
6. **Video during the drift.** `setFocus` calls `video(p)`. When the sample video post comes into focus while drifting, it decodes and composites a looping video. That depends on timing, so it may have inflated part of the 20 s sample.
7. **See-through window composition.** The widget is a transparent WebView2 inside a WPF window with `DwmExtendFrameIntoClientArea(-1)`. DWM must alpha-blend the whole widget rectangle over the wallpaper each frame. The wallpaper is itself a live Lively WebView2, so DWM is already busy. This costs mostly GPU and DWM time, which **was not counted** in the 17%. The real cost is higher.
8. **Region updates (to check).** The Phase 0 proof host code was a throwaway in an old scratchpad. If it called `SetWindowRgn` on a timer or on resize, each call makes DWM recompose the window. The region should change only when the shape changes (fold/unfold), never per frame.
9. **Shadow streaming (side cost).** Every changed pixel on the desktop is also captured and encoded for the stream to his Mac. A constant crawl means constant encoding, and stepped motion helps here too.

## 2. Cheapest-by-design motion

**The rule: no animation may run unbounded. Every movement has a start and an end, and between movements nothing ticks.**

| Lever | What to do | Why |
|---|---|---|
| **Stepped glides (River)** | Every N s (6–8 s; a setting), glide up by exactly one post in about 400 ms with his `--ease` curve. Then stop. | Animating about 7% of the time instead of 100%. Even at 17% while moving that's ~1.2% + idle. Settling on a post also matches "each wheel click moves one post". |
| **Crossfade (Slate, idle)** | One big post. Every 20–30 s, a 600 ms opacity crossfade to the next (two stacked layers, the old one fades out). | Opacity is compositor/DWM-only. Active about 2–3% of the time. Expect it to be close to the idle cost. |
| **Recycle, don't double the list** | Keep about 5 posts in the tree. After each glide, move the top one to the bottom and reset the offset: one layout, once per step. | Small layer, nothing new to raster mid-glide, less GPU memory. Drops the "doubled list" and the infinite loop. |
| **Bake every look into images** | Grain, contour lines, the brass rim and studs, shadows and the garden-tone photos become PNG/JPG files. Use his `tools/*.py` pipeline for the frame art. Samara writes a toned copy of each cached photo next to the normal one. | Removes blend modes, SVG filters, `backdrop-filter` and blur, so the widget composites plain textures. Hover "real colour" becomes an opacity swap between the two photos. |
| **No forever loops** | Delete the fireflies, the cursor `nudge` keyframes and the 200 ms poll. Focus changes only when a glide ends (you know which post is on top). | The main thread sleeps between steps. |
| **`will-change` hygiene** (if web) | Use it only on the one moving element, only during a glide; set it on start and remove it at the end. | Every layer costs memory and management ([web.dev](https://web.dev/articles/stick-to-compositor-only-properties-and-manage-layer-count)). |
| **Frame-rate caps** | Chromium has no documented "max fps" switch for pages. `--disable-frame-rate-limit` only removes limits. CSS `steps()` still ticks at 60 Hz (the VS Code case). WebView2's prerelease `PreferredForegroundTimerWakeInterval` throttles **timers only**, not compositor frames ([docs](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2settings.preferredforegroundtimerwakeinterval?view=webview2-dotnet-1.0.818.41)). Chrome's Energy Saver does lower frame rates, but there's no documented number and WebView2 doesn't expose it ([How-To Geek](https://www.howtogeek.com/875326/chrome-energy-saver-mode/)). In WPF, `Timeline.SetDesiredFrameRate` works per animation (the clock uses it). | Don't count on a flag. The design must make few frames. |
| **Small window** | The window should be the frame's size plus the knobs, never larger. Folded = smaller. | Pixels blended per frame scale with area times DPI squared. |
| **Pause rules** | Game in front: hide + suspend (works, 0.5%). Full-screen app: `SHQueryUserNotificationState` (QUNS_BUSY / QUNS_RUNNING_D3D_FULL_SCREEN). Away: `GetLastInputInfo`, 3 min → still. Covered: on the bottom layer it is almost always covered by a maximised window. Do a cheap rectangle check on `EVENT_SYSTEM_FOREGROUND` / location-change hooks (the method Chromium uses for its own windows: [design doc](https://chromium.googlesource.com/chromium/src/+/master/docs/windows_native_window_occlusion_tracking.md)), then stop the glide timer. WebView2 does **not** do this for a hosted window; the host has to set `IsVisible`. Battery: N/A on the Shadow (always on mains). For a future Mac/laptop build, `RegisterPowerSettingNotification` (GUID_ACDC_POWER_SOURCE). | Most of the day the widget is covered or he's away, so it should cost nothing then. |
| **Mouse-away** | Mouse on it: stop gliding (already designed). Mouse gone for 1.5 s: resume steps. Away for 3 min: fold to the Slate. | Matches the design: the river while in use, the slate when idle. |

## 3. The native alternative

### Option N1: plain WPF, but not a layered window (recommended)
- **How:** a `Window` with `AllowsTransparency = false`, `WindowStyle.None`, `Background = Transparent`, `HwndSource.CompositionTarget.BackgroundColor = Transparent` and `DwmExtendFrameIntoClientArea(-1)`, the same trick Phase 0 proved, plus `SetWindowRgn` for click-through. WPF then renders on the GPU straight into DWM, with **no per-frame copy back to system memory**.
- **Why not `AllowsTransparency`:** layered windows render on the GPU and then read each frame back to system memory for `UpdateLayeredWindow`. Microsoft's own WPF team called that readback the bottleneck ([Seema, "Layered windows… SW is sometimes faster than HW"](https://learn.microsoft.com/en-gb/archive/blogs/seema/layered-windows-sw-is-sometimes-faster-than-hw), [WPF 3.5 SP1 perf notes](https://learn.microsoft.com/en-us/archive/blogs/jgoldb/performance-improvements-in-wpf-in-net-3-5-3-0-sp1)). That's the likely reason his clock costs 7% at 24 fps. **Side win:** the clock could move to the same trick, which is worth measuring.
- **Motion:** posts are `Border`s with baked `ImageBrush` art, `CacheMode = BitmapCache`, moved by a `TranslateTransform` `DoubleAnimation` with `DesiredFrameRate` 60 only during a glide. WPF animations tick on the UI thread, so a glide costs some CPU while it runs. Stepped, it averages low.
- **Expected:** a few % of one core during a 400 ms glide, so about **0.3–1% averaged**, and about 0% still, because WPF renders nothing when nothing changes. These are my estimates, not sourced figures; the gate in section 5 decides. Memory: **+20–50 MB** in ShrineDock (decode photos at display size with `DecodePixelWidth`: 30 posts × ~0.3–0.6 MB).
- **Effort:** about the same as the web Phase 3 (~10–14 h). ShrineDock is already WPF, and the clock, strip and settings show the patterns.

### Option N2: Windows.UI.Composition (plan B, lowest CPU)
- **How:** a window with `WS_EX_NOREDIRECTIONBITMAP` (no opaque redirection surface, so true per-pixel alpha on the GPU: [Kenny Kerr, MSDN Magazine 2014](https://msdn.microsoft.com/en-us/magazine/dn745861.aspx)). A `DesktopWindowTarget` from the system compositor ([Visual Layer with Win32](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/using-the-visual-layer-with-win32)). Each post is a `SpriteVisual` holding a surface you draw once (render the card with WPF `RenderTargetBitmap`, upload through D2D/D3D interop). Glide = `Vector3KeyFrameAnimation` on the container's offset; crossfade = `ScalarKeyFrameAnimation` on opacity. The garden tone can even be a GPU colour-matrix effect brush. Video: `MediaPlayer.GetSurface(compositor)` ([docs](https://learn.microsoft.com/en-us/uwp/api/windows.media.playback.mediaplayer.getsurface)).
- **Why cheaper:** composition animations run on the system compositor, **independent of the app's UI thread** ([composition animation docs](https://learn.microsoft.com/en-us/windows/uwp/composition/composition-animation)). The app process does nothing per frame. The cost moves into DWM, so **DWM must be in the measurement**.
- **Expected:** host ≈ 0% while gliding, plus a small DWM delta (a handful of textured quads). Memory about +20–50 MB.
- **Effort:** about 20–28 h. Interop for drawing surfaces, your own hit-testing (visuals don't take mouse input), and text drawn into bitmaps.

### What native loses (both options)
- HTML/CSS layout: `text-wrap: pretty`, line clamping, `border-radius` per corner, CSS gradients. WPF covers most of it; N2 needs every card drawn to a bitmap.
- The design loop: his mockups are HTML, and "compare against the sheet" means lifting computed values. Native needs a pixel check against the sheet instead.
- Rich content: Reddit markdown (render bold/italic/links/paragraphs natively, the rest as plain text), animated GIFs (convert to mp4 in Samara), embedded players (send to Samara).
- Video: fine for local mp4s (Samara already caches them). WPF `MediaElement` on a non-layered window, or a MediaPlayer surface in N2.

### Hybrid: native river + WebView2 only when a post opens
- A cold WebView2 environment takes noticeable time to start, so opening "in place" would lag, and keeping it warm brings the memory back. A fully native open view (title, body, 2 comments, votes) is simple. "Open in Samara" already exists for anything richer. **I wouldn't build the hybrid** unless the open view later needs real web content.

### The honest comparison

| | WebView2, fixed (stepped, baked) | WPF native, not layered (N1) | Composition (N2) |
|---|---|---|---|
| CPU still | ~0.5% measured | ~0% (est.) | ~0% (est.) |
| CPU while moving, averaged | ~1–2% (est. from 17% × 7% + 0.5%) | ~0.3–1% (est.) | host ~0%, DWM small (est.) |
| Memory | 200–350 MB after tuning (est.); 450–580 measured | +20–50 MB (est.) | +20–50 MB (est.) |
| Effort | ~10 h (Phase 3 as planned) | ~10–14 h | ~20–28 h |
| Look fidelity vs HTML sheet | exact | close, re-built | close, more work |
| Risk | memory goal missed | WPF glide smoothness at 60 fps on the Shadow | interop bugs |

All the "est." cells are reasoning, not measurements. That's why section 5 measures before building.

## 4. Memory

**WebView2, if it stays:**
- **One environment, one user data folder** for everything ShrineDock hosts. Processes are shared per user data folder; each extra folder creates a whole new set ([process model](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-model)).
- `AdditionalBrowserArguments`: `--renderer-process-limit=1`, plus `--disable-extensions`. `--in-process-gpu` saves a process but a GPU crash then takes the browser down. Test each and measure; WebView2 doesn't guarantee every Chromium switch.
- `MemoryUsageTargetLevel = Low` while the mouse is away; it lets the runtime swap out memory and scripts keep running. Don't mix it with `TrySuspendAsync`, which sets Low itself ([docs](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.memoryusagetargetlevel)).
- `TrySuspendAsync` in games (already works). Load pictures at display size. One video element at most.
- **Expected floor:** browser + GPU + network/utility + one renderer + crashpad is still a few hundred MB of working set. "Far below 500" is unlikely. I found no official per-process figures; read them from `GetProcessInfos` or the Browser Task Manager.

**Native:** ShrineDock is already running and paid for. The feed adds its bitmaps and text: roughly 20–50 MB.

## 5. Recommended architecture and the measurement gate

**Architecture:** the Feed is a ShrineDock window (N1): non-layered, see-through via DWM, cut to the frame with a region set only on fold/unfold, kept at the bottom like the clock, and kept in place by WidgetKeeper. River = stepped glides of ~5 recycled cards; Slate = two stacked cards that crossfade. Hover rise = a 200 ms scale/translate, and the brass frame is one baked 9-slice image that glides to the post. All art is baked (Python tools for the frame, Samara for toned photos). The open post is native. Samara stays the only program that talks to Reddit (unchanged). Pause rules: game → hide; full-screen → hide; covered → stop timers; away 3 min → Slate; mouse on → stop.

**The gate (same method as Phase 0, plus DWM):**
1. Pause all tests if Cemu or Wow.exe is running (tasklist check first).
2. Wait 10 s for things to settle, then take two snapshots 20 s apart, three runs, and use the middle one.
3. CPU % of one core = Δ(TotalProcessorTime, in seconds) / 20 × 100. Sum: ShrineDock + every `msedgewebview2.exe` whose parent chain leads to it (Win32_Process ParentProcessId; zero for native). Report **dwm.exe separately**, as the delta against a baseline run with the feed hidden.
4. GPU (when relevant): `typeperf "\GPU Engine(*)\Utilization Percentage" -si 1 -sc 20`, filtered to the pids above and dwm.exe. Use PresentMon only if a glide looks uneven.
5. Memory: the sum of Private Working Set of the same pids, compared with ShrineDock without the feed.
6. Pass: **still ≤ 1%**, **gliding (steps on) ≤ 3% averaged**, the DWM delta ≤ 1%, memory ≤ +80 MB (native) / record it (web).

```powershell
# sketch (do not run while he plays)
$h = Get-Process ShrineDock; $ids = @($h.Id)   # not $host: that name is reserved in PowerShell
$all = Get-CimInstance Win32_Process
do { $n = $ids.Count; $ids += ($all | ? { $ids -contains $_.ParentProcessId -and $ids -notcontains $_.ProcessId }).ProcessId } while ($ids.Count -gt $n)
$t = { ($ids | % { (Get-Process -Id $_ -EA 0).TotalProcessorTime.TotalSeconds } | Measure -Sum).Sum }
$d = { (Get-Process dwm).TotalProcessorTime.TotalSeconds }
$a = & $t; $da = & $d; Start-Sleep 20; $b = & $t; $db = & $d
"app {0:N2}%  dwm {1:N2}%" -f (($b-$a)/20*100), (($db-$da)/20*100)
```

**Phased plan to prove it before any design work:**

| Step | What | Time | Decides |
|---|---|---|---|
| P1 | **Native spike (N1):** a ShrineDock test window, 5 grey cards with one baked frame PNG, a 400 ms glide every 6 s, a crossfade every 20 s, hover rise. Measure still / gliding / DWM / memory. | 3–4 h | If it passes, it's the path. |
| P2 | *Only if P1 glides look rough or cost too much:* the same spike on Windows.UI.Composition (N2). | 5–6 h | Plan B. |
| P3 | *Optional control:* the web page stripped to the same skeleton (stepped glide, no fireflies, blends or masks) in the Phase 0 host, measured the same way. | 2 h | Gives a fair web number if he wants to keep HTML fidelity and accept the memory. |
| P4 | Build the real River + Slate on the winning path, against the approved sheets. | 10–14 h (N1) | Phase 3 + 4 merged. |
| P5 | Re-run the gate on his real desktop, never while he's in a game. Record it in the build plan. | 1 h | Phase 3 "done". |

## Sources
- [VS Code #22900: 13% CPU from a blinking cursor](https://www.github.com/microsoft/vscode/issues/22900), [mitigation commit](https://github.com/Microsoft/vscode/commit/dd50ffdb940c37ea9da409c2075fc7beebbfb349), [Habr write-up](https://habr.com/en/articles/402601)
- [web.dev: compositor-only properties and layer count](https://web.dev/articles/stick-to-compositor-only-properties-and-manage-layer-count)
- [Chromium render surfaces (unofficial notes)](https://grida.co/docs/wg/research/chromium/render-surfaces), [effect optimizations](https://grida.co/docs/wg/research/chromium/effect-optimizations)
- [Chromium: Windows native window occlusion detection](https://chromium.googlesource.com/chromium/src/+/master/docs/windows_native_window_occlusion_tracking.md)
- [WebView2 process model](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-model), [MemoryUsageTargetLevel](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.memoryusagetargetlevel), [PreferredForegroundTimerWakeInterval (prerelease)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2settings.preferredforegroundtimerwakeinterval?view=webview2-dotnet-1.0.818.41), [DefaultBackgroundColor](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2controller.defaultbackgroundcolor), [CreateCoreWebView2CompositionControllerAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2environment.createcorewebview2compositioncontrollerasync)
- [Layered windows: SW sometimes faster than HW (readback)](https://learn.microsoft.com/en-gb/archive/blogs/seema/layered-windows-sw-is-sometimes-faster-than-hw), [WPF 3.5 SP1 performance notes](https://learn.microsoft.com/en-us/archive/blogs/jgoldb/performance-improvements-in-wpf-in-net-3-5-3-0-sp1)
- [Kenny Kerr: DirectComposition and WS_EX_NOREDIRECTIONBITMAP](https://msdn.microsoft.com/en-us/magazine/dn745861.aspx), [Visual Layer with Win32](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/using-the-visual-layer-with-win32), [Composition animations](https://learn.microsoft.com/en-us/windows/uwp/graphics/composition-animation), [MediaPlayer.GetSurface](https://learn.microsoft.com/en-us/uwp/api/windows.media.playback.mediaplayer.getsurface)
- [Chrome Energy Saver (no documented fps number)](https://www.howtogeek.com/875326/chrome-energy-saver-mode/)
- Local: ShrineDock `ClockWindow.cs` (AllowsTransparency, 24 fps sweep), memory note "native clock ≈ 7% of one core"; `widget-proof.html` (fireflies rAF still running while hidden, mask + blend modes on the moving rail, 200 ms poll, video on focus).
