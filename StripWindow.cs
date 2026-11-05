using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ShrineDock;

// The Shrine top strip (sheet 08, dock v8 language): every open window in its own small clock-style frame,
// hidden until the mouse touches the top edge of screen 1. Click focuses or minimises, right-click gives the
// window's own menu, and resting on a tile shows its name and a live preview. It also keeps the Windows taskbar
// hidden while it runs, and gives it back when it closes.
class StripWindow : Window
{
    const double Tile = 52, Gap = 9, Pad = 8, Icon = 28, PreviewW = 480, PreviewH = 300; // 2026-11-02: up from 260x160, too small to read what a window shows
    readonly Canvas root = new(), row = new();
    readonly TranslateTransform rise = new();
    readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromMilliseconds(33) };
    readonly DispatcherTimer hideTimer = new(), labelTimer = new(), openIntent = new() { Interval = TimeSpan.FromMilliseconds(80) };
    readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly Border label = new();
    readonly Grid previewFrame = PreviewFrame();
    const double PreviewPad = 20; // room kept in the strip's height for the frame around the preview
    // sheet 15 A (his pick, 2026-11-02): the picture tucks 10 px in, under the gold rail, and the rail and seals
    // (frame-rail.png, the frame with its slate cut away) are drawn above it in their own see-through window, since
    // Windows paints a live preview over everything in the strip's own window
    const double Rail = 10;
    RailWindow? rail;
    readonly TextBlock labelText = new();
    readonly List<(IntPtr hwnd, Border tile, ScaleTransform scale, TranslateTransform lift, Border pip)> tiles = [];
    Func<Settings> settings;
    bool open, asleepForGame;
    int hovered = -1, tick;
    IntPtr thumb, me;
    double winW, winH;
    Rect area;
    readonly bool primary;

    static readonly ImageBrush TileBrush = Frozen(new ImageBrush(Art("strip-tile.png"))), TileHotBrush = Frozen(new ImageBrush(Art("strip-tile-hot.png")));
    static readonly Color Cyan = Color.FromRgb(0x38, 0xC6, 0xFF);
    static ImageBrush Frozen(ImageBrush b) { b.Freeze(); return b; }
    static BitmapImage Art(string f)
    {
        var b = new BitmapImage(); b.BeginInit(); b.UriSource = new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "art", f)); b.CacheOption = BitmapCacheOption.OnLoad; b.EndInit(); b.Freeze(); return b;
    }

    public StripWindow(Func<Settings> settings, Rect area, bool primary)
    {
        this.settings = settings; this.area = area; this.primary = primary;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; Topmost = true; ResizeMode = ResizeMode.NoResize; ShowActivated = false; Title = "Shrine Strip";
        winW = area.Width; winH = Pad + Tile + 54 + PreviewH + 2 * PreviewPad + 30; // room for the framed preview and its shadow
        Width = winW; Height = winH; Left = area.Left; Top = area.Top;
        Content = root;
        row.RenderTransform = rise; row.Opacity = 0; rise.Y = -(Tile + Pad + 20);
        root.Children.Add(row);
        // the name plate and the preview frame under a hovered tile
        labelText.FontFamily = new FontFamily("Exo 2, Segoe UI"); labelText.FontStyle = FontStyles.Italic; labelText.FontWeight = FontWeights.SemiBold; labelText.FontSize = 13;
        labelText.Foreground = new SolidColorBrush(Color.FromRgb(0xC4, 0xF2, 0xFF)); labelText.TextTrimming = TextTrimming.CharacterEllipsis; labelText.MaxWidth = PreviewW - 20;
        label.Child = labelText; label.Padding = new Thickness(10, 4, 10, 5); label.CornerRadius = new CornerRadius(8);
        label.Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x0A, 0x1A, 0x28)); label.BorderBrush = new SolidColorBrush(Color.FromRgb(0xB8, 0x92, 0x4C)); label.BorderThickness = new Thickness(1.5);
        label.Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 10, ShadowDepth = 0, Opacity = .45 };
        label.Opacity = 0; label.IsHitTestVisible = false;
        previewFrame.Opacity = 0; previewFrame.IsHitTestVisible = false;
        root.Children.Add(previewFrame); root.Children.Add(label);

        Loaded += (_, _) =>
        {
            me = new WindowInteropHelper(this).Handle; SetClickThrough(true); if (primary) { HideTaskbar(); WatchTaskbar(); }
            // Explorer restarting brings the Windows taskbar back (2026-11-02): hide it again once it is rebuilt
            if (primary) HwndSource.FromHwnd(me).AddHook((IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) =>
            {
                if (msg == TaskbarCreated) Dispatcher.BeginInvoke(() => { HideTaskbar(); WatchTaskbar(); }, DispatcherPriority.Background); // a new Explorer, a new pid to watch
                return IntPtr.Zero;
            });
        };
        Closed += (_, _) => { poll.Stop(); refresh.Stop(); hideTimer.Stop(); labelTimer.Stop(); openIntent.Stop(); HidePreview(); };
        poll.Tick += (_, _) => Track(); poll.Start();
        hideTimer.Tick += (_, _) => { hideTimer.Stop(); if (!PointerOver()) Sink(); };
        labelTimer.Tick += (_, _) => { labelTimer.Stop(); if (hovered >= 0) ShowPreview(hovered); };
        openIntent.Tick += (_, _) => { openIntent.Stop(); if (AtTopEdge()) Rise(); };
        refresh.Tick += (_, _) => Fill();
        bubbleTimer.Tick += (_, _) => { bubbleTimer.Stop(); bubble.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250)) { FillBehavior = FillBehavior.Stop }); bubble.Opacity = 0; };
        if (primary)
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => ShowTaskbar();
            System.Windows.Application.Current.SessionEnding += (_, _) => ShowTaskbar();
        }
    }

    // ---- the Windows taskbar stays out of sight while the Shrine strip runs
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string? title);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int RegisterWindowMessage(string s);
    static readonly int TaskbarCreated = RegisterWindowMessage("TaskbarCreated");
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    static IEnumerable<IntPtr> Taskbars()
    {
        var t = FindWindow("Shell_TrayWnd", null); if (t != IntPtr.Zero) yield return t;
        var s = IntPtr.Zero;
        while ((s = FindWindowEx(IntPtr.Zero, s, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero) yield return s;
    }
    static void HideTaskbar() { foreach (var t in Taskbars()) if (IsWindowVisible(t)) ShowWindow(t, 0); }

    // Explorer shows its taskbar again when an app flashes for attention, on the Win key, etc. The half-second poll left it
    // on screen until the next check, a visible flash (2026-11-05, his report). Hide it the moment Explorer shows it instead.
    delegate void WinEventProc(IntPtr hook, uint ev, IntPtr h, int idObject, int idChild, uint thread, uint time);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
    static WinEventProc? taskbarShown; // held so the callback isn't collected
    static IntPtr taskbarHook;
    static void WatchTaskbar()
    {
        try
        {
            if (taskbarHook != IntPtr.Zero) { UnhookWinEvent(taskbarHook); taskbarHook = IntPtr.Zero; }
            var tray = FindWindow("Shell_TrayWnd", null); if (tray == IntPtr.Zero) return;
            GetWindowThreadProcessId(tray, out var explorer);
            taskbarShown ??= (_, _, h, idObject, _, _, _) =>
            {
                try
                {
                    if (idObject != 0) return; // the window itself, not a part of it
                    var c = new StringBuilder(64); GetClassName(h, c, 64);
                    if (c.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") ShowWindow(h, 0);
                }
                catch { }
            };
            taskbarHook = SetWinEventHook(0x8002, 0x8002, IntPtr.Zero, taskbarShown, explorer, 0, 0); // EVENT_OBJECT_SHOW, Explorer only
        }
        catch { }
    }
    public static void ShowTaskbar() { foreach (var t in Taskbars()) ShowWindow(t, 5); }

    // ---- where the mouse is
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT p, uint flags);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    struct POINT { public int X, Y; }
    Point MousePos() { GetCursorPos(out var p); var d = VisualTreeHelper.GetDpi(this); return new Point(p.X / d.DpiScaleX, p.Y / d.DpiScaleY); }
    Point Local() { var c = MousePos(); return new Point(c.X - area.Left, c.Y - area.Top); }
    bool AtTopEdge() { var c = Local(); return c.Y >= 0 && c.Y <= 2 && c.X >= 80 && c.X <= winW - 80; }
    public void MoveTo(Rect a)
    {
        if (a == area) return;
        area = a; winW = a.Width; Width = winW; Left = a.Left; Top = a.Top; Sink();
    }
    bool PointerOver()
    {
        var c = Local(); if (c.X < 0 || c.X > winW || c.Y < 0) return false;
        if (tiles.Count == 0 && speaker == null) return c.Y <= Pad + Tile + 20;
        double x0 = (clock != null ? Canvas.GetLeft(clock) : tiles.Count > 0 ? Canvas.GetLeft(tiles[0].tile.Parent as UIElement) : Canvas.GetLeft(speaker)) - 30;
        double x1 = (speaker != null ? Canvas.GetLeft(speaker) : Canvas.GetLeft(tiles[^1].tile.Parent as UIElement)) + Tile + 30;
        double bottom = hovered >= 0 && previewFrame.Opacity > 0 ? Pad + Tile + 54 + PreviewH + 2 * PreviewPad + 10 : Pad + Tile + 24;
        return c.Y <= bottom && c.X >= x0 && c.X <= x1;
    }

    void Track()
    {
        if (++tick % 15 == 0) CheckGame();   // half a second, so alt-tab out brings it back quickly
        // keep the Windows taskbar hidden even while a game is in front (2026-11-04: BotW on the Sansui let the Acer's taskbar peek)
        if (tick % 15 == 0 && primary) HideTaskbar();
        if (asleepForGame) return;
        if (!open) { if (AtTopEdge()) { if (!openIntent.IsEnabled) openIntent.Start(); } else openIntent.Stop(); return; }
        if (PointerOver()) hideTimer.Stop();
        else if (!hideTimer.IsEnabled) { hideTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(150, settings().HideDelayMs)); hideTimer.Start(); }
    }

    void Rise()
    {
        if (open || asleepForGame) return;
        Fill(); open = true; SetClickThrough(false); refresh.Start(); ToFront();
        Program.Log($"strip on {area} rose with {tiles.Count} windows");
        rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        row.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)));
    }
    // other always-on-top windows (Samara's pop-out, Rainmeter, Aang) stack by whoever came last, so the strip and its
    // preview step back in front of them each time they show (2026-11-02, his report), without taking the focus
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    void ToFront() { if (me != IntPtr.Zero) SetWindowPos(me, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); }
    void Sink()
    {
        if (!open) return;
        open = false; Hover(-1); refresh.Stop();
        rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-(Tile + Pad + 20), TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });
        row.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(240)));
        SetClickThrough(true);
    }

    // ---- the open windows, in the order Windows keeps them (stable: new ones join at the end)
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int size);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    readonly List<IntPtr> order = [];
    int hereCount = -1;
    const double AwayGap = 18;
    // titleEnds: for apps that share their process with others (Samara is one of Aang's electron windows)
    internal static IntPtr WindowOf(string process, string? titleEnds = null)
    {
        foreach (var h in OpenWindows())
        {
            GetWindowThreadProcessId(h, out var pid); var exe = ExePath(pid);
            if (exe == null || !System.IO.Path.GetFileNameWithoutExtension(exe).Equals(process, StringComparison.OrdinalIgnoreCase)) continue;
            if (titleEnds != null) { var t = new StringBuilder(512); GetWindowText(h, t, 512); if (!t.ToString().EndsWith(titleEnds, StringComparison.Ordinal)) continue; }
            return h;
        }
        return IntPtr.Zero;
    }
    static readonly HashSet<string> SkipClass = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow", "RainmeterMeterWindow"];

    internal static List<IntPtr> OpenWindows()
    {
        var list = new List<IntPtr>();
        EnumWindows((h, _) => { if (IsAppWindow(h)) list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }

    /// <summary>A real app window, the kind Alt+Tab lists: not Windows' own helpers (snap previews, Start's host).</summary>
    internal static bool IsAppWindow(IntPtr h)
    {
        if (!IsWindowVisible(h) || GetWindow(h, 4) != IntPtr.Zero) return false;           // visible, no owner
        int ex = GetWindowLong(h, -20);
        if ((ex & 0x80) != 0 && (ex & 0x40000) == 0) return false;                          // tool windows, unless APPWINDOW
        if (DwmGetWindowAttribute(h, 14, out int cloaked, 4) == 0 && cloaked != 0) return false;
        GetWindowThreadProcessId(h, out var pid); if (pid == (uint)Environment.ProcessId) return false;
        var cls = new StringBuilder(128); GetClassName(h, cls, 128); if (SkipClass.Contains(cls.ToString())) return false;
        var t = new StringBuilder(4); return GetWindowText(h, t, 4) != 0;
    }

    void Fill()
    {
        var mon = MonitorFromPoint(new POINT { X = (int)(area.Left + area.Width / 2), Y = (int)(area.Top + area.Height / 2) }, 2);
        // this screen's windows first; then, after a small gap and dimmed, the ones on the other screen,
        // so every screen's top bar can reach every open app
        var all = OpenWindows();
        var here = all.Where(h => MonitorFromWindow(h, 2) == mon).ToList();
        order.RemoveAll(h => !all.Contains(h));
        foreach (var h in all) if (!order.Contains(h)) order.Add(h);
        var shown = order.Where(here.Contains).Concat(order.Where(h => !here.Contains(h))).ToList();
        var fg = GetForegroundWindow();
        Tock();
        if (tiles.Select(t => t.hwnd).SequenceEqual(shown) && here.Count == hereCount) { foreach (var t in tiles) Pip(t.pip, t.hwnd == fg, t.hwnd); return; }
        hereCount = here.Count;
        Hover(-1); row.Children.Clear(); tiles.Clear();
        double split = here.Count > 0 && here.Count < shown.Count ? AwayGap : 0;
        double ends = (shown.Count > 0 ? 2 * AwayGap : AwayGap) + 2 * Tile; // the clock on the left, the speaker on the right
        double total = shown.Count * Tile + Math.Max(0, shown.Count - 1) * Gap + split + ends, x = (winW - total) / 2;
        // sheet 18 v2 B (his pick, 2026-11-03): the clock tile at the left end, then a gap
        row.Children.Add(clock = MakeClock(x)); x += Tile + AwayGap;
        for (int i = 0; i < shown.Count; i++)
        {
            if (i == here.Count && i > 0) x += split;
            // the other screen's windows keep the gap but are no longer dimmed (his call, 2026-11-03: they read as faded out)
            var t = MakeTile(i, shown[i], x, fg);
            row.Children.Add(t); x += Tile + Gap;
        }
        // sheet 17 v2: the speaker tile at the right end, after a gap
        if (shown.Count > 0) x += AwayGap - Gap;
        row.Children.Add(speaker = MakeSpeaker(x));
        Tock();
    }

    // ---- the clock tile (sheet 18 v2 B): the time with AM/PM in clay under it, no icon; resting on it shows the date.
    // It is drawn only while the strip is up, so it costs nothing hidden; the strip's 1 s refresh keeps it current.
    Canvas? clock;
    TextBlock? clockTime, clockAmPm;
    Canvas MakeClock(double x)
    {
        var host = new Canvas { Width = Tile, Height = Tile + 14 };
        Canvas.SetLeft(host, x); Canvas.SetTop(host, Pad);
        clockTime = new TextBlock { FontFamily = new FontFamily("Exo 2, Segoe UI"), FontStyle = FontStyles.Italic, FontWeight = FontWeights.Bold, FontSize = 14, Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center, Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 6, ShadowDepth = 0, Opacity = .75 } };
        clockAmPm = new TextBlock { FontFamily = new FontFamily("Exo 2, Segoe UI"), FontWeight = FontWeights.Bold, FontSize = 9.5, Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xB8, 0x70)),
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0) };
        var tile = new Border
        {
            Width = Tile, Height = Tile, Background = TileBrush,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 3, Direction = 270, Opacity = .5 },
            Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { clockTime, clockAmPm } },
        };
        host.Children.Add(tile);
        var rest = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(300, settings().LabelDelayMs)) };
        rest.Tick += (_, _) =>
        {
            rest.Stop(); if (!tile.IsMouseOver) return;
            ((TextBlock)bubble.Child).Text = DateTime.Now.ToString("dddd, MMMM d");
            if (bubble.Parent == null) root.Children.Add(bubble);
            bubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(bubble, Math.Max(4, x + Tile / 2 - bubble.DesiredSize.Width / 2)); Canvas.SetTop(bubble, Pad + Tile + 10);
            bubbleTimer.Stop(); bubble.BeginAnimation(OpacityProperty, null); bubble.Opacity = 1;
        };
        tile.MouseEnter += (_, _) => { Hover(-1); tile.Background = TileHotBrush; rest.Stop(); rest.Start(); };
        tile.MouseLeave += (_, _) => { tile.Background = TileBrush; rest.Stop(); bubbleTimer.Stop(); bubbleTimer.Start(); };
        return host;
    }
    void Tock()
    {
        if (clockTime == null || clockAmPm == null) return;
        var now = DateTime.Now;
        clockTime.Text = now.ToString("h:mm"); clockAmPm.Text = now.Hour < 12 ? "AM" : "PM";
    }

    // ---- the speaker tile: click for the volume drop-down, scroll for the whole PC's volume
    Canvas? speaker;
    readonly Border bubble = new() { Padding = new Thickness(8, 2, 8, 3), CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1.5), Opacity = 0, IsHitTestVisible = false,
        Background = new LinearGradientBrush(Color.FromRgb(0x12, 0x2C, 0x42), Color.FromRgb(0x06, 0x10, 0x1A), 90), BorderBrush = new SolidColorBrush(Color.FromRgb(0xB8, 0x92, 0x4C)),
        Child = new TextBlock { FontFamily = new FontFamily("Exo 2, Segoe UI"), FontStyle = FontStyles.Italic, FontWeight = FontWeights.SemiBold, FontSize = 12.5, Foreground = Brushes.White } };
    readonly DispatcherTimer bubbleTimer = new() { Interval = TimeSpan.FromMilliseconds(1100) };
    Canvas MakeSpeaker(double x)
    {
        var host = new Canvas { Width = Tile, Height = Tile + 14 };
        Canvas.SetLeft(host, x); Canvas.SetTop(host, Pad);
        var tile = new Border { Width = Tile, Height = Tile, Background = TileBrush, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = null,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 3, Direction = 270, Opacity = .5 },
            Child = MixerView.Speaker(false, Color.FromRgb(0xC4, 0xF2, 0xFF), 24) };
        host.Children.Add(tile);
        // where the drop-down hangs (under the tile) and the tile itself, in screen DIPs
        (Point under, Rect rect) Where()
        {
            var d = VisualTreeHelper.GetDpi(this);
            var a = tile.PointToScreen(new Point(0, 0)); var b = tile.PointToScreen(new Point(Tile, Tile));
            Point Dip(Point p) => new(p.X / d.DpiScaleX, p.Y / d.DpiScaleY);
            var tl = Dip(a); var br = Dip(b);
            return (new Point((tl.X + br.X) / 2, br.Y + 6), new Rect(tl, br));
        }
        // his pick (2026-11-03): resting on the tile opens it, after the same delay as the window names
        var rest = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(300, settings().LabelDelayMs)) };
        rest.Tick += (_, _) => { rest.Stop(); if (tile.IsMouseOver) { var (u, r) = Where(); VolumePopup.Hover(u, r); } };
        tile.MouseEnter += (_, _) => { Hover(-1); tile.Background = TileHotBrush; rest.Stop(); rest.Start(); };
        tile.MouseLeave += (_, _) => { tile.Background = TileBrush; rest.Stop(); };
        tile.MouseLeftButtonUp += (_, _) => { rest.Stop(); var (u, r) = Where(); VolumePopup.Toggle(u, r); };
        tile.MouseWheel += (_, e) =>
        {
            var v = AudioMixer.NudgeMaster(e.Delta > 0 ? .02f : -.02f);
            ((TextBlock)bubble.Child).Text = $"Whole PC {Math.Round(v * 100)}%";
            if (bubble.Parent == null) root.Children.Add(bubble);
            bubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(bubble, x + Tile / 2 - bubble.DesiredSize.Width / 2); Canvas.SetTop(bubble, Pad + Tile + 10);
            bubble.Opacity = 1; bubbleTimer.Stop(); bubbleTimer.Start();
        };
        return host;
    }

    FrameworkElement MakeTile(int i, IntPtr h, double x, IntPtr fg)
    {
        var host = new Canvas { Width = Tile, Height = Tile + 14 };
        Canvas.SetLeft(host, x); Canvas.SetTop(host, Pad);
        var scale = new ScaleTransform(1, 1, Tile / 2, 0); var lift = new TranslateTransform();
        var tile = new Border
        {
            Width = Tile, Height = Tile, Background = TileBrush, Cursor = System.Windows.Input.Cursors.Hand,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 3, Direction = 270, Opacity = .5 },
            RenderTransform = new TransformGroup { Children = { scale, lift } },
        };
        var img = new Image { Width = Icon, Height = Icon, Source = IconOf(h), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        tile.Child = img;
        var pip = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Cyan), Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 8, ShadowDepth = 0 } };
        Canvas.SetTop(pip, Tile + 6); Pip(pip, h == fg, h);
        host.Children.Add(tile); host.Children.Add(pip);
        tile.MouseEnter += (_, _) => Hover(i);
        tile.MouseLeave += (_, _) => { if (hovered == i) Hover(-1); };
        tile.MouseLeftButtonUp += (_, _) => Activate(h);
        tile.MouseRightButtonUp += (_, _) => { labelTimer.Stop(); HidePreview(); SystemMenu(h); };
        tiles.Add((h, tile, scale, lift, pip));
        return host;
    }
    static readonly Color Amber = Color.FromRgb(0xFF, 0xA5, 0x3A);
    static void Pip(Border p, bool active, IntPtr h)
    {
        // sheet 22: a window that wants him has an amber pip, wide like the active one, until he goes to it
        bool wants = Attention.Wants(h);
        p.Width = active || wants ? 20 : 10; Canvas.SetLeft(p, (Tile - p.Width) / 2);
        var c = wants ? Amber : active ? Color.FromRgb(0xA8, 0xEC, 0xFF) : Cyan;
        p.Background = new SolidColorBrush(c); if (p.Effect is DropShadowEffect fx) fx.Color = wants ? Amber : Cyan;
    }

    // ---- hover: only the tile under the mouse lifts; after the label delay, its name and a live preview
    void Hover(int i)
    {
        if (i == hovered) return;
        if (hovered >= 0 && hovered < tiles.Count) { var o = tiles[hovered]; Grow(o.scale, o.lift, 1, 0); o.tile.Background = TileBrush; }
        hovered = i; labelTimer.Stop(); HidePreview();
        if (i < 0 || i >= tiles.Count) return;
        var t = tiles[i]; Grow(t.scale, t.lift, 1.15, 4); t.tile.Background = TileHotBrush;
        var d = settings().LabelDelayMs; if (d >= 0) { labelTimer.Interval = TimeSpan.FromMilliseconds(d); labelTimer.Start(); }
    }
    static void Grow(ScaleTransform s, TranslateTransform l, double to, double y)
    {
        var e = new CubicEase { EasingMode = EasingMode.EaseOut }; var d = TimeSpan.FromMilliseconds(200);
        s.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(to, d) { EasingFunction = e });
        s.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(to, d) { EasingFunction = e });
        l.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, d) { EasingFunction = e });
    }

    [DllImport("dwmapi.dll")] static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
    [DllImport("dwmapi.dll")] static extern int DwmUnregisterThumbnail(IntPtr thumb);
    [DllImport("dwmapi.dll")] static extern int DwmUpdateThumbnailProperties(IntPtr thumb, ref THUMBPROPS p);
    [DllImport("dwmapi.dll")] static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out PSIZE s);
    [StructLayout(LayoutKind.Sequential)] struct PSIZE { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct THUMBPROPS { public int flags; public RECT dest, src; public byte opacity; public int visible, sourceClientOnly; }

    // the preview's frame: the same frame as his clock, gauges and settings menu (clock.png cut in nine parts at its
    // exact pixel size), so the bamboo rail and the corner seals are the real ones and never stretch
    static Grid PreviewFrame()
    {
        var g = SettingsPanel.Frame(new Border());
        g.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 16, ShadowDepth = 5, Direction = 270, Opacity = .55 };
        return g;
    }

    // the preview and its frame fade in and out together, as one piece (Windows draws the live preview itself, so
    // its opacity is stepped along with the frame's on every frame of the fade)
    readonly DispatcherTimer fade = new() { Interval = TimeSpan.FromMilliseconds(15) };
    double fadeAt, fadeTo; DateTime fadeStart; THUMBPROPS props; bool fadeHooked;
    void FadePreview(double to, int ms)
    {
        if (!fadeHooked) { fade.Tick += (_, _) => FadeStep(); fadeHooked = true; }
        fadeAt = previewFrame.Opacity; fadeTo = to; fadeStart = DateTime.Now; fadeMs = ms; fade.Start();
    }
    int fadeMs;
    void FadeStep()
    {
        double k = Math.Min(1, (DateTime.Now - fadeStart).TotalMilliseconds / fadeMs), o = fadeAt + (fadeTo - fadeAt) * k;
        previewFrame.BeginAnimation(OpacityProperty, null); previewFrame.Opacity = o; rail?.Fade(o);
        if (thumb != IntPtr.Zero) { props.opacity = (byte)Math.Round(255 * o); props.flags = 0x4; DwmUpdateThumbnailProperties(thumb, ref props); }
        if (k < 1) return;
        fade.Stop();
        if (fadeTo == 0 && thumb != IntPtr.Zero) { DwmUnregisterThumbnail(thumb); thumb = IntPtr.Zero; }
    }

    void ShowPreview(int i)
    {
        var t = tiles[i]; var host = (UIElement)t.tile.Parent;
        double cx = Canvas.GetLeft(host) + Tile / 2;
        var title = new StringBuilder(256); GetWindowText(t.hwnd, title, 256); labelText.Text = title.ToString();
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(label, Math.Clamp(cx - label.DesiredSize.Width / 2, 8, winW - label.DesiredSize.Width - 8)); Canvas.SetTop(label, Pad + Tile + 16);
        label.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
        // a live preview of the window, drawn by Windows itself, fitted by its own shape (never squashed, never
        // enlarged past its real size) and centred in the frame with the same padding on every side
        fade.Stop(); ToFront();
        if (thumb != IntPtr.Zero) { DwmUnregisterThumbnail(thumb); thumb = IntPtr.Zero; }
        if (DwmRegisterThumbnail(me, t.hwnd, out thumb) != 0) { thumb = IntPtr.Zero; return; }
        DwmQueryThumbnailSourceSize(thumb, out var src);
        var vis = Visible(t.hwnd, src); double sw = vis.R - vis.L, sh = vis.B - vis.T;
        double sc = Math.Min(1, Math.Min(PreviewW / Math.Max(1.0, sw), PreviewH / Math.Max(1.0, sh)));
        double w = Math.Round(sw * sc), h = Math.Round(sh * sc);
        double fw = w + 2 * Rail, fh = h + 2 * Rail;
        double fx = Math.Clamp(cx - fw / 2, 12, winW - fw - 12), fy = Pad + Tile + 54; // clear of the name above
        previewFrame.Width = fw; previewFrame.Height = fh; Canvas.SetLeft(previewFrame, fx); Canvas.SetTop(previewFrame, fy);
        double px = fx + Rail, py = fy + Rail;
        var dpi = VisualTreeHelper.GetDpi(this);
        rail ??= new RailWindow(this);
        rail.Place(Left + fx, Top + fy, fw, fh);
        props = new THUMBPROPS { flags = 0x1 | 0x2 | 0x4 | 0x8 | 0x10, opacity = 0, visible = 1, sourceClientOnly = 0, src = vis,
            dest = new RECT { L = (int)Math.Round(px * dpi.DpiScaleX), T = (int)Math.Round(py * dpi.DpiScaleY), R = (int)Math.Round((px + w) * dpi.DpiScaleX), B = (int)Math.Round((py + h) * dpi.DpiScaleY) } };
        DwmUpdateThumbnailProperties(thumb, ref props);
        previewFrame.Opacity = 0; FadePreview(1, 140);
    }
    // the part of a window that shows: Windows hands it over with invisible resize borders (8 px past the screen when
    // maximized), which sat as uneven dark gaps inside the frame
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT v, int size);
    static RECT Visible(IntPtr h, PSIZE src)
    {
        var all = new RECT { L = 0, T = 0, R = src.x, B = src.y };
        if (IsIconic(h) || !GetWindowRect(h, out var wr) || DwmGetWindowAttribute(h, 9, out RECT ext, 16) != 0) return all;
        double kx = src.x / Math.Max(1.0, wr.R - wr.L), ky = src.y / Math.Max(1.0, wr.B - wr.T);
        var v = new RECT { L = (int)Math.Round((ext.L - wr.L) * kx), T = (int)Math.Round((ext.T - wr.T) * ky), R = (int)Math.Round((ext.R - wr.L) * kx), B = (int)Math.Round((ext.B - wr.T) * ky) };
        v.L = Math.Clamp(v.L, 0, src.x); v.T = Math.Clamp(v.T, 0, src.y); v.R = Math.Clamp(v.R, v.L, src.x); v.B = Math.Clamp(v.B, v.T, src.y);
        return v.R - v.L < 40 || v.B - v.T < 40 ? all : v;
    }
    void HidePreview()
    {
        label.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(100)));
        if (thumb != IntPtr.Zero || previewFrame.Opacity > 0) FadePreview(0, 110);
    }

    // ---- clicks: focus (or minimise the window already in front); right-click is the window's own menu
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void SwitchToThisWindow(IntPtr h, bool alt);
    void Activate(IntPtr h)
    {
        if (GetForegroundWindow() == h && !IsIconic(h)) ShowWindow(h, 6); else Focus(h);
        Dispatcher.BeginInvoke(Fill, DispatcherPriority.Background);
    }
    internal static void Focus(IntPtr h) { if (IsIconic(h)) ShowWindow(h, 9); if (!SetForegroundWindow(h)) SwitchToThisWindow(h, true); }
    internal static bool IsMinimized(IntPtr h) => IsIconic(h);
    internal static void MinimizeOrRestore(IntPtr h) { if (IsIconic(h)) Focus(h); else ShowWindow(h, 6); }
    internal static void CloseWindow(IntPtr h) => PostMessage(h, 0x0010 /*WM_CLOSE*/, IntPtr.Zero, IntPtr.Zero);

    // ---- the menus' shared moves (his picks, 2026-11-03: move to the other screen, close all, the app's window list)
    /// <summary>Every open window of this program (by its file), in the strip's order; titleEnds as WindowOf.</summary>
    internal static List<IntPtr> WindowsOf(string process, string? titleEnds = null) => OpenWindows().Where(h =>
    {
        GetWindowThreadProcessId(h, out var pid); var exe = ExePath(pid);
        if (exe == null || !System.IO.Path.GetFileNameWithoutExtension(exe).Equals(process, StringComparison.OrdinalIgnoreCase)) return false;
        return titleEnds == null || TitleOf(h).EndsWith(titleEnds, StringComparison.Ordinal);
    }).ToList();
    internal static string TitleOf(IntPtr h) { var t = new StringBuilder(256); GetWindowText(h, t, 256); return t.ToString(); }
    internal static bool HasOtherScreen => Screens.All().Count > 1;
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hgt, bool repaint);
    /// <summary>To the next screen, keeping its size and its place on the screen (maximized stays maximized there).</summary>
    internal static void MoveToOtherScreen(IntPtr h)
    {
        var all = Screens.All(); if (all.Count < 2 || !GetWindowRect(h, out var r)) return;
        var c = new Point((r.L + r.R) / 2.0, (r.T + r.B) / 2.0);
        int i = Math.Max(0, all.FindIndex(s => s.Area.Contains(c)));
        Rect from = all[i].Area, to = all[(i + 1) % all.Count].Area;
        bool max = IsZoomed(h), min = IsIconic(h);
        if (max || min) ShowWindow(h, 9);                        // a maximized window moves as itself, then fills the new screen
        if (!GetWindowRect(h, out r)) return;
        int w = r.R - r.L, hgt = r.B - r.T;
        int x = (int)Math.Clamp(to.Left + (r.L - from.Left) * to.Width / from.Width, to.Left, Math.Max(to.Left, to.Right - w));
        int y = (int)Math.Clamp(to.Top + (r.T - from.Top) * to.Height / from.Height, to.Top, Math.Max(to.Top, to.Bottom - hgt));
        MoveWindow(h, x, y, w, hgt, true);
        if (max) ShowWindow(h, 3);
        Focus(h);
    }
    /// <summary>The window list and moves for one app's windows, shared by the dock's and the strip's menus.</summary>
    internal static void AddWindowItems(List<MenuEntry?> items, List<IntPtr> wins)
    {
        if (wins.Count == 0) return;
        if (wins.Count > 1)
            foreach (var w in wins.Take(8)) { var t = TitleOf(w); items.Add(new(t.Length > 42 ? t[..40] + "…" : t, () => Focus(w))); }
        else items.Add(new("Bring to front", () => Focus(wins[0])));
        var front = wins[0];
        items.Add(new(IsIconic(front) ? "Restore" : "Minimize", () => MinimizeOrRestore(front)));
        if (HasOtherScreen) items.Add(new("Move to other screen", () => MoveToOtherScreen(front)));
    }
    internal static void AddCloseItems(List<MenuEntry?> items, List<IntPtr> wins)
    {
        if (wins.Count == 1) items.Add(new("Close window", () => CloseWindow(wins[0]), Warn: true));
        else if (wins.Count > 1) items.Add(new($"Close all {wins.Count} windows", () => { foreach (var w in wins) CloseWindow(w); }, Warn: true));
    }

    // a name plate in the strip's style (for pinned apps without painted labels)
    internal static Border NamePlate(string text) => new()
    {
        Padding = new Thickness(10, 4, 10, 5), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1.5),
        Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x0A, 0x1A, 0x28)), BorderBrush = new SolidColorBrush(Color.FromRgb(0xB8, 0x92, 0x4C)),
        Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 10, ShadowDepth = 0, Opacity = .45 },
        Child = new TextBlock { Text = text, FontFamily = new FontFamily("Exo 2, Segoe UI"), FontStyle = FontStyles.Italic, FontWeight = FontWeights.SemiBold, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0xC4, 0xF2, 0xFF)) },
    };
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    // right-click: the Shrine menu for this window
    void SystemMenu(IntPtr h)
    {
        GetWindowThreadProcessId(h, out var pid); var exe = ExePath(pid);
        bool shared = exe == null || exe.EndsWith("electron.exe", StringComparison.OrdinalIgnoreCase) || exe.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase);
        string name = exe == null || shared ? "" : FileVersionInfo.GetVersionInfo(exe).FileDescription ?? "";
        if (string.IsNullOrWhiteSpace(name)) { var t = new StringBuilder(256); GetWindowText(h, t, 256); name = t.ToString(); }
        // this window first; the app's others after it (for an app with several, the list and "Close all")
        var wins = new List<IntPtr> { h };
        if (exe != null && !shared) wins.AddRange(WindowsOf(System.IO.Path.GetFileNameWithoutExtension(exe)).Where(w => w != h));
        var items = new List<MenuEntry?>();
        if (wins.Count > 1) AddWindowItems(items, wins);
        else
        {
            items.Add(new("Bring to front", () => Focus(h)));
            items.Add(new(IsIconic(h) ? "Restore" : "Minimize", () => MinimizeOrRestore(h)));
            if (HasOtherScreen) items.Add(new("Move to other screen", () => MoveToOtherScreen(h)));
        }
        if (!shared) items.Add(new("New window", () => Process.Start(new ProcessStartInfo(exe!) { UseShellExecute = true })));
        items.Add(new("Close window", () => { CloseWindow(h); Dispatcher.BeginInvoke(Fill, DispatcherPriority.Background); }, Warn: true));
        if (wins.Count > 1) items.Add(new($"Close all {wins.Count} windows", () => { foreach (var w in wins) CloseWindow(w); Dispatcher.BeginInvoke(Fill, DispatcherPriority.Background); }, Warn: true));
        GetCursorPos(out var c); var d = VisualTreeHelper.GetDpi(this);
        var at = new Point(c.X / d.DpiScaleX, c.Y / d.DpiScaleY); var icon = IconOf(h);
        if (exe != null)
        {
            if (DockWindow.IsPinned(exe, out var key)) items.AddRange([null, new("Unpin from dock", () => ShrineMenu.Confirm($"Unpin {name} from the dock?", icon, "Yes, unpin", () => Unpin(exe, key), at, upward: false))]);
            else if (!shared) items.AddRange([null, new("Pin to dock", () => { var s = Settings.Load(); Settings.Save(s with { Pinned = [.. (s.Pinned ?? []), name.Replace("|", " ") + "|" + exe] }); })]);
        }
        ShrineMenu.Open(name, icon, items, at, upward: false);
    }
    static void Unpin(string exe, string? key)
    {
        var s = Settings.Load();
        Settings.Save(key != null ? s with { Hidden = [.. (s.Hidden ?? []), key] } : s with { Pinned = (s.Pinned ?? []).Where(p => !p.EndsWith("|" + exe, StringComparison.OrdinalIgnoreCase)).ToArray() });
    }

    // ---- icons: the window's own (apps like Samara and Aang set theirs), else its program's
    const int WM_GETICON = 0x7F;
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, int msg, IntPtr w, IntPtr l, int flags, int timeout, out IntPtr result);
    [DllImport("user32.dll")] static extern IntPtr GetClassLongPtr(IntPtr h, int i);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr p, int flags, StringBuilder s, ref int size);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint PrivateExtractIcons(string file, int index, int cx, int cy, IntPtr[] icons, int[]? ids, uint n, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    readonly Dictionary<IntPtr, ImageSource?> iconCache = [];

    ImageSource? IconOf(IntPtr h)
    {
        if (iconCache.TryGetValue(h, out var cached)) return cached;
        ImageSource? src = null;
        GetWindowThreadProcessId(h, out var pid);
        var exe = ExePath(pid);
        // a sharp 48 px icon from the program file first, unless it's a shared host (Electron, UWP frames)
        bool shared = exe == null || exe.EndsWith("electron.exe", StringComparison.OrdinalIgnoreCase) || exe.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase);
        if (!shared) src = FromExe(exe!);
        if (src == null)
        {
            SendMessageTimeout(h, WM_GETICON, (IntPtr)1, IntPtr.Zero, 2, 100, out var hi);
            if (hi == IntPtr.Zero) hi = GetClassLongPtr(h, -14);
            if (hi == IntPtr.Zero) SendMessageTimeout(h, WM_GETICON, (IntPtr)2, IntPtr.Zero, 2, 100, out hi);
            if (hi != IntPtr.Zero) try { src = Imaging.CreateBitmapSourceFromHIcon(hi, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); src.Freeze(); } catch { }
        }
        if (src == null && exe != null) src = FromExe(exe);
        iconCache[h] = src; return src;
    }
    internal static string? ExePath(uint pid)
    {
        var p = OpenProcess(0x1000, false, pid); if (p == IntPtr.Zero) return null;
        try { var sb = new StringBuilder(1024); int n = sb.Capacity; return QueryFullProcessImageName(p, 0, sb, ref n) ? sb.ToString() : null; } finally { CloseHandle(p); }
    }
    internal static ImageSource? FromExe(string exe)
    {
        var icons = new IntPtr[1];
        if (PrivateExtractIcons(exe, 0, 48, 48, icons, null, 1, 0) == 0 || icons[0] == IntPtr.Zero) return null;
        try { var s = Imaging.CreateBitmapSourceFromHIcon(icons[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); s.Freeze(); return s; }
        catch { return null; } finally { DestroyIcon(icons[0]); }
    }

    void SetClickThrough(bool through)
    {
        if (me == IntPtr.Zero) return;
        int ex = GetWindowLong(me, -20) | 0x80 | 0x08000000;
        SetWindowLong(me, -20, through ? ex | 0x20 : ex & ~0x20);
    }

    // ---- games, like Windows (his pick 2026-11-02): out of sight while WoW or Cemu is the window in front on this
    // screen; alt-tab out, or use the other screen, and the strip is back
    // 2026-11-04: the game on top of this screen counts even when the focus is on the other screen, so working there
    // no longer wakes this strip over the game (FullScreen.cs)
    internal static bool GameInFrontOn(Rect screen) => FullScreen.GameOn(screen);
    void CheckGame()
    {
        bool game = GameInFrontOn(area);
        if (game == asleepForGame) return;
        asleepForGame = game;
        if (game) { Sink(); Visibility = Visibility.Hidden; }
        else { Visibility = Visibility.Visible; SetClickThrough(true); if (primary) HideTaskbar(); }
    }
}

// sheet 15 A: the preview's gold rail and corner seals, in a see-through window just above the strip so they sit over
// the live picture's edges (clicks pass through; it never takes the focus)
class RailWindow : Window
{
    public RailWindow(Window owner)
    {
        Owner = owner; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; IsHitTestVisible = false; Focusable = false; Opacity = 0;
        Content = SettingsPanel.Frame(new Border(), "frame-rail.png");
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            SetWindowLong(h, -20, GetWindowLong(h, -20) | 0x20 /*TRANSPARENT*/ | 0x80 /*TOOLWINDOW*/ | 0x08000000 /*NOACTIVATE*/);
        };
    }
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    public void Place(double x, double y, double w, double h) { Left = x; Top = y; Width = w; Height = h; if (!IsVisible) Show(); }
    public void Fade(double o) { Opacity = o; if (o <= 0 && IsVisible) Hide(); }
}

