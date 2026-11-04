using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ShrineDock;

// The Shrine settings panel (sheet 10, approved: "the new settings menu is great"): a game menu inside his own
// clock frame. Six pages (Garden, Clock, Dock, Sound, Screens, Widgets), one row per setting on one grid, and only
// four kinds of control: the ‹ › picker, the seal switch, the notched slider and the carved button. The Sound page
// (sheet 17 v2) also holds the mixer: MixerView's rows, with its smooth slider and mute seal.
// Every change applies at once: the garden through Lively's setprop, the widgets through Rainmeter bangs and
// ClockSettings/PCSettings.inc, the dock, strip and lantern clock through settings.json.
// Keys: ↑↓ move, ←→ change, Tab next page; Esc or any click outside closes it.
class SettingsPanel : Window
{
    static SettingsPanel? open;
    static DateTime closedByOutsideClick;
    static int lastPage;
    public static void Toggle()
    {
        if (open != null) { open.Close(); return; }
        // the click on the seal that closed it from outside shouldn't reopen it straight away
        if ((DateTime.Now - closedByOutsideClick).TotalMilliseconds < 500) return;
        open = new SettingsPanel(); open.Closed += (_, _) => open = null; FullScreen.GiveBackOnClose(open); open.Show(); open.Activate(); open.Focus();
    }
    /// <summary>Opens the menu on one page ("All sound settings…" in the strip's drop-down).</summary>
    public static void OpenOn(string name)
    {
        open?.Close();
        lastPage = Math.Max(0, Array.IndexOf(["Garden", "Clock", "Dock", "Sound", "Screens", "Widgets"], name));
        closedByOutsideClick = DateTime.MinValue; Toggle();
    }

    const string Res = @"C:\Users\Shadow\Documents\Rainmeter\Skins\Shrine\@Resources\";
    const string Rm = @"C:\Program Files\Rainmeter\Rainmeter.exe";
    static readonly string Lively = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Lively Wallpaper\Lively.exe");
    static readonly string Saved = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Lively Wallpaper\Library\SaveData\wpdata\shrinegarden\1\LivelyProperties.json");

    static readonly Color Cyan = Color.FromRgb(0x38, 0xC6, 0xFF), Ice = Color.FromRgb(0xC4, 0xF2, 0xFF), Dim = Color.FromRgb(0x7F, 0xA8, 0xBF), Hint = Color.FromRgb(0x5F, 0x86, 0xA0),
        Clay = Color.FromRgb(0xB8, 0x92, 0x4C), ClayHi = Color.FromRgb(0xDD, 0xB8, 0x70), ClayLo = Color.FromRgb(0x6E, 0x52, 0x26), Well = Color.FromRgb(0x06, 0x12, 0x1C), WellEdge = Color.FromRgb(0x10, 0x20, 0x2A);
    static readonly FontFamily Font = new("Exo 2, Segoe UI"), Body = new("Figtree, Segoe UI");
    const double PanelW = 452, Slice = 32, ControlW = 196;
    static readonly double[] DockSizes = [.85, 1, 1.15, 1.3];

    // one row: what it shows and what its control does
    abstract record Item(string Label, string? Note);
    record Pick(string Label, string[] Options, Func<int> Get, Action<int> Set, string? Note = null) : Item(Label, Note);
    record Switch(string Label, Func<bool> Get, Action<bool> Set, string? Note = null) : Item(Label, Note);
    record Slide(string Label, string[] Stops, Func<int> Get, Action<int> Set, string? Note = null) : Item(Label, Note);
    record Act(string Label, string Button, Action Do, string? Note = null) : Item(Label, Note);
    record Mixer() : Item("", null);                   // the mixer's rows (MixerView), as many as are playing
    record Section(string Label) : Item(Label, null);  // a small clay heading, not a row

    readonly List<(string name, Item[] items)> pages;
    readonly StackPanel tabs = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
    readonly StackPanel rows = new();
    readonly List<(Border row, Action<int> change, Action redraw)> live = [];
    readonly Dictionary<int, Action> mutes = [];       // M on a mixer row
    MixerView? mixer;
    int page = lastPage, cur;
    JsonNode? garden;

    SettingsPanel()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; Title = "Shrine settings"; SizeToContent = SizeToContent.WidthAndHeight; Focusable = true;
        try { garden = JsonNode.Parse(File.ReadAllText(Saved)); } catch { }
        pages = Pages();

        // the frame: his clock frame, nine-sliced at 1:1 so the bamboo rail and the seals stay crisp pixel art
        var inner = new StackPanel { Margin = new Thickness(Slice - 14, Slice - 14, Slice - 12, Slice - 6), Width = PanelW - 2 * Slice + 26 };
        var title = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10), Background = Brushes.Transparent };
        title.Children.Add(Rule(false));
        title.Children.Add(new TextBlock { Text = "Shrine", FontFamily = Font, FontStyle = FontStyles.Italic, FontWeight = FontWeights.Bold, FontSize = 24, Foreground = new SolidColorBrush(Ice), Margin = new Thickness(10, 0, 10, 0),
            Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 8, ShadowDepth = 0, Opacity = .7 } });
        title.Children.Add(Rule(true));
        title.MouseLeftButtonDown += (_, _) => DragMove();
        inner.Children.Add(title); inner.Children.Add(tabs); inner.Children.Add(rows);
        var foot = new DockPanel { Margin = new Thickness(16, 12, 16, 2) };
        var line = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(0x1C, 0x34, 0x46)), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 8, 0, 0), Child = foot };
        var right = Foot("Esc", " or click outside to close"); DockPanel.SetDock(right, Dock.Right);
        foot.Children.Add(right); foot.Children.Add(Foot("↑↓", " move · ", "←→", " change · ", "Tab", " page"));
        inner.Children.Add(line);
        var grid = SoftFrame(inner);
        Content = new Border { Child = grid, Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 22, ShadowDepth = 10, Direction = 270, Opacity = .6 }, Margin = new Thickness(16) };
        DrawTabs(); DrawPage();

        // his rule for every popup: Esc or a click anywhere outside closes it (the panel may not get focus when it
        // opens from the seal, so it also watches the mouse and Esc itself)
        bool closing = false;
        Closing += (_, _) => { closing = true; lastPage = page; mixer?.Stop(); };
        PreviewKeyDown += (_, e) => Key(e);
        Deactivated += (_, _) => { if (!closing) Close(); };
        var watch = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        watch.Tick += (_, _) =>
        {
            if (closing) { watch.Stop(); return; }
            if ((GetAsyncKeyState(0x1B) & 0x8000) != 0) { Close(); return; }
            bool down = (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0;
            if (down && !CursorOnPane()) { closedByOutsideClick = DateTime.Now; Close(); }
        };
        Loaded += (_, _) =>
        {
            watch.Start();
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - ActualWidth - 8; Top = Math.Max(wa.Top + 12, (wa.Height - ActualHeight) / 2);
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
        };
        Closed += (_, _) => watch.Stop();
    }

    // ---- the six pages, every row wired to the real setting
    List<(string, Item[])> Pages()
    {
        var clock = ReadInc("ClockSettings.inc");
        var pc = ReadInc("PCSettings.inc");
        int[] sizes = [300, 350, 400, 480], sinks = [400, 900, 1500, 3000], names = [-1, 300, 600, 1000];
        string[] warm = ["50", "60", "70", "80"], hot = ["80", "85", "90", "95"];
        return
        [
            ("Garden", [
                // on: sheet 13's living garden (sun-timed light, lamps, glowing pond, rainy window); off: the classic garden
                new Switch("Living garden", () => G("living", true), v => Garden("living", v)),
                new Pick("Scene", ["Follow Toronto", "Dawn", "Day", "Dusk", "Night"], () => G("follow", true) ? 0 : G("scene", 1) + 1, i =>
                {
                    if (i == 0) Garden("follow", true); else { Garden("follow", false); Garden("scene", i - 1); }
                }, "Real time and weather"),
                new Switch("Rain", () => G("rain", true), v => Garden("rain", v)),
                new Pick("Fireflies", ["Few", "Some", "Many"], () => G("fireflyAmount", 2) - 1, i => Garden("fireflyAmount", i + 1)),
                new Switch("Ripples on click", () => G("ripples", true), v => Garden("ripples", v)),
                new Pick("Motion", ["Full", "Calm", "Still"], () => G("motion", 0), i => Garden("motion", i)),
            ]),
            ("Clock", [
                new Pick("Face", ["Night Lantern", "Digital"], () => clock.GetValueOrDefault("IsAnalog") == "1" ? 0 : 1, i =>
                {
                    bool analog = i == 0;
                    clock["IsAnalog"] = analog ? "1" : "0"; clock["ClockStyle"] = analog ? "analog" : "digital";
                    WriteInc("ClockSettings.inc", clock); Bang("!Refresh", "Shrine\\Clock"); SwapFace(analog);
                }),
                new Slide("Size", ["Small", "Medium", "Large", "XL"], () => Math.Max(0, Array.IndexOf(sizes, Settings.Load().ClockSize)), i =>
                {
                    // grow or shrink around the clock's centre, keeping its top; the gauges and seal follow below it
                    var c = Settings.Load(); int n = sizes[i];
                    WidgetKeeper.Current?.Expect();
                    Save(c with { ClockSize = n, ClockX = c.ClockX + (c.ClockSize - n) / 2 });
                }, "The Night Lantern"),
                new Pick("Second hand", ["Sweep", "Off"], () => clock.GetValueOrDefault("SecondHand") == "0" ? 1 : 0, i =>
                {
                    clock["SecondHand"] = i == 0 ? "1" : "0"; WriteInc("ClockSettings.inc", clock); Bang("!Refresh", "Shrine\\Clock");
                }),
            ]),
            ("Dock", [
                // sheet 23 (his pick B, 2026-11-04): also behind Start's extra seal; the dock rises to show each step
                new Slide("Size", ["Small", "Normal", "Large", "Larger"], () => Math.Max(0, Array.IndexOf(DockSizes, Settings.Load().DockScale)), i => Save(Settings.Load() with { DockScale = DockSizes[i] })),
                new Switch("Hide until the edge", () => Settings.Load().HideUntilHover, v => Save(Settings.Load() with { HideUntilHover = v }), "Dock and top bar"),
                new Slide("Sinks after", ["0.4 s", "0.9 s", "1.5 s", "3 s"], () => Math.Max(0, Array.IndexOf(sinks, Settings.Load().HideDelayMs)), i => Save(Settings.Load() with { HideDelayMs = sinks[i] })),
                new Pick("Names appear", ["Never", "Quickly", "After a pause", "Slowly"], () => Math.Max(0, Array.IndexOf(names, Settings.Load().LabelDelayMs)), i => Save(Settings.Load() with { LabelDelayMs = names[i] })),
            ]),
            ("Sound", [
                new Section("Volume"),
                new Mixer(),
                new Switch("Remember each app's level", () => !Settings.Load().ForgetLevels, v => Save(Settings.Load() with { ForgetLevels = !v }), "Put back when the app plays again"),
                new Section("Sounds"),
                new Pick("Welcome chime", ["Normal", "Quiet", "Off"], () => File.Exists(Res + @"Sounds\chime-off") ? 2 : File.Exists(Res + @"Sounds\chime-quiet") ? 1 : 0, i =>
                {
                    Flag("chime-quiet", i == 1); Flag("chime-off", i == 2);
                }),
                new Act("Welcome", "Play it now", () => _ = Arrival.Run("settings: play it now", force: true)),
            ]),
            ("Screens", [
                new Switch("Keep the Acer on the left", () => !Settings.Load().FreeScreen2, v => { Save(Settings.Load() with { FreeScreen2 = !v }); if (v) Screens.KeepAcerLeft(); }, "If Shadow moves screen 2"),
            ]),
            ("Widgets", [
                new Switch("Lock in place", () => Settings.Load().LockWidgets, v =>
                {
                    Save(Settings.Load() with { LockWidgets = v });
                    foreach (var c in new[] { "Shrine\\Clock", "Shrine\\PC", "Shrine\\Seal" }) Bang("!Draggable", v ? "0" : "1", c);
                }, "Clock, gauges, seal"),
                new Pick("Gauges warm at", ["50%", "60%", "70%", "80%"], () => Math.Max(0, Array.IndexOf(warm, pc.GetValueOrDefault("WarnAt", "60"))), i =>
                {
                    pc["WarnAt"] = warm[i]; WriteInc("PCSettings.inc", pc); Bang("!Refresh", "Shrine\\PC");
                }),
                new Pick("Gauges hot at", ["80%", "85%", "90%", "95%"], () => Math.Max(0, Array.IndexOf(hot, pc.GetValueOrDefault("HotAt", "85"))), i =>
                {
                    pc["HotAt"] = hot[i]; WriteInc("PCSettings.inc", pc); Bang("!Refresh", "Shrine\\PC");
                }),
                new Act("Positions", "Reset to default", () =>
                {
                    Save(Settings.Load() with { LockWidgets = false });
                    foreach (var c in new[] { "Shrine\\Clock", "Shrine\\PC", "Shrine\\Seal" }) Bang("!Draggable", "1", c);
                    PlaceWidgets(clock.GetValueOrDefault("IsAnalog") == "1");
                    DrawPage();
                }),
            ]),
        ];
    }

    // ---- drawing
    void DrawTabs()
    {
        tabs.Children.Clear();
        double w = (PanelW - 2 * Slice + 26 - 5 * 4) / 6;
        for (int i = 0; i < pages.Count; i++)
        {
            int k = i; bool on = i == page;
            var t = new Border
            {
                Width = w, Margin = new Thickness(i == 0 ? 0 : 4, 0, 0, 0), Padding = new Thickness(0, 7, 0, 6), Cursor = Cursors.Hand, BorderThickness = new Thickness(2),
                Background = on ? new LinearGradientBrush(Color.FromRgb(0x2A, 0x20, 0x10), Color.FromRgb(0x1A, 0x14, 0x0A), 90) : new SolidColorBrush(Color.FromRgb(0x07, 0x14, 0x20)),
                BorderBrush = new SolidColorBrush(on ? Clay : WellEdge),
                Effect = on ? new DropShadowEffect { Color = ClayHi, BlurRadius = 10, ShadowDepth = 0, Opacity = .3 } : null,
                Child = new TextBlock { Text = pages[i].name.ToUpperInvariant(), FontFamily = Font, FontWeight = FontWeights.SemiBold, FontSize = 11.5, TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(on ? Color.FromRgb(0xFF, 0xEC, 0xCC) : Dim) },
            };
            t.MouseLeftButtonUp += (_, _) => { page = k; cur = 0; DrawTabs(); DrawPage(); };
            tabs.Children.Add(t);
        }
    }

    void DrawPage()
    {
        rows.Children.Clear(); live.Clear(); mutes.Clear();
        var items = pages[page].items;
        bool hasMixer = items.Any(i => i is Mixer);
        if (!hasMixer && mixer != null) { mixer.Stop(); mixer = null; }
        foreach (var item in items)
        {
            switch (item)
            {
                case Section s:
                    if (rows.Children.Count > 0) rows.Children.Add(new Rectangle { Height = 1, Margin = new Thickness(10, 8, 10, 4), Fill = new LinearGradientBrush(new GradientStopCollection { new(Colors.Transparent, 0), new(Color.FromArgb(0xC0, 0xDD, 0xB8, 0x70), .5), new(Colors.Transparent, 1) }, 0) });
                    rows.Children.Add(new TextBlock { Text = s.Label.ToUpperInvariant(), FontFamily = Font, FontWeight = FontWeights.SemiBold, FontSize = 11, Foreground = new SolidColorBrush(ClayHi), Margin = new Thickness(26, 4, 10, 2) });
                    break;
                case Mixer:
                    if (mixer == null) { mixer = new MixerView(compact: false); mixer.Changed += () => DrawPage(); mixer.Start(); }
                    foreach (var r in mixer.Rows)
                    {
                        // unhook from the last page's frame before it goes into this one
                        if (r.Element.Parent is Panel old) old.Children.Remove(r.Element);
                        var row = Framed(live.Count, r.Element, 8);
                        int k = live.Count; live.Add((row, r.Nudge, () => { })); mutes[k] = r.ToggleMute;
                        rows.Children.Add(row);
                    }
                    if (mixer.IsQuiet) { if (mixer.Nothing.Parent is Panel p) p.Children.Remove(mixer.Nothing); rows.Children.Add(mixer.Nothing); }
                    break;
                default:
                    rows.Children.Add(Row(live.Count, item));
                    break;
            }
        }
        Cursor(Math.Min(cur, live.Count - 1));
    }

    // a row's frame: the game cursor arrow and the stripe (shared by the setting rows and the mixer's rows)
    Border Framed(int i, Grid g, double right = 10)
    {
        var arrow = new Polygon { Points = new PointCollection { new(0, 0), new(9, 6), new(0, 12) }, Fill = new SolidColorBrush(Cyan), Opacity = 0, IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(-18, 0, 0, 0),
            Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 6, ShadowDepth = 0 }, RenderTransform = new TranslateTransform() };
        ((TranslateTransform)arrow.RenderTransform).BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, 3, TimeSpan.FromMilliseconds(500)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        var host = new Grid(); host.Children.Add(g); host.Children.Add(arrow);
        var row = new Border { Padding = new Thickness(26, 0, right, 0), Child = host, Tag = arrow, Background = Brushes.Transparent };
        row.MouseEnter += (_, _) => Cursor(i);
        return row;
    }

    Border Row(int i, Item item)
    {
        var g = new Grid { MinHeight = 42 };
        g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ControlW) });
        var label = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock { Text = item.Label, FontFamily = Font, FontWeight = FontWeights.Medium, FontSize = 14.5, Foreground = new SolidColorBrush(Ice) });
        if (item.Note != null) label.Children.Add(new TextBlock { Text = item.Note, FontFamily = Body, FontSize = 11.5, Foreground = new SolidColorBrush(Hint), Margin = new Thickness(0, -1, 0, 0) });
        g.Children.Add(label);
        var (ctrl, change, redraw) = Control(item);
        Grid.SetColumn(ctrl, 1); ctrl.HorizontalAlignment = HorizontalAlignment.Right; ctrl.VerticalAlignment = VerticalAlignment.Center;
        g.Children.Add(ctrl);
        // the game cursor: a cyan arrow that nudges, on the row you're on
        var arrow = new Polygon { Points = new PointCollection { new(0, 0), new(9, 6), new(0, 12) }, Fill = new SolidColorBrush(Cyan), Opacity = 0, IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(-18, 0, 0, 0),
            Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 6, ShadowDepth = 0 }, RenderTransform = new TranslateTransform() };
        ((TranslateTransform)arrow.RenderTransform).BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, 3, TimeSpan.FromMilliseconds(500)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        g.Children.Add(arrow);
        var row = new Border { Padding = new Thickness(26, 5, 10, 5), Child = g, Cursor = Cursors.Hand, Tag = arrow,
            Background = i % 2 == 1 ? new SolidColorBrush(Color.FromArgb(5, 255, 255, 255)) : Brushes.Transparent };
        row.MouseEnter += (_, _) => Cursor(i);
        live.Add((row, change, redraw));
        return row;
    }

    void Cursor(int i)
    {
        cur = i;
        for (int k = 0; k < live.Count; k++)
        {
            var on = k == i; var r = live[k].row;
            r.Background = on ? new LinearGradientBrush(Color.FromArgb(0x33, 0x38, 0xC6, 0xFF), Color.FromArgb(0x0A, 0x38, 0xC6, 0xFF), 0)
                : k % 2 == 1 ? new SolidColorBrush(Color.FromArgb(5, 255, 255, 255)) : Brushes.Transparent;
            ((UIElement)r.Tag).Opacity = on ? 1 : 0;
        }
    }

    // the four kinds of control; each returns the element, how ←/→ (or a click) changes it, and how it redraws
    (FrameworkElement, Action<int>, Action) Control(Item item)
    {
        switch (item)
        {
            case Pick p:
            {
                int v = Math.Clamp(p.Get(), 0, p.Options.Length - 1);
                var g = new Grid { Width = ControlW };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
                var left = Arrow("‹"); var right = Arrow("›"); Grid.SetColumn(right, 2);
                var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(stack, 1);
                var val = new TextBlock { FontFamily = Font, FontStyle = FontStyles.Italic, FontWeight = FontWeights.SemiBold, FontSize = 14, Foreground = Brushes.White, TextAlignment = TextAlignment.Center,
                    Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 6, ShadowDepth = 0, Opacity = .6 } };
                var dots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
                stack.Children.Add(val); stack.Children.Add(dots);
                g.Children.Add(left); g.Children.Add(stack); g.Children.Add(right);
                void Redraw()
                {
                    val.Text = p.Options[v]; dots.Children.Clear();
                    for (int j = 0; j < p.Options.Length; j++)
                        dots.Children.Add(new Rectangle { Width = 5, Height = 5, Margin = new Thickness(2, 0, 2, 0), Fill = new SolidColorBrush(j == v ? Cyan : Color.FromRgb(0x1D, 0x3A, 0x50)),
                            Effect = j == v ? new DropShadowEffect { Color = Cyan, BlurRadius = 4, ShadowDepth = 0 } : null });
                }
                void Change(int d) { v = (v + d + p.Options.Length) % p.Options.Length; Redraw(); p.Set(v); }
                left.MouseLeftButtonUp += (_, e) => { Change(-1); e.Handled = true; };
                right.MouseLeftButtonUp += (_, e) => { Change(1); e.Handled = true; };
                g.Background = Brushes.Transparent; g.MouseLeftButtonUp += (_, e) => { if (!e.Handled) Change(1); };
                Redraw();
                return (g, Change, Redraw);
            }
            case Switch s:
            {
                bool v = s.Get();
                var word = new TextBlock { FontFamily = Font, FontStyle = FontStyles.Italic, FontWeight = FontWeights.SemiBold, FontSize = 12.5, Width = 26, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
                var well = new Border { Width = 52, Height = 22, BorderThickness = new Thickness(2), Background = new SolidColorBrush(Well) };
                var knob = new Ellipse { Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(2, 0, 0, 0), RenderTransform = new TranslateTransform() };
                well.Child = knob;
                var wrap = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent, Children = { word, well } };
                void Redraw()
                {
                    word.Text = v ? "On" : "Off"; word.Foreground = new SolidColorBrush(v ? Ice : Dim);
                    well.BorderBrush = new SolidColorBrush(v ? Clay : WellEdge);
                    knob.Fill = v ? Seal() : new RadialGradientBrush(new GradientStopCollection { new(Color.FromRgb(0x6E, 0x8A, 0x9C), 0), new(Color.FromRgb(0x6E, 0x8A, 0x9C), .3), new(Color.FromRgb(0x3B, 0x4C, 0x58), .32), new(Color.FromRgb(0x3B, 0x4C, 0x58), .6), new(Color.FromRgb(0x20, 0x30, 0x3A), .62) });
                    knob.Effect = v ? new DropShadowEffect { Color = Cyan, BlurRadius = 8, ShadowDepth = 0, Opacity = .8 } : null;
                    ((TranslateTransform)knob.RenderTransform).BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(v ? 28 : 0, TimeSpan.FromMilliseconds(140)));
                }
                void Change(int d) { v = !v; Redraw(); s.Set(v); }
                wrap.MouseLeftButtonUp += (_, _) => Change(1);
                Redraw();
                return (wrap, Change, Redraw);
            }
            case Slide sl:
            {
                int v = Math.Clamp(sl.Get(), 0, sl.Stops.Length - 1);
                var g = new Grid { Width = ControlW };
                g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
                var track = new Grid { Height = 24, Background = Brushes.Transparent, Cursor = Cursors.Hand };
                var well = new Border { Height = 10, BorderThickness = new Thickness(2), BorderBrush = new SolidColorBrush(WellEdge), Background = new SolidColorBrush(Well), VerticalAlignment = VerticalAlignment.Center };
                var canvas = new Canvas { IsHitTestVisible = false };
                track.Children.Add(well); track.Children.Add(canvas);
                var val = new TextBlock { FontFamily = Font, FontStyle = FontStyles.Italic, FontWeight = FontWeights.SemiBold, FontSize = 13, Foreground = Brushes.White, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(val, 1); g.Children.Add(track); g.Children.Add(val);
                void Redraw()
                {
                    canvas.Children.Clear(); double w = track.ActualWidth > 0 ? track.ActualWidth : ControlW - 66, p = (double)v / (sl.Stops.Length - 1);
                    for (int j = 0; j < sl.Stops.Length; j++) { var n = new Rectangle { Width = 2, Height = 4, Fill = new SolidColorBrush(ClayLo) }; Canvas.SetLeft(n, j * w / (sl.Stops.Length - 1) - 1); Canvas.SetTop(n, 0); canvas.Children.Add(n); }
                    var fill = new Rectangle { Width = Math.Max(0, p * (w - 4)), Height = 6, Fill = new LinearGradientBrush(Color.FromRgb(0x15, 0x5A, 0x80), Cyan, 0) }; Canvas.SetLeft(fill, 2); Canvas.SetTop(fill, 9); canvas.Children.Add(fill);
                    var knob = new Ellipse { Width = 18, Height = 18, Fill = Seal(), Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 6, ShadowDepth = 0, Opacity = .7 } };
                    Canvas.SetLeft(knob, p * w - 9); Canvas.SetTop(knob, 3); canvas.Children.Add(knob);
                    val.Text = sl.Stops[v];
                }
                void Change(int d) { int n = Math.Clamp(v + d, 0, sl.Stops.Length - 1); if (n == v) return; v = n; Redraw(); sl.Set(v); }
                void At(MouseEventArgs e) { var x = e.GetPosition(track).X; int n = (int)Math.Round(Math.Clamp(x / track.ActualWidth, 0, 1) * (sl.Stops.Length - 1)); if (n != v) { v = n; Redraw(); sl.Set(v); } }
                track.MouseLeftButtonDown += (_, e) => { track.CaptureMouse(); At(e); };
                track.MouseMove += (_, e) => { if (track.IsMouseCaptured) At(e); };
                track.MouseLeftButtonUp += (_, _) => track.ReleaseMouseCapture();
                track.SizeChanged += (_, _) => Redraw();
                Redraw();
                return (g, Change, Redraw);
            }
            case Act a:
            {
                var b = new Border { Padding = new Thickness(14, 6, 14, 6), BorderThickness = new Thickness(2), BorderBrush = new SolidColorBrush(Clay), Cursor = Cursors.Hand,
                    Background = new LinearGradientBrush(Color.FromRgb(0x3A, 0x2A, 0x12), Color.FromRgb(0x22, 0x18, 0x0A), 90),
                    Child = new TextBlock { Text = a.Button, FontFamily = Font, FontWeight = FontWeights.SemiBold, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xEC, 0xC4)) } };
                b.MouseEnter += (_, _) => b.Effect = new DropShadowEffect { Color = ClayHi, BlurRadius = 10, ShadowDepth = 0, Opacity = .45 };
                b.MouseLeave += (_, _) => b.Effect = null;
                b.MouseLeftButtonUp += (_, _) => a.Do();
                return (b, d => a.Do(), () => { });
            }
        }
        throw new ArgumentException(item.ToString());
    }

    static TextBlock Arrow(string s)
    {
        var t = new TextBlock { Text = s, FontFamily = Font, FontWeight = FontWeights.Bold, FontSize = 16, Foreground = new SolidColorBrush(ClayHi), TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, Background = Brushes.Transparent };
        t.MouseEnter += (_, _) => { t.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xF4, 0xDC)); t.Effect = new DropShadowEffect { Color = ClayHi, BlurRadius = 6, ShadowDepth = 0 }; };
        t.MouseLeave += (_, _) => { t.Foreground = new SolidColorBrush(ClayHi); t.Effect = null; };
        return t;
    }
    // the seal knob: cyan eye, clay ring, dark rim (the frames' seal medallion in miniature)
    internal static RadialGradientBrush Seal() => new(new GradientStopCollection
    {
        new(Ice, 0), new(Ice, .22), new(Cyan, .26), new(Cyan, .40), new(ClayHi, .44), new(ClayHi, .70), new(ClayLo, .72), new(ClayLo, 1),
    });
    static FrameworkElement Rule(bool flip) => new Rectangle { Width = 44, Height = 2, VerticalAlignment = VerticalAlignment.Center,
        Fill = new LinearGradientBrush(flip ? ClayHi : Colors.Transparent, flip ? Colors.Transparent : ClayHi, 0) };
    static TextBlock Foot(params string[] parts)
    {
        var t = new TextBlock { FontFamily = Body, FontSize = 11.5, Foreground = new SolidColorBrush(Hint) };
        for (int i = 0; i < parts.Length; i++) t.Inlines.Add(new System.Windows.Documents.Run(parts[i]) { Foreground = i % 2 == 0 ? new SolidColorBrush(ClayHi) : new SolidColorBrush(Hint), FontWeight = i % 2 == 0 ? FontWeights.SemiBold : FontWeights.Normal });
        return t;
    }

    // Soft edges (his asks 2026-11-04): his frame, drawn round and smooth. frame-soft.png is made at 2x by a script
    // (rounded corners whose curve runs through the seals, the braided dark-faded rail of his clock and PC widget,
    // smooth seals): no shiny flat bronze, no pixel steps. Nine-sliced like Frame, shown at half size, smooth.
    internal static Grid SoftFrame(UIElement content)
    {
        var src = new BitmapImage(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "art", "frame-soft.png")));
        const int Z = 2, s = 40;   // the art's scale, and the corner slice in DIPs (the curve and seal fit inside it)
        int W = src.PixelWidth, H = src.PixelHeight, p = s * Z;
        ImageBrush Part(int x, int y, int w, int h, bool tile)
        {
            var b = new ImageBrush(new CroppedBitmap(src, new Int32Rect(x, y, w, h)));
            if (tile) { b.TileMode = TileMode.Tile; b.ViewportUnits = BrushMappingMode.Absolute; b.Viewport = new Rect(0, 0, w / (double)Z, h / (double)Z); b.Stretch = Stretch.Fill; }
            else b.Stretch = Stretch.Fill;
            RenderOptions.SetBitmapScalingMode(b, BitmapScalingMode.HighQuality);
            return b;
        }
        var g = new Grid();
        RenderOptions.SetBitmapScalingMode(g, BitmapScalingMode.HighQuality);
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(s) }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(s) });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(s) }); g.RowDefinitions.Add(new RowDefinition()); g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(s) });
        void Put(int c, int r, Brush b) { var x = new Border { Background = b }; Grid.SetColumn(x, c); Grid.SetRow(x, r); g.Children.Add(x); }
        Put(1, 1, Part(p, p, W - 2 * p, H - 2 * p, false));
        Put(1, 0, Part(p, 0, W - 2 * p, p, true)); Put(1, 2, Part(p, H - p, W - 2 * p, p, true));
        Put(0, 1, Part(0, p, p, H - 2 * p, true)); Put(2, 1, Part(W - p, p, p, H - 2 * p, true));
        Put(0, 0, Part(0, 0, p, p, false)); Put(2, 0, Part(W - p, 0, p, p, false));
        Put(0, 2, Part(0, H - p, p, p, false)); Put(2, 2, Part(W - p, H - p, p, p, false));
        var host = new Border { Child = content }; Grid.SetColumnSpan(host, 3); Grid.SetRowSpan(host, 3); g.Children.Add(host);
        return g;
    }

    // his clock frame (clock.png), nine-sliced: corners and seals 1:1, the rail repeated along the edges, the slate inside
    internal static Grid Frame(UIElement content, string art = "frame.png")
    {
        var src = new BitmapImage(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "art", art)));
        int W = src.PixelWidth, H = src.PixelHeight, s = (int)Slice;
        ImageBrush Part(int x, int y, int w, int h, bool tile, bool tileX)
        {
            var b = new ImageBrush(new CroppedBitmap(src, new Int32Rect(x, y, w, h)));
            if (tile) { b.TileMode = TileMode.Tile; b.ViewportUnits = BrushMappingMode.Absolute; b.Viewport = new Rect(0, 0, w, h); b.Stretch = Stretch.None; }
            else b.Stretch = Stretch.Fill;
            RenderOptions.SetBitmapScalingMode(b, BitmapScalingMode.NearestNeighbor);
            return b;
        }
        var g = new Grid { SnapsToDevicePixels = true, UseLayoutRounding = true };
        RenderOptions.SetBitmapScalingMode(g, BitmapScalingMode.NearestNeighbor);
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(s) }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(s) });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(s) }); g.RowDefinitions.Add(new RowDefinition()); g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(s) });
        void Put(int c, int r, Brush b) { var x = new Border { Background = b }; Grid.SetColumn(x, c); Grid.SetRow(x, r); g.Children.Add(x); }
        Put(1, 1, Part(s, s, W - 2 * s, H - 2 * s, false, false));
        Put(1, 0, Part(s, 0, W - 2 * s, s, true, true)); Put(1, 2, Part(s, H - s, W - 2 * s, s, true, true));
        Put(0, 1, Part(0, s, s, H - 2 * s, true, false)); Put(2, 1, Part(W - s, s, s, H - 2 * s, true, false));
        Put(0, 0, Part(0, 0, s, s, false, false)); Put(2, 0, Part(W - s, 0, s, s, false, false));
        Put(0, 2, Part(0, H - s, s, s, false, false)); Put(2, 2, Part(W - s, H - s, s, s, false, false));
        var host = new Border { Child = content }; Grid.SetColumnSpan(host, 3); Grid.SetRowSpan(host, 3); g.Children.Add(host);
        return g;
    }

    // ---- keys: ↑↓ move, ←→ change, Tab page, Esc close
    void Key(KeyEventArgs e)
    {
        int n = live.Count;
        switch (e.Key)
        {
            case System.Windows.Input.Key.Escape: Close(); break;
            case System.Windows.Input.Key.Down: Cursor((cur + 1) % n); break;
            case System.Windows.Input.Key.Up: Cursor((cur - 1 + n) % n); break;
            case System.Windows.Input.Key.M when mutes.ContainsKey(cur): mutes[cur](); break;
            case System.Windows.Input.Key.Right: live[cur].change(1); break;
            case System.Windows.Input.Key.Left: live[cur].change(-1); break;
            case System.Windows.Input.Key.Tab:
                page = (page + ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? -1 : 1) + pages.Count) % pages.Count; cur = 0; DrawTabs(); DrawPage(); break;
            default: return;
        }
        e.Handled = true;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    struct POINT { public int X, Y; }
    // where the cursor is, against the frame itself (the window has a see-through margin for the shadow)
    bool CursorOnPane()
    {
        GetCursorPos(out var p); var d = VisualTreeHelper.GetDpi(this);
        double x = p.X / d.DpiScaleX, y = p.Y / d.DpiScaleY;
        return x >= Left + 16 && x <= Left + ActualWidth - 16 && y >= Top + 16 && y <= Top + ActualHeight - 16;
    }

    // ---- where the clock and gauges sit by default: top right of screen 1, the gauges right under the clock
    static void PlaceWidgets(bool analog)
    {
        var sw = (int)SystemParameters.PrimaryScreenWidth;
        int pcW = 416, top = 60, right = 24; // top clears the strip
        int pcX = sw - right - pcW;
        int size = Settings.Load().ClockSize;
        int clockW = analog ? size : 360, clockH = analog ? size : 196;
        int clockX = pcX + (pcW - clockW) / 2;
        WidgetKeeper.Current?.Forget(); // these are the new homes; don't put the old ones back
        Bang("!Move", clockX.ToString(), top.ToString(), "Shrine\\Clock");
        if (analog) Save(Settings.Load() with { ClockX = clockX, ClockY = top }); // the lantern clock lives in this app
        Bang("!Move", pcX.ToString(), (top + clockH + 12).ToString(), "Shrine\\PC");
        Bang("!Move", (pcX + pcW - 50).ToString(), (top + clockH + 12 + 202 + 8).ToString(), "Shrine\\Seal");
    }

    // a new clock face takes the old one's place (where he put it), and the gauges and seal restack under it
    static void SwapFace(bool analog)
    {
        WidgetKeeper.Current?.Expect();
        var c = Settings.Load();
        int w = analog ? c.ClockSize : 360, h = analog ? c.ClockSize : 196;
        if (!analog) Bang("!Move", c.ClockX.ToString(), c.ClockY.ToString(), "Shrine\\Clock");
        int pcX = c.ClockX + (w - 416) / 2, pcY = c.ClockY + h + 12;
        Bang("!Move", pcX.ToString(), pcY.ToString(), "Shrine\\PC");
        Bang("!Move", (pcX + 416 - 50).ToString(), (pcY + 202 + 8).ToString(), "Shrine\\Seal");
    }

    // ---- writing things
    int G(string key, int d) { try { var v = garden?[key]?["value"]; return v == null ? d : v.GetValueKind() == JsonValueKind.True ? 1 : v.GetValueKind() == JsonValueKind.False ? 0 : (int)v.GetValue<double>(); } catch { return d; } }
    bool G(string key, bool d) { try { return garden?[key]?["value"]?.GetValue<bool>() ?? d; } catch { return d; } }
    void Garden(string key, object value)
    {
        var v = value is bool b ? (b ? "true" : "false") : value.ToString()!;
        foreach (var m in new[] { "1", "2" }) Run(Lively, $"setprop --monitor {m} --property \"{key}={v}\"");
        // keep our own copy in step, so the picker shows the truth when the page redraws
        try { if (garden?[key] is JsonObject o) o["value"] = value is bool bb ? JsonValue.Create(bb) : JsonValue.Create(Convert.ToDouble(value)); } catch { }
    }
    static void Bang(params string[] parts) => Run(Rm, string.Join(" ", parts.Select(p => p.Contains(' ') || p.Contains('\\') ? $"\"{p}\"" : p)));
    static void Run(string exe, string args)
    {
        try { Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true }); } catch (Exception e) { Program.Log($"run {exe}: {e.Message}"); }
    }
    static void Flag(string name, bool on)
    {
        var p = Res + @"Sounds\" + name;
        try { if (on) File.WriteAllText(p, ""); else File.Delete(p); } catch { }
    }
    static void Save(Settings s) => Settings.Save(s);
    static Dictionary<string, string> ReadInc(string file)
    {
        var d = new Dictionary<string, string>();
        try { foreach (var l in File.ReadAllLines(Res + file)) { var i = l.IndexOf('='); if (i > 0) d[l[..i].Trim()] = l[(i + 1)..].Trim(); } } catch { }
        return d;
    }
    static void WriteInc(string file, Dictionary<string, string> d)
    {
        var sb = new StringBuilder("[Variables]\r\n");
        foreach (var (k, v) in d) sb.Append(k).Append('=').Append(v).Append("\r\n");
        File.WriteAllText(Res + file, sb.ToString(), Encoding.Unicode);
    }
}
