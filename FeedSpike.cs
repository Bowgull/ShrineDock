using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ShrineDock;

// GREY-BOX CPU GATE for the native Reddit feed (2026-11-04). Not part of the normal dock: it only runs as its own
// process via `ShrineDock.exe --feed-spike <mode>` and never touches the dock, the clock or the keeper.
//  modes: drift (continuous slow drift, --speed px/s, --fps), river (glide one post up every 6-8 s, 400 ms ease-out), slate (one big post, 600 ms crossfade every
//         20-30 s), still (river cards, nothing moves), none (no window: the process baseline for memory/DWM)
//  --fast  uses the fastest end of each range (6 s / 20 s) so a 20 s sample is the worst case
// The window is NOT layered (no AllowsTransparency): it is see-through through DWM (frame extended into the whole
// client area) and cut to its rounded shape by a region set once. All card art is baked into bitmaps once at start;
// between movements nothing ticks (no per-frame code, no timers besides the one that starts the next movement).
static class FeedSpike
{
    public static void Run(string[] args)
    {
        Log("spike start: " + string.Join(" ", args));
        string mode = args.SkipWhile(a => a != "--feed-spike").Skip(1).FirstOrDefault() ?? "river";
        bool fast = args.Contains("--fast");
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // never a dialog on his screen: everything is logged and swallowed
        app.DispatcherUnhandledException += (_, e) => { Log(e.Exception.ToString()); e.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject?.ToString() ?? "?");
        TaskScheduler.UnobservedTaskException += (_, e) => { Log(e.Exception.ToString()); e.SetObserved(); };
        if (mode != "none") app.Dispatcher.BeginInvoke(() => { try { new FeedSpikeWindow(mode, fast).Show(); } catch (Exception ex) { Log(ex.ToString()); } });
        // the gate script ends it with Stop-Process; this is a backstop so a forgotten spike never lingers
        var bye = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) }; bye.Tick += (_, _) => app.Shutdown(); bye.Start();
        app.Run();
    }
    public static void Log(string m) { try { File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "feed-spike.log"), $"{DateTime.Now:T} {m}\n"); } catch { } }
}

class FeedSpikeWindow : Window
{
    const double W = 380, CardW = 340, CardH = 150, Gap = 14, Pad = 20, SlateH = 420;
    const int Shown = 4;                                   // posts in view; the 5th waits below to glide in
    static readonly double Step = CardH + Gap;
    readonly string mode; readonly bool fast;
    readonly Canvas rail = new();
    readonly TranslateTransform railY = new();
    readonly List<FrameworkElement> cards = [];
    readonly Image slateA = new(), slateB = new();
    readonly List<BitmapSource> slateArt = [];
    int slateNext = 1; bool aOnTop = true;
    readonly DispatcherTimer next = new();
    readonly Random rnd = new(7);
    bool gliding, mouseOn;
    IntPtr me;
    // drift mode: one long linear animation of the rail (WPF animates it; no app code per frame), eased to a halt on
    // hover and eased back on leave; a card that has left the top is moved under the last one, out of sight
    readonly double speed = 9; readonly int fps = 60; int driftGen;
    readonly DispatcherTimer recycle = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly List<(double t, double y)> frames = [];

    public FeedSpikeWindow(string mode, bool fast)
    {
        this.mode = mode; this.fast = fast;
        var argv = Environment.GetCommandLineArgs();
        string? Arg(string k) => argv.SkipWhile(a => a != k).Skip(1).FirstOrDefault();
        if (double.TryParse(Arg("--speed"), System.Globalization.CultureInfo.InvariantCulture, out var sp)) speed = sp;
        if (int.TryParse(Arg("--fps"), out var f)) fps = f;
        WindowStyle = WindowStyle.None; AllowsTransparency = false; ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Title = "Shrine Feed spike";
        double h = mode == "slate" ? SlateH + 2 * Pad : Shown * Step - Gap + 2 * Pad;
        Width = W; Height = h;
        var wa = SystemParameters.WorkArea; Left = wa.Right - W - 60; Top = wa.Top + 120;

        // the frame: one baked bitmap (stands in for the brass 9-slice PNG)
        var root = new Grid { Width = W, Height = h };
        root.Children.Add(new Image { Source = Bake(W, h, 18, Color.FromArgb(0x94, 0x1A, 0x20, 0x24), Color.FromArgb(0xC0, 0x8A, 0x7A, 0x55), 3, null), IsHitTestVisible = false });
        var view = new Canvas { Width = CardW, Height = h - 2 * Pad, ClipToBounds = true, Margin = new Thickness(Pad), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        root.Children.Add(view);

        if (mode == "slate")
        {
            for (int i = 0; i < 5; i++) slateArt.Add(Bake(CardW, SlateH, 12, Grey(i), Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF), 1, $"Slate post {i + 1}"));
            slateA.Source = slateArt[0]; slateB.Source = slateArt[1]; slateB.Opacity = 0;
            foreach (var s in new[] { slateB, slateA }) { s.Width = CardW; s.Height = SlateH; s.CacheMode = new BitmapCache(); view.Children.Add(s); }
        }
        else
        {
            // 5 cards, recycled: after each glide the top one moves to the bottom (one layout per step, no doubled list)
            rail.RenderTransform = railY; view.Children.Add(rail);
            for (int i = 0; i < Shown + 1; i++)
            {
                var img = new Image { Source = Bake(CardW, CardH, 12, Grey(i), Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF), 1, $"Post {i + 1}"), Width = CardW, Height = CardH };
                var lift = new TranslateTransform();
                var card = new Border { Child = img, RenderTransform = lift, CacheMode = new BitmapCache() };
                card.MouseEnter += (_, _) => Lift(lift, -3); card.MouseLeave += (_, _) => Lift(lift, 0);
                Canvas.SetTop(card, i * Step); rail.Children.Add(card); cards.Add(card);
            }
            rail.CacheMode = new BitmapCache();
        }
        // mouse on: no gliding (the design); mouse gone: carry on. Drift eases to a halt and back instead.
        MouseEnter += (_, _) => { mouseOn = true; if (mode == "drift") Ease(stop: true); };
        MouseLeave += (_, _) => { mouseOn = false; if (mode == "drift") Ease(stop: false); };
        Content = root;

        SourceInitialized += (_, _) =>
        {
            me = new WindowInteropHelper(this).Handle;
            SetWindowLong(me, -20, GetWindowLong(me, -20) | 0x80 | 0x08000000);   // tool window, never takes focus
            var src = HwndSource.FromHwnd(me);
            src.CompositionTarget.BackgroundColor = Colors.Transparent;
            var m = new MARGINS { L = -1, R = -1, T = -1, B = -1 }; DwmExtendFrameIntoClientArea(me, ref m);
            src.AddHook(KeepAtBottom);
        };
        Loaded += (_, _) =>
        {
            // the region, once: the frame's rounded shape (never touched again, not on a timer)
            var dpi = VisualTreeHelper.GetDpi(this);
            int pw = (int)Math.Ceiling(W * dpi.DpiScaleX), ph = (int)Math.Ceiling(h * dpi.DpiScaleY), rr = (int)(36 * dpi.DpiScaleX);
            SetWindowRgn(me, CreateRoundRectRgn(0, 0, pw + 1, ph + 1, rr, rr), true);
            SetWindowPos(me, (IntPtr)1, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            if (mode is "river" or "slate") Arm();
            if (mode == "drift") { DriftFrom(0); recycle.Tick += (_, _) => Recycle(); recycle.Start(); }
            if (argv.Contains("--framelog")) FrameLog(argv.Contains("--hovertest"));
            FeedSpike.Log($"spike up: mode={mode} fast={fast} {pw}x{ph}px");
        };
        next.Tick += (_, _) => { next.Stop(); try { if (mode == "slate") Fade(); else Glide(); } catch (Exception ex) { FeedSpike.Log(ex.ToString()); Arm(); } };
    }

    void DriftFrom(double y)
    {
        driftGen++;
        var a = new DoubleAnimation(y, y - speed * 3600, TimeSpan.FromSeconds(3600));
        Timeline.SetDesiredFrameRate(a, fps);
        railY.BeginAnimation(TranslateTransform.YProperty, a);
    }

    // a quadratic ease over 600 ms covering speed*0.3 px starts (or ends) at exactly the drift speed: no jolt
    void Ease(bool stop)
    {
        int gen = ++driftGen; double y = railY.Y;
        var a = new DoubleAnimation(y, y - speed * 0.3, TimeSpan.FromMilliseconds(600)) { EasingFunction = new QuadraticEase { EasingMode = stop ? EasingMode.EaseOut : EasingMode.EaseIn } };
        Timeline.SetDesiredFrameRate(a, fps);
        if (!stop) a.Completed += (_, _) => { if (gen == driftGen) DriftFrom(railY.Y); };
        railY.BeginAnimation(TranslateTransform.YProperty, a);
    }

    void Recycle()
    {
        double y = railY.Y, bottom = cards.Max(Canvas.GetTop);
        foreach (var c in cards.OrderBy(Canvas.GetTop).ToList())
            if (Canvas.GetTop(c) + y < -CardH - 4) { bottom += Step; Canvas.SetTop(c, bottom); FeedSpike.Log($"recycled at y={y:F1}"); }
    }

    // diagnostic only (never in a gate run): logs every frame's time and rail offset for 40 s, then the stats
    void FrameLog(bool hoverTest)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        EventHandler h = (_, _) => frames.Add((sw.Elapsed.TotalMilliseconds, railY.Y));
        CompositionTarget.Rendering += h;
        if (hoverTest)
        {
            var ht = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            ht.Tick += (_, _) => { mouseOn = !mouseOn; Ease(stop: mouseOn); FeedSpike.Log($"hovertest {(mouseOn ? "halt" : "resume")} at {sw.Elapsed.TotalMilliseconds:F0}ms"); };
            ht.Start();
        }
        var done = new DispatcherTimer { Interval = TimeSpan.FromSeconds(40) };
        done.Tick += (_, _) =>
        {
            done.Stop(); CompositionTarget.Rendering -= h;
            var gaps = frames.Zip(frames.Skip(1), (a, b) => b.t - a.t).ToList();
            var steps = frames.Zip(frames.Skip(1), (a, b) => a.y - b.y).ToList();
            FeedSpike.Log($"frames={frames.Count} gapAvg={gaps.Average():F1}ms gapMax={gaps.Max():F1}ms gaps>25ms={gaps.Count(g => g > 25)} gaps>40ms={gaps.Count(g => g > 40)}");
            FeedSpike.Log($"step px/frame: min={steps.Min():F3} max={steps.Max():F3} backwards={steps.Count(s => s < -0.001)}");
            File.WriteAllLines(System.IO.Path.Combine(AppContext.BaseDirectory, "frames.csv"), frames.Select(f => $"{f.t:F2},{f.y:F3}"));
        };
        done.Start();
    }

    void Arm()
    {
        double s = mode == "slate" ? (fast ? 20 : 20 + rnd.NextDouble() * 10) : (fast ? 6 : 6 + rnd.NextDouble() * 2);
        next.Interval = TimeSpan.FromSeconds(s); next.Start();
    }

    void Glide()
    {
        if (gliding || mouseOn) { Arm(); return; }
        gliding = true;
        var a = new DoubleAnimation(0, -Step, TimeSpan.FromMilliseconds(400)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Timeline.SetDesiredFrameRate(a, 60);
        a.Completed += (_, _) =>
        {
            // recycle: the top card goes to the bottom, everything shifts up one slot, the offset returns to 0
            var top = cards[0]; cards.RemoveAt(0); cards.Add(top);
            for (int i = 0; i < cards.Count; i++) Canvas.SetTop(cards[i], i * Step);
            railY.BeginAnimation(TranslateTransform.YProperty, null); railY.Y = 0;
            gliding = false; Arm();
        };
        railY.BeginAnimation(TranslateTransform.YProperty, a);
    }

    void Fade()
    {
        // two stacked layers: the next post waits underneath, the top one fades out, then they swap roles
        var top = aOnTop ? slateA : slateB; var under = aOnTop ? slateB : slateA;
        under.Opacity = 1;
        var a = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(600));
        Timeline.SetDesiredFrameRate(a, 60);
        a.Completed += (_, _) =>
        {
            top.BeginAnimation(OpacityProperty, null); top.Opacity = 0;
            slateNext = (slateNext + 1) % slateArt.Count; top.Source = slateArt[slateNext];
            var v = (Canvas)top.Parent; v.Children.Remove(top); v.Children.Insert(0, top);   // the old top goes underneath
            aOnTop = !aOnTop; Arm();
        };
        top.BeginAnimation(OpacityProperty, a);
    }

    static void Lift(TranslateTransform t, double to)
    {
        var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        t.BeginAnimation(TranslateTransform.YProperty, a);
    }

    static Color Grey(int i) { byte g = (byte)(0x3A + i * 9); return Color.FromArgb(0xE6, g, g, (byte)(g + 4)); }

    // draws a card once into a frozen bitmap; after this nothing about it is ever redrawn
    static BitmapSource Bake(double w, double h, double r, Color fill, Color edge, double edgeW, string? label)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var pen = new Pen(new SolidColorBrush(edge), edgeW);
            dc.DrawRoundedRectangle(new SolidColorBrush(fill), pen, new Rect(edgeW / 2, edgeW / 2, w - edgeW, h - edgeW), r, r);
            if (label != null)
            {
                var light = new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF));
                dc.DrawRoundedRectangle(light, null, new Rect(14, 14, 28, 28), 14, 14);              // sub emblem
                dc.DrawRoundedRectangle(light, null, new Rect(52, 18, w * .45, 8), 4, 4);             // sub name
                dc.DrawRoundedRectangle(light, null, new Rect(14, 54, w - 28, 12), 6, 6);             // title
                dc.DrawRoundedRectangle(light, null, new Rect(14, 74, w * .6, 12), 6, 6);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)), null, new Rect(14, 100, w - 28, h - 114), 8, 8); // photo
                var t = new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, Brushes.White, 1.0);
                dc.DrawText(t, new Point(w - t.Width - 14, 16));
            }
        }
        // bake at 1.5x so it stays crisp at his 150% scale
        var bmp = new RenderTargetBitmap((int)(w * 1.5), (int)(h * 1.5), 144, 144, PixelFormats.Pbgra32);
        bmp.Render(dv); bmp.Freeze(); return bmp;
    }

    [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int L, R, T, B; }
    [StructLayout(LayoutKind.Sequential)] struct WINDOWPOS { public IntPtr hwnd, after; public int x, y, cx, cy; public uint flags; }
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr h, IntPtr rgn, bool redraw);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    IntPtr KeepAtBottom(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == 0x0046)
        {
            var p = Marshal.PtrToStructure<WINDOWPOS>(l); p.after = (IntPtr)1; p.flags &= ~0x0004u; Marshal.StructureToPtr(p, l, false);
        }
        return IntPtr.Zero;
    }
}
