using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ShrineDock;

// Our own Start (sheet 16, 2026-11-02): Windows' Start can only open on the main screen while its taskbars are
// hidden, so the dock app has its own. It opens on the screen he's on (the Win key: where the mouse is; a dock's
// Start tile: that dock's screen), rising above that dock's Start tile. His clock frame with the Start crest, a clay
// search well, his pins, recent apps, every app A to Z behind the "All" plate, and a power seal that asks first.
// Typing searches at once; ↑↓←→ move, Enter opens, Esc goes back or closes; any click outside closes it.
class StartMenu : Window
{
    static StartMenu? open;
    static DateTime closedAt;
    static readonly string[] DefaultPins = ["Settings", "Snipping Tool", "CurseForge", "Google Chrome", "Spotify", "Terminal"];
    static readonly Color Cyan = Color.FromRgb(0x38, 0xC6, 0xFF), Ice = Color.FromRgb(0xC4, 0xF2, 0xFF), Text = Color.FromRgb(0xE8, 0xF6, 0xFF), Sub = Color.FromRgb(0x6D, 0x93, 0xAB),
        Clay = Color.FromRgb(0xB8, 0x92, 0x4C), ClayHi = Color.FromRgb(0xDD, 0xB8, 0x70), ClayLo = Color.FromRgb(0x6E, 0x52, 0x26), Well = Color.FromRgb(0x04, 0x10, 0x1A);
    static readonly FontFamily Font = new("Exo 2, Segoe UI"), Body = new("Figtree, Segoe UI"), Glyphs = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    const double W = 300, H = 600;

    /// <summary>Opens Start on this screen above x (its dock's Start tile), or closes it if it's already open there.</summary>
    public static void Toggle(Rect screen, double anchorX)
    {
        if (open != null) { var was = open.screen; open.Close(); if (was == screen) return; }
        else if ((DateTime.Now - closedAt).TotalMilliseconds < 300) return; // the click that closed it from outside
        open = new StartMenu(screen, anchorX);
        open.Closed += (s, _) => { if (open == s) open = null; closedAt = DateTime.Now; };
        open.Show(); Bring(open);
    }
    public static bool IsOpen => open != null;

    readonly Rect screen;
    readonly TextBox query = new();
    readonly ScrollViewer scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Focusable = false };
    readonly StackPanel body = new();
    readonly Border allPlate = new();
    readonly TextBlock allText = new();
    readonly List<(Border el, AppCatalog.Entry app, bool grid)> items = [];
    bool showAll, closing;
    int cur;

    StartMenu(Rect screen, double anchorX)
    {
        this.screen = screen;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; Title = "Shrine Start"; Width = W + 32; Height = H + 32 + 16; Opacity = 0;
        // Always from the bottom left corner of the screen, as Windows' Start does (his rule, 2026-11-02: it floated
        // above the dock's tile or under the mouse, mid-screen). anchorX only says which screen.
        Left = screen.Left + 12;
        Top = screen.Bottom - 12 - Height;

        // inside the frame: the search well, the list, then "All" and the power seal (the top clears the crest)
        var inner = new Grid { Margin = new Thickness(18, 32, 18, 18) };
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inner.RowDefinitions.Add(new RowDefinition());
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        // sheet 18 idea 2 (his "Yes, add it", 2026-11-03): the time first, big, under the crest; the day and Toronto's weather
        var when = When(); inner.Children.Add(when);
        var well = Search(); Grid.SetRow(well, 1); inner.Children.Add(well);
        scroll.Content = body; scroll.Margin = new Thickness(-4, 12, -4, 0); Grid.SetRow(scroll, 2); inner.Children.Add(scroll);
        var foot = Foot(); Grid.SetRow(foot, 3); inner.Children.Add(foot);

        var frame = SettingsPanel.Frame(inner); frame.Width = W; frame.Height = H;
        // the dock's Start emblem rides the top rail as a crest
        var crest = new Image { Source = Art("emblem.png"), Width = 30, Height = 30, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0),
            Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 8, ShadowDepth = 0, Opacity = .6 }, IsHitTestVisible = false };
        RenderOptions.SetBitmapScalingMode(crest, BitmapScalingMode.HighQuality);
        var lift = new TranslateTransform(0, 16);
        Content = new Grid
        {
            Margin = new Thickness(16, 16, 16, 16), RenderTransform = lift,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 22, ShadowDepth = 10, Direction = 270, Opacity = .6 },
            Children = { frame, crest },
        };

        AppCatalog.Changed += Redraw;
        Closed += (_, _) => AppCatalog.Changed -= Redraw;
        AppCatalog.Refresh(Dispatcher);
        Draw();

        PreviewKeyDown += Key;
        Closing += (_, _) => closing = true;
        Deactivated += (_, _) => { if (!closing && !MenuOpen()) Close(); }; // its own right-click and power menus keep it open
        // a click anywhere outside closes it, even if it never got focus
        var watch = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        watch.Tick += (_, _) =>
        {
            if (closing) { watch.Stop(); return; }
            bool down = (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0;
            if (down && !CursorOnMe() && !MenuOpen()) Close();
        };
        Loaded += (_, _) =>
        {
            watch.Start();
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            query.Focus(); Keyboard.Focus(query);
        };
        Closed += (_, _) => watch.Stop();
    }

    // ---- the parts
    FrameworkElement When()
    {
        var time = new TextBlock { FontFamily = Font, FontStyle = FontStyles.Italic, FontWeight = FontWeights.Bold, FontSize = 34, Foreground = Brushes.White,
            Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 10, ShadowDepth = 0, Opacity = .7 } };
        var ampm = new TextBlock { FontFamily = Font, FontWeight = FontWeights.SemiBold, FontSize = 14, Foreground = new SolidColorBrush(Ice), Margin = new Thickness(5, 0, 0, 5), VerticalAlignment = VerticalAlignment.Bottom };
        var day = new TextBlock { FontFamily = Font, FontWeight = FontWeights.SemiBold, FontSize = 12.5, Foreground = new SolidColorBrush(ClayHi), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        var wx = new TextBlock { FontFamily = Body, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x7F, 0xA8, 0xBF)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0) };
        var top = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Children = { time, ampm } };
        void Paint()
        {
            var now = DateTime.Now;
            time.Text = now.ToString("h:mm"); ampm.Text = now.Hour < 12 ? "AM" : "PM";
            day.Text = now.ToString("dddd, MMMM d").ToUpperInvariant();
            wx.Text = ClockWindow.WeatherLine is string w ? "Toronto · " + w : "Toronto";
        }
        Paint();
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        t.Tick += (_, _) => Paint(); t.Start(); Closed += (_, _) => t.Stop();
        return new StackPanel { Margin = new Thickness(0, 2, 0, 12), Children = { top, day, wx } };
    }

    FrameworkElement Search()
    {
        var glyph = new TextBlock { Text = "", FontFamily = Glyphs, FontSize = 14, Foreground = new SolidColorBrush(Cyan), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 8, 0) };
        var hint = new TextBlock { Text = "Search for apps", FontFamily = Font, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x7A, 0x92)), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        query.FontFamily = Font; query.FontSize = 14; query.Foreground = new SolidColorBrush(Ice); query.CaretBrush = new SolidColorBrush(Cyan);
        query.Background = Brushes.Transparent; query.BorderThickness = new Thickness(0); query.VerticalAlignment = VerticalAlignment.Center; query.Padding = new Thickness(0);
        query.SelectionBrush = new SolidColorBrush(Cyan);
        query.TextChanged += (_, _) => { hint.Visibility = query.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; cur = 0; Draw(); };
        var field = new Grid(); field.Children.Add(hint); field.Children.Add(query);
        var row = new DockPanel(); DockPanel.SetDock(glyph, Dock.Left); row.Children.Add(glyph); row.Children.Add(field);
        // a carved well: dark inset, clay rim
        return new Border { Height = 34, CornerRadius = new CornerRadius(17), BorderThickness = new Thickness(2), Padding = new Thickness(12, 0, 12, 0), Child = row,
            Background = new SolidColorBrush(Well), BorderBrush = new SolidColorBrush(Clay), Cursor = Cursors.IBeam };
    }

    FrameworkElement Foot()
    {
        // the "All" plate (his note 2026-11-03: "sitting badly in frame and all harsh corners"): the same clay plate, now with
        // soft corners, a dark-wood edge round a carved clay rim (lit top, shaded bottom, as the menus' rim), the seal's
        // height band so both sit on one centre line, and its left edge on the pins' left edge
        allText.FontFamily = Font; allText.FontStyle = FontStyles.Italic; allText.FontWeight = FontWeights.Bold; allText.FontSize = 13.5; allText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xEC, 0xC4));
        allText.VerticalAlignment = VerticalAlignment.Center; allText.HorizontalAlignment = HorizontalAlignment.Center; allText.Margin = new Thickness(0, 0, 2, 1); // italic leans right; nudge it to the eye's centre
        var rim = new Border
        {
            Height = 28, MinWidth = 64, Padding = new Thickness(18, 0, 18, 0), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1.5), Child = allText,
            BorderBrush = new LinearGradientBrush(new GradientStopCollection { new(ClayHi, 0), new(Clay, .5), new(ClayLo, 1) }, 90),
            Background = new LinearGradientBrush(Color.FromRgb(0x3A, 0x2A, 0x12), Color.FromRgb(0x1E, 0x15, 0x09), 90),
        };
        allPlate.Child = rim; allPlate.Padding = new Thickness(1); allPlate.CornerRadius = new CornerRadius(9.5);
        allPlate.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x12, 0x08)); allPlate.BorderThickness = new Thickness(0);
        allPlate.Cursor = Cursors.Hand; allPlate.VerticalAlignment = VerticalAlignment.Center;
        allPlate.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 2, Direction = 270, Opacity = .5 };
        allPlate.MouseEnter += (_, _) => allPlate.Effect = new DropShadowEffect { Color = ClayHi, BlurRadius = 10, ShadowDepth = 0, Opacity = .6 };
        allPlate.MouseLeave += (_, _) => allPlate.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 2, Direction = 270, Opacity = .5 };
        allPlate.MouseLeftButtonUp += (_, _) => { showAll = !showAll; query.Text = ""; cur = 0; Draw(); query.Focus(); };

        // the power seal: clay rings round a dark eye, the power glyph lit cyan
        var seal = Seal("", "Power");
        seal.MouseLeftButtonUp += (_, _) => Power(seal);
        // sheet 21 (2026-11-03): the Tasks seal beside it opens Shrine Tasks, the simple Task Manager
        var tasks = Seal("", "Tasks"); tasks.Margin = new Thickness(0, 0, 8, 0);
        // opened a moment after Start has gone: opened inside the click, Tasks inherited Start's mouse capture and its
        // keys (Esc, arrows) went nowhere (tested 2026-11-03)
        tasks.MouseLeftButtonUp += (_, _) =>
        {
            Close();
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            t.Tick += (_, _) => { t.Stop(); Mouse.Capture(null); TasksPanel.Toggle(); }; t.Start();
        };

        // one 36 px band for all three, lifted 18 px so the seals clear the frame's 32 px corner seals; the sides line up
        // with the pins' outer edges (the list runs 4 px wider than the frame's inside, each tile 3 px in)
        var foot = new DockPanel { Margin = new Thickness(-1, 10, -1, 18), Height = 36, LastChildFill = false };
        DockPanel.SetDock(seal, Dock.Right); DockPanel.SetDock(tasks, Dock.Right);
        foot.Children.Add(allPlate); foot.Children.Add(seal); foot.Children.Add(tasks);
        return foot;
    }
    static Grid Seal(string glyph, string tip)
    {
        var seal = new Grid { Width = 36, Height = 36, Cursor = Cursors.Hand, ToolTip = tip };
        seal.Children.Add(new Ellipse { Fill = new RadialGradientBrush(new GradientStopCollection {
            new(Color.FromRgb(0x0A, 0x1C, 0x2C), 0), new(Color.FromRgb(0x0A, 0x1C, 0x2C), .52), new(ClayLo, .54), new(ClayLo, .60), new(Clay, .62), new(Clay, .78), new(ClayHi, .80), new(ClayHi, .86), new(Color.FromRgb(0x3E, 0x2C, 0x16), .88), new(Color.FromRgb(0x3E, 0x2C, 0x16), 1) }),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 2, Direction = 270, Opacity = .5 } });
        seal.Children.Add(new TextBlock { Text = glyph, FontFamily = Glyphs, FontSize = 14, Foreground = new SolidColorBrush(Ice), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 6, ShadowDepth = 0 } });
        seal.MouseEnter += (_, _) => seal.Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 12, ShadowDepth = 0, Opacity = .6 };
        seal.MouseLeave += (_, _) => seal.Effect = null;
        return seal;
    }

    // ---- what the list shows: home (pins + recent), a search, or every app
    void Redraw() { if (!closing) Draw(); }
    void Draw()
    {
        body.Children.Clear(); items.Clear();
        var s = Settings.Load(); var all = AppCatalog.All; var q = query.Text.Trim();
        allText.Text = showAll && q.Length == 0 ? "‹ Back" : "All";
        if (q.Length > 0)
        {
            var hits = all.Where(a => a.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 0 : a.Name.Split(' ').Any(w => w.StartsWith(q, StringComparison.OrdinalIgnoreCase)) ? 1 : 2)
                .ThenBy(a => a.Name.Length).Take(9).ToList();
            if (hits.Count == 0) body.Children.Add(new TextBlock { Text = all.Count == 0 ? "Reading your apps…" : $"Nothing called “{q}”", FontFamily = Font, FontStyle = FontStyles.Italic, FontSize = 13, Foreground = new SolidColorBrush(Sub), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 30, 0, 0) });
            else { body.Children.Add(Heading("Apps")); foreach (var a in hits) body.Children.Add(Row(a, null)); }
        }
        else if (showAll)
        {
            body.Children.Add(Heading("All apps"));
            string last = "";
            foreach (var a in all)
            {
                var c = char.IsLetter(a.Name[0]) ? char.ToUpperInvariant(a.Name[0]).ToString() : "#";
                if (c != last) { body.Children.Add(Letter(c)); last = c; }
                body.Children.Add(Row(a, null));
            }
        }
        else
        {
            var grid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 4, 0, 0) };
            foreach (var name in s.StartPins ?? DefaultPins) { var a = AppCatalog.Find(name); if (a != null) grid.Children.Add(Tile(a)); }
            body.Children.Add(grid);
            var recent = (s.StartRecent ?? []).Select(AppCatalog.Find).Where(a => a != null).Take(3).ToList();
            if (recent.Count > 0) { body.Children.Add(Heading("Recent")); foreach (var a in recent) body.Children.Add(Row(a!, "Opened lately")); }
        }
        cur = Math.Clamp(cur, 0, Math.Max(0, items.Count - 1));
        Paint();
    }

    Border Tile(AppCatalog.Entry a)
    {
        var st = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        st.Children.Add(new Image { Source = AppCatalog.IconOf(a.Id), Width = 34, Height = 34, Margin = new Thickness(0, 0, 0, 5) });
        st.Children.Add(new TextBlock { Text = a.Name, FontFamily = Font, FontSize = 12.5, Foreground = new SolidColorBrush(Text), TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 120 });
        var b = new Border { Child = st, Padding = new Thickness(0, 10, 0, 8), Margin = new Thickness(3, 2, 3, 2), CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(2), Cursor = Cursors.Hand };
        Wire(b, a, true); return b;
    }
    Border Row(AppCatalog.Entry a, string? sub)
    {
        var arrow = new Polygon { Points = { new(0, 0), new(8, 5), new(0, 10) }, Fill = new SolidColorBrush(Cyan), Width = 8, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = 0,
            Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 6, ShadowDepth = 0 } };
        var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(new TextBlock { Text = a.Name, FontFamily = Font, FontSize = 13.5, Foreground = new SolidColorBrush(Text), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 190 });
        if (sub != null) words.Children.Add(new TextBlock { Text = sub, FontFamily = Body, FontSize = 11, Foreground = new SolidColorBrush(Sub) });
        var st = new StackPanel { Orientation = Orientation.Horizontal, Children = { arrow, new Image { Source = AppCatalog.IconOf(a.Id), Width = 26, Height = 26, Margin = new Thickness(0, 0, 10, 0) }, words } };
        var b = new Border { Child = st, Padding = new Thickness(6, 5, 8, 5), Margin = new Thickness(0, 1, 0, 1), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(2), Cursor = Cursors.Hand, Tag = arrow };
        Wire(b, a, false); return b;
    }
    void Wire(Border b, AppCatalog.Entry a, bool grid)
    {
        int i = items.Count; items.Add((b, a, grid));
        b.Background = Brushes.Transparent; b.BorderBrush = Brushes.Transparent;
        b.MouseEnter += (_, _) => { cur = i; Paint(); };
        b.MouseLeftButtonUp += (_, _) => Open(a);
        b.MouseRightButtonUp += (_, e) => { e.Handled = true; AppMenu(a); };
    }
    static FrameworkElement Heading(string t)
    {
        var line = new Rectangle { Height = 2, VerticalAlignment = VerticalAlignment.Center, Fill = new LinearGradientBrush(Clay, Color.FromArgb(0, 0xB8, 0x92, 0x4C), 0) };
        var lead = new Rectangle { Width = 14, Height = 2, VerticalAlignment = VerticalAlignment.Center, Fill = new LinearGradientBrush(Color.FromArgb(0, 0xB8, 0x92, 0x4C), Clay, 0) };
        var text = new TextBlock { Text = t.ToUpperInvariant(), FontFamily = Font, FontStyle = FontStyles.Italic, FontWeight = FontWeights.SemiBold, FontSize = 11.5, Foreground = new SolidColorBrush(ClayHi), Margin = new Thickness(8, 0, 8, 1) };
        var d = new DockPanel { Margin = new Thickness(4, 12, 4, 6) };
        DockPanel.SetDock(lead, Dock.Left); DockPanel.SetDock(text, Dock.Left);
        d.Children.Add(lead); d.Children.Add(text); d.Children.Add(line);
        return d;
    }
    static FrameworkElement Letter(string c) => new Border { BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x40)), Margin = new Thickness(6, 10, 6, 2), Padding = new Thickness(0, 0, 0, 2),
        Child = new TextBlock { Text = c, FontFamily = Font, FontStyle = FontStyles.Italic, FontWeight = FontWeights.Bold, FontSize = 13, Foreground = new SolidColorBrush(ClayHi) } };

    // the one under the cursor (mouse or keys): cyan glow, and the arrow on list rows
    void Paint()
    {
        for (int i = 0; i < items.Count; i++)
        {
            var (b, _, _) = items[i]; bool on = i == cur;
            b.Background = on ? new SolidColorBrush(Color.FromArgb(0x24, 0x38, 0xC6, 0xFF)) : Brushes.Transparent;
            b.BorderBrush = on ? new SolidColorBrush(Color.FromArgb(0xCC, 0x38, 0xC6, 0xFF)) : Brushes.Transparent;
            b.Effect = on ? new DropShadowEffect { Color = Cyan, BlurRadius = 10, ShadowDepth = 0, Opacity = .35 } : null;
            if (b.Tag is UIElement arrow) arrow.Opacity = on ? 1 : 0;
            if (on) b.BringIntoView();
        }
    }

    // ---- keys
    void Key(object? _, KeyEventArgs e)
    {
        bool grid = items.Count > 0 && items[cur].grid;
        switch (e.Key)
        {
            case System.Windows.Input.Key.Escape:
                if (query.Text.Length > 0) query.Text = "";
                else if (showAll) { showAll = false; cur = 0; Draw(); }
                else Close();
                break;
            case System.Windows.Input.Key.Enter: if (items.Count > 0) Open(items[cur].app); break;
            case System.Windows.Input.Key.Down: Move(grid ? (cur + 2 < items.Count && items[cur + 2].grid ? 2 : NextRow()) : 1); break;
            case System.Windows.Input.Key.Up: Move(grid ? -2 : -1); break;
            case System.Windows.Input.Key.Right when grid && query.Text.Length == 0: Move(cur % 2 == 0 && cur + 1 < items.Count && items[cur + 1].grid ? 1 : 0); break;
            case System.Windows.Input.Key.Left when grid && query.Text.Length == 0: Move(cur % 2 == 1 ? -1 : 0); break;
            default: return;
        }
        e.Handled = true;
    }
    int NextRow() { int first = items.FindIndex(x => !x.grid); return first < 0 ? 0 : first - cur; }
    void Move(int by) { cur = Math.Clamp(cur + by, 0, Math.Max(0, items.Count - 1)); Paint(); }

    // ---- doing things
    void Open(AppCatalog.Entry a)
    {
        AppCatalog.Launch(a);
        var s = Settings.Load();
        Settings.Save(s with { StartRecent = [a.Name, .. (s.StartRecent ?? []).Where(n => n != a.Name).Take(5)] });
        Close();
    }
    void AppMenu(AppCatalog.Entry a)
    {
        var st = Settings.Load();
        var pins = (st.StartPins ?? DefaultPins).ToList(); bool pinned = pins.Contains(a.Name);
        // his ask (2026-11-03): pin an app from Start to the dock. Saved as "Name|shell:AppsFolder\id|icon.png": an app id
        // has no program file for the dock to read an icon from, so its Start icon is kept beside the dock
        var launch = @"shell:AppsFolder\" + a.Id;
        bool onDock = (st.Pinned ?? []).Any(p => p.Split('|') is var b && b.Length >= 2 && b[1].Equals(launch, StringComparison.OrdinalIgnoreCase));
        GetCursorPos(out var c); var at = new Point(c.X, c.Y); var icon = AppCatalog.IconOf(a.Id);
        void Refresh() { if (open != null) { open.Draw(); Bring(open); } }
        ShrineMenu.Open(a.Name, icon, [
            new("Open", () => Open(a)),
            null,
            new(pinned ? "Unpin from Start" : "Pin to Start", () =>
            {
                var s = Settings.Load(); var p = (s.StartPins ?? DefaultPins).ToList();
                if (pinned) p.Remove(a.Name); else p.Add(a.Name);
                Settings.Save(s with { StartPins = [.. p] });
                Refresh();
            }),
            onDock
                ? new("Unpin from dock", () => ShrineMenu.Confirm($"Unpin {a.Name} from the dock?", icon, "Yes, unpin", () =>
                    { var s = Settings.Load(); Settings.Save(s with { Pinned = (s.Pinned ?? []).Where(p => !(p.Split('|') is var b && b.Length >= 2 && b[1].Equals(launch, StringComparison.OrdinalIgnoreCase))).ToArray() }); }, at, upward: false))
                : new("Pin to dock", () =>
                {
                    var png = SaveIcon(a, icon);
                    var s = Settings.Load(); var name = a.Name.Replace("|", " ");
                    Settings.Save(s with { Pinned = [.. (s.Pinned ?? []), png != null ? $"{name}|{launch}|{png}" : $"{name}|{launch}"] });
                    Refresh();
                }),
        ], at, upward: false);
    }
    static string? SaveIcon(AppCatalog.Entry a, ImageSource? icon)
    {
        if (icon is not BitmapSource bmp) return null;
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Settings.Path)!, "pins"); System.IO.Directory.CreateDirectory(dir);
            var file = System.IO.Path.Combine(dir, string.Concat(a.Name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')) + ".png");
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp));
            using var fs = System.IO.File.Create(file); enc.Save(fs);
            return file;
        }
        catch (Exception e) { Program.Log("start: pin icon " + e.Message); return null; }
    }
    void Power(FrameworkElement seal)
    {
        var p = seal.PointToScreen(new Point(18, 0));
        void Ask(string what, string yes, Action act) => ShrineMenu.Open($"{what} now?", null, [new(yes, act, Warn: true), new("Cancel", () => { })], p, upward: true);
        ShrineMenu.Open("Power", null, [
            new("Sleep", () => { Close(); SetSuspendState(false, false, false); }),
            new("Sign out", () => Ask("Sign out", "Yes, sign out", () => Run("/l"))),
            null,
            new("Restart", () => Ask("Restart", "Yes, restart", () => Run("/r /t 0")), Warn: true),
            new("Shut down", () => Ask("Shut down", "Yes, shut down", () => Run("/s /t 0")), Warn: true),
        ], p, upward: true);
        static void Run(string args) { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("shutdown.exe", args) { CreateNoWindow = true, UseShellExecute = false }); } catch (Exception ex) { Program.Log("start: power: " + ex.Message); } }
    }

    // ---- plumbing
    static BitmapImage Art(string file) { var b = new BitmapImage(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "art", file))); b.Freeze(); return b; }
    bool CursorOnMe() { GetCursorPos(out var c); return c.X >= Left && c.X <= Left + ActualWidth && c.Y >= Top && c.Y <= Top + ActualHeight; }
    static bool MenuOpen() => Application.Current.Windows.OfType<ShrineMenu>().Any();
    // a background app may not take the foreground; a tap of Alt (sent by us) lets it, the usual way round that
    static void Bring(Window w)
    {
        var h = new System.Windows.Interop.WindowInteropHelper(w).Handle;
        keybd_event(0x12, 0, 0, WinKey.Mine); SetForegroundWindow(h); keybd_event(0x12, 0, 2, WinKey.Mine);
        w.Activate();
    }
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    struct POINT { public int X, Y; }
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("powrprof.dll")] static extern bool SetSuspendState(bool hibernate, bool force, bool wakeupEventsDisabled);
}
