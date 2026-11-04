using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ShrineDock;

// Shrine Tasks (sheet 21, his "Yes, build it", 2026-11-03): a simple Task Manager with the same kill switch. One row per
// app (a process and the helpers it started), open apps first, then the ones running without a window, heaviest first;
// Windows' own parts are hidden, and Shadow and the dock itself are locked (ending Shadow cuts him off the PC). End asks
// once by turning amber, then ends the whole app at once, as Task Manager's End task does; one started as admin asks for
// admin. Opened from the Tasks seal in our Start or Ctrl+Shift+Esc, at the bottom left of the screen he's on.
class TasksPanel : Window
{
    static TasksPanel? open;
    static DateTime closedAt;
    static readonly Color Cyan = Color.FromRgb(0x38, 0xC6, 0xFF), Ice = Color.FromRgb(0xC4, 0xF2, 0xFF), Text = Color.FromRgb(0xE8, 0xF6, 0xFF), Sub = Color.FromRgb(0x6D, 0x93, 0xAB),
        Clay = Color.FromRgb(0xB8, 0x92, 0x4C), ClayHi = Color.FromRgb(0xDD, 0xB8, 0x70), ClayLo = Color.FromRgb(0x6E, 0x52, 0x26), Well = Color.FromRgb(0x04, 0x10, 0x1A),
        Amber = Color.FromRgb(0xEE, 0xA0, 0x40), Pale = Color.FromRgb(0xFF, 0xD9, 0xA0), Dim = Color.FromRgb(0x4F, 0x73, 0x89);
    static readonly FontFamily Font = new("Exo 2, Segoe UI"), Body = new("Figtree, Segoe UI");
    const double W = 470, Heavy = 1500; // MB

    /// <summary>Opens Tasks on the screen the mouse is on, or closes it if it's open.</summary>
    public static void Toggle()
    {
        if (open != null) { Program.Log("tasks: closed (asked again)"); open.Close(); return; }
        if ((DateTime.Now - closedAt).TotalMilliseconds < 300) return; // the click that closed it from outside
        GetCursorPos(out var c);
        var screen = Screens.All().FirstOrDefault(s => s.Area.Contains(new Point(c.X, c.Y)))?.Area ?? SystemParameters.WorkArea;
        open = new TasksPanel(screen);
        open.Closed += (s, _) => { if (open == s) open = null; closedAt = DateTime.Now; };
        open.Show(); Bring(open);
        Program.Log($"tasks: opened on {screen}");
    }

    readonly Rect screen;
    readonly StackPanel openList = new(), bgList = new(), keepList = new();
    readonly TextBlock nOpen = new(), nBg = new(), nKeep = new(), cpuText = new(), memText = new();
    readonly Rectangle cpuBar = new(), memBar = new();
    readonly ScrollViewer scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Focusable = false };
    readonly Dictionary<string, Row> rows = [];
    readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(2) };
    readonly Tasks model = new();
    bool closing, busy;
    int cur = -1;

    TasksPanel(Rect screen)
    {
        this.screen = screen;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; Title = "Shrine Tasks"; Width = W + 32; Opacity = 0;
        Height = Math.Min(720, screen.Height - 60);
        Left = screen.Left + 12; Top = screen.Bottom - 12 - Height; // where Start opens

        var inner = new Grid { Margin = new Thickness(18, 18, 18, 18) };
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inner.RowDefinitions.Add(new RowDefinition());
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inner.Children.Add(TitleBar());
        var g = Gauges(); Grid.SetRow(g, 1); inner.Children.Add(g);

        var body = new StackPanel();
        body.Children.Add(Heading("Open apps", nOpen, null)); body.Children.Add(Columns()); body.Children.Add(openList);
        body.Children.Add(Heading("In the background", nBg, null)); body.Children.Add(bgList);
        keepList.Visibility = Visibility.Collapsed;
        body.Children.Add(Heading("Keeps your PC running", nKeep, keepList)); body.Children.Add(keepList);
        scroll.Content = body; scroll.Margin = new Thickness(-4, 4, -4, 0); Grid.SetRow(scroll, 2); inner.Children.Add(scroll);
        var foot = Foot(); Grid.SetRow(foot, 3); inner.Children.Add(foot);

        var frame = SettingsPanel.Frame(inner);
        var lift = new TranslateTransform(0, 16);
        Content = new Grid
        {
            Margin = new Thickness(16), RenderTransform = lift, Children = { frame },
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 22, ShadowDepth = 10, Direction = 270, Opacity = .6 },
        };

        PreviewKeyDown += Key;
        // opened from Start's seal, Start was the window with the keys a moment ago; take them every time it comes in
        Focusable = true; Activated += (_, _) => Keyboard.Focus(this);
        Closing += (_, _) => { closing = true; tick.Stop(); Program.Log("tasks: closing"); };
        Deactivated += (_, _) => { if (!closing && !MenuOpen()) { Program.Log("tasks: another window came in front"); Close(); } };
        var watch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        watch.Tick += (_, _) =>
        {
            if (closing) { watch.Stop(); return; }
            bool down = (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0;
            if (down && !CursorOnMe() && !MenuOpen()) { Program.Log("tasks: a click outside"); Close(); }
        };
        tick.Tick += (_, _) => Refresh();
        Loaded += (_, _) =>
        {
            watch.Start(); tick.Start(); Refresh();
            lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            Keyboard.Focus(this);
        };
        Closed += (_, _) => watch.Stop();
    }

    // ---- the parts
    FrameworkElement TitleBar()
    {
        Rectangle Line(bool flip) => new() { Width = 52, Height = 2, VerticalAlignment = VerticalAlignment.Center,
            Fill = new LinearGradientBrush(flip ? ClayHi : Color.FromArgb(0, 0xDD, 0xB8, 0x70), flip ? Color.FromArgb(0, 0xDD, 0xB8, 0x70) : ClayHi, 0) };
        var t = new TextBlock { Text = "TASKS", FontFamily = Font, FontStyle = FontStyles.Italic, FontWeight = FontWeights.Bold, FontSize = 22, Foreground = new SolidColorBrush(Ice), Margin = new Thickness(10, 0, 10, 2),
            Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 10, ShadowDepth = 0, Opacity = .7 } };
        return new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10), Children = { Line(false), t, Line(true) } };
    }

    FrameworkElement Gauges()
    {
        FrameworkElement One(string name, TextBlock value, Rectangle bar)
        {
            var label = new TextBlock { Text = name.ToUpperInvariant(), FontFamily = Font, FontWeight = FontWeights.SemiBold, FontSize = 11, Foreground = new SolidColorBrush(ClayHi) };
            value.FontFamily = Font; value.FontWeight = FontWeights.SemiBold; value.FontSize = 12; value.Foreground = new SolidColorBrush(Ice);
            var top = new DockPanel(); DockPanel.SetDock(value, Dock.Right); top.Children.Add(value); top.Children.Add(label);
            bar.HorizontalAlignment = HorizontalAlignment.Left; bar.RadiusX = bar.RadiusY = 3; bar.Width = 0;
            var well = new Border { Height = 9, Margin = new Thickness(0, 5, 0, 0), CornerRadius = new CornerRadius(4.5), BorderThickness = new Thickness(1.5), Padding = new Thickness(0.5),
                Background = new SolidColorBrush(Well), BorderBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x20, 0x2A)), Child = bar };
            return new StackPanel { Children = { top, well } };
        }
        var g = new Grid { Margin = new Thickness(6, 0, 6, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); g.ColumnDefinitions.Add(new ColumnDefinition());
        var a = One("CPU", cpuText, cpuBar); var b = One("Memory", memText, memBar); Grid.SetColumn(b, 2);
        g.Children.Add(a); g.Children.Add(b);
        return g;
    }
    void Fill(Rectangle bar, double frac, bool hot)
    {
        var w = Math.Max(0, (bar.Parent as Border)?.ActualWidth - 4 ?? 0) * Math.Clamp(frac, 0, 1);
        bar.Fill = hot ? new LinearGradientBrush(Color.FromRgb(0x8A, 0x5A, 0x1C), Amber, 0) : new LinearGradientBrush(Color.FromRgb(0x1E, 0x6E, 0x80), Cyan, 0);
        bar.Effect = new DropShadowEffect { Color = hot ? Amber : Cyan, BlurRadius = 6, ShadowDepth = 0, Opacity = .6 };
        bar.BeginAnimation(WidthProperty, new DoubleAnimation(w, TimeSpan.FromMilliseconds(900)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    FrameworkElement Heading(string t, TextBlock count, StackPanel? folds)
    {
        var lead = new Rectangle { Width = 14, Height = 2, VerticalAlignment = VerticalAlignment.Center, Fill = new LinearGradientBrush(Color.FromArgb(0, 0xB8, 0x92, 0x4C), Clay, 0) };
        var text = new TextBlock { Text = t.ToUpperInvariant(), FontFamily = Font, FontStyle = FontStyles.Italic, FontWeight = FontWeights.SemiBold, FontSize = 11.5, Foreground = new SolidColorBrush(ClayHi), Margin = new Thickness(8, 0, 8, 1), VerticalAlignment = VerticalAlignment.Center };
        var line = new Rectangle { Height = 2, VerticalAlignment = VerticalAlignment.Center, Fill = new LinearGradientBrush(Clay, Color.FromArgb(0, 0xB8, 0x92, 0x4C), 0) };
        count.FontFamily = Font; count.FontSize = 11; count.Foreground = new SolidColorBrush(Sub); count.Margin = new Thickness(8, 0, 0, 1); count.VerticalAlignment = VerticalAlignment.Center;
        var d = new DockPanel { Margin = new Thickness(4, 14, 4, 4), Background = Brushes.Transparent };
        DockPanel.SetDock(lead, Dock.Left); d.Children.Add(lead);
        if (folds != null)
        {
            // folded away, like sheet 20's "Always running": a clay arrow that turns
            var turn = new RotateTransform(-90);
            var arrow = new Polygon { Points = { new(0, 0), new(9, 0), new(4.5, 6) }, Fill = new SolidColorBrush(ClayHi), Width = 9, Height = 6, Margin = new Thickness(8, 1, -2, 0), VerticalAlignment = VerticalAlignment.Center, RenderTransform = turn, RenderTransformOrigin = new Point(.5, .5) };
            DockPanel.SetDock(arrow, Dock.Left); d.Children.Add(arrow);
            d.Cursor = Cursors.Hand;
            d.MouseLeftButtonUp += (_, _) =>
            {
                bool show = folds.Visibility != Visibility.Visible;
                folds.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                turn.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(show ? 0 : -90, TimeSpan.FromMilliseconds(220)));
                if (show) Dispatcher.BeginInvoke(() => folds.BringIntoView(), DispatcherPriority.Background);
            };
        }
        DockPanel.SetDock(text, Dock.Left); d.Children.Add(text);
        DockPanel.SetDock(count, Dock.Right); d.Children.Add(count);
        d.Children.Add(line);
        return d;
    }

    static Grid RowGrid()
    {
        var g = new Grid();
        foreach (var w in new[] { 26.0, -1, 62, 42, 66 }) g.ColumnDefinitions.Add(new ColumnDefinition { Width = w < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(w) });
        return g;
    }
    static FrameworkElement Columns()
    {
        var g = RowGrid(); g.Margin = new Thickness(10, 0, 8, 0);
        void Put(string t, int c) { var x = new TextBlock { Text = t, FontFamily = Font, FontSize = 10.5, Foreground = new SolidColorBrush(Dim), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 0) }; Grid.SetColumn(x, c); g.Children.Add(x); }
        Put("memory", 2); Put("cpu", 3);
        return g;
    }

    FrameworkElement Foot()
    {
        var note = new TextBlock { Text = "Windows' own parts are hidden", FontFamily = Body, FontSize = 11.5, Foreground = new SolidColorBrush(Dim), VerticalAlignment = VerticalAlignment.Center };
        var link = new TextBlock { Text = "Windows' Task Manager ›", FontFamily = Font, FontSize = 12.5, Foreground = new SolidColorBrush(Ice), VerticalAlignment = VerticalAlignment.Center };
        var b = new Border { Child = link, Padding = new Thickness(8, 4, 8, 4), CornerRadius = new CornerRadius(7), Background = Brushes.Transparent, Cursor = Cursors.Hand };
        b.MouseEnter += (_, _) => { b.Background = new SolidColorBrush(Color.FromArgb(0x38, 0x38, 0xC6, 0xFF)); link.Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 8, ShadowDepth = 0 }; };
        b.MouseLeave += (_, _) => { b.Background = Brushes.Transparent; link.Effect = null; };
        b.MouseLeftButtonUp += (_, _) => { Close(); WindowsTaskManager(); };
        var d = new DockPanel { Margin = new Thickness(4, 12, 0, 18), LastChildFill = false };
        DockPanel.SetDock(b, Dock.Right); d.Children.Add(note); d.Children.Add(b);
        return d;
    }
    /// <summary>Windows' own Task Manager (Ctrl+Shift+Esc, sent past our hook).</summary>
    public static void WindowsTaskManager()
    {
        try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); } catch (Exception e) { Program.Log("tasks: taskmgr " + e.Message); }
    }

    // ---- the rows: made once per app and kept, so a row under the mouse keeps its amber "End?" through a refresh
    sealed class Row
    {
        public required Tasks.App App;
        public required Border El;
        public required TextBlock Name, Line, Mem, Cpu;
        public Border? End; public TextBlock? EndText;
        public bool Sure, Gone;
    }

    Row MakeRow(Tasks.App a)
    {
        var g = RowGrid();
        var icon = new Image { Source = a.Icon, Width = 24, Height = 24, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        g.Children.Add(icon);
        var name = new TextBlock { FontFamily = Font, FontSize = 13.5, Foreground = new SolidColorBrush(Text), TextTrimming = TextTrimming.CharacterEllipsis };
        var line = new TextBlock { FontFamily = Body, FontSize = 11, Foreground = new SolidColorBrush(Sub), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, -1, 0, 0) };
        var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 6, 0), Children = { name, line } };
        Grid.SetColumn(words, 1); g.Children.Add(words);
        TextBlock Num(int c) { var t = new TextBlock { FontFamily = Font, FontWeight = FontWeights.SemiBold, FontSize = 12, Foreground = new SolidColorBrush(Ice), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(t, c); g.Children.Add(t); return t; }
        var mem = Num(2); var cpu = Num(3);
        var el = new Border { Child = g, Height = 42, Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(0, 1, 0, 1), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(2),
            Background = Brushes.Transparent, BorderBrush = Brushes.Transparent };
        var r = new Row { App = a, El = el, Name = name, Line = line, Mem = mem, Cpu = cpu };
        if (a.Locked)
        {
            var lk = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 13, Foreground = new SolidColorBrush(Dim), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 13, 0), ToolTip = "Shrine keeps this one" };
            Grid.SetColumn(lk, 4); g.Children.Add(lk);
        }
        else
        {
            var et = new TextBlock { Text = "End", FontFamily = Font, FontWeight = FontWeights.SemiBold, FontSize = 11.5, Foreground = new SolidColorBrush(Pale), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 1) };
            var end = new Border { Child = et, Width = 56, Height = 24, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Right, Cursor = Cursors.Hand };
            Grid.SetColumn(end, 4); g.Children.Add(end);
            r.End = end; r.EndText = et; PaintEnd(r, false);
            end.MouseEnter += (_, _) => PaintEnd(r, true);
            end.MouseLeave += (_, _) => PaintEnd(r, false);
            end.MouseLeftButtonUp += (_, e) => { e.Handled = true; Press(r); };
        }
        el.MouseEnter += (_, _) => { cur = Order().IndexOf(r); Paint(); };
        el.MouseLeave += (_, _) => { if (r.Sure) { r.Sure = false; PaintEnd(r, false); } };
        el.MouseLeftButtonUp += (_, _) => { if (a.Windows.Count > 0) { Close(); Tasks.Front(a); } };
        el.MouseRightButtonUp += (_, e) => { e.Handled = true; Menu(r); };
        Update(r, a);
        return r;
    }

    void PaintEnd(Row r, bool hot)
    {
        if (r.End == null) return;
        if (r.Sure)
        {
            r.End.Background = new RadialGradientBrush(Color.FromRgb(0x4A, 0x2A, 0x0E), Color.FromRgb(0x24, 0x14, 0x0A)) { Center = new Point(.5, .3), GradientOrigin = new Point(.5, .3) };
            r.End.BorderBrush = new SolidColorBrush(Amber); r.EndText!.Text = "End?"; r.EndText.Foreground = Brushes.White;
            r.End.Effect = new DropShadowEffect { Color = Amber, BlurRadius = 10, ShadowDepth = 0, Opacity = .6 };
        }
        else
        {
            r.End.Background = new RadialGradientBrush(Color.FromRgb(0x17, 0x32, 0x47), Color.FromRgb(0x0B, 0x1A, 0x26)) { Center = new Point(.5, .3), GradientOrigin = new Point(.5, .3) };
            r.End.BorderBrush = new SolidColorBrush(Clay); r.EndText!.Text = "End"; r.EndText.Foreground = new SolidColorBrush(Pale);
            r.End.Effect = hot ? new DropShadowEffect { Color = ClayHi, BlurRadius = 10, ShadowDepth = 0, Opacity = .5 } : null;
        }
    }

    static string Mb(double mb) => mb >= 1000 ? $"{mb / 1024:0.0} GB" : $"{Math.Max(1, mb):0} MB";
    void Update(Row r, Tasks.App a)
    {
        r.App = a;
        r.Name.Text = a.Name;
        r.Line.Text = a.Hung ? "Not responding" : a.Line;
        r.Line.Foreground = new SolidColorBrush(a.Hung ? Amber : Sub);
        r.Line.FontWeight = a.Hung ? FontWeights.SemiBold : FontWeights.Normal;
        r.Mem.Text = Mb(a.MemMb); r.Mem.Foreground = new SolidColorBrush(a.MemMb > Heavy ? Pale : Ice);
        r.Cpu.Text = $"{a.Cpu:0}%"; r.Cpu.Foreground = new SolidColorBrush(a.Cpu > 25 ? Pale : Ice);
    }

    // ---- reading the PC, off the window's thread
    async void Refresh()
    {
        if (busy || closing) return;
        busy = true;
        try
        {
            var snap = await System.Threading.Tasks.Task.Run(model.Read);
            if (closing) return;
            cpuText.Text = $"{snap.Cpu:0}%"; Fill(cpuBar, snap.Cpu / 100, snap.Cpu > 70);
            memText.Text = $"{snap.UsedGb:0.0} of {snap.TotalGb:0} GB"; Fill(memBar, snap.UsedGb / Math.Max(1, snap.TotalGb), snap.UsedGb / Math.Max(1, snap.TotalGb) > .8);
            Place(openList, snap.Apps.Where(a => !a.Locked && a.Windows.Count > 0), nOpen);
            Place(bgList, snap.Apps.Where(a => !a.Locked && a.Windows.Count == 0), nBg);
            Place(keepList, snap.Apps.Where(a => a.Locked), nKeep);
            Paint();
        }
        catch (Exception e) { Program.Log("tasks: " + e); }
        finally { busy = false; }
    }

    // heaviest first, but only re-sorted when an app comes or goes, so rows don't jump under the mouse
    void Place(StackPanel list, IEnumerable<Tasks.App> apps, TextBlock count)
    {
        var want = apps.ToList();
        var keys = want.Select(a => a.Key).ToHashSet();
        var mine = list.Children.OfType<Border>().Select(b => rows.Values.FirstOrDefault(r => r.El == b)).Where(r => r != null).ToList();
        bool same = mine.Count == want.Count && mine.All(r => keys.Contains(r!.App.Key));
        if (same) { foreach (var a in want) Update(rows[a.Key], a); }
        else
        {
            list.Children.Clear();
            foreach (var a in want.OrderByDescending(a => a.MemMb))
            {
                if (!rows.TryGetValue(a.Key, out var r) || r.Gone) rows[a.Key] = r = MakeRow(a); else Update(r, a);
                if (r.El.Parent is Panel p) p.Children.Remove(r.El);
                list.Children.Add(r.El);
            }
            foreach (var k in rows.Keys.Where(k => rows[k].El.Parent == null).ToList()) rows.Remove(k);
        }
        count.Text = "· " + want.Count;
    }

    List<Row> Order() => new[] { openList, bgList, keepList }.Where(l => l.Visibility == Visibility.Visible)
        .SelectMany(l => l.Children.OfType<Border>()).Select(b => rows.Values.FirstOrDefault(r => r.El == b)).OfType<Row>().ToList();

    void Paint()
    {
        var order = Order();
        for (int i = 0; i < order.Count; i++)
        {
            var b = order[i].El; bool on = i == cur;
            b.Background = on ? new SolidColorBrush(Color.FromArgb(0x24, 0x38, 0xC6, 0xFF)) : order[i].App.Hung ? new SolidColorBrush(Color.FromArgb(0x14, 0xEE, 0xA0, 0x40)) : Brushes.Transparent;
            b.BorderBrush = on ? new SolidColorBrush(Color.FromArgb(0xCC, 0x38, 0xC6, 0xFF)) : Brushes.Transparent;
            b.Effect = on ? new DropShadowEffect { Color = Cyan, BlurRadius = 10, ShadowDepth = 0, Opacity = .3 } : null;
        }
    }

    // ---- ending an app: the first press asks (amber "End?"), the second ends it
    void Press(Row r)
    {
        if (r.Gone || r.App.Locked) return;
        if (!r.Sure) { r.Sure = true; PaintEnd(r, true); return; }
        r.Gone = true;
        var a = r.App;
        var denied = Tasks.End(a);
        Program.Log($"tasks: ended {a.Name} ({a.Pids.Count} processes){(denied.Count > 0 ? $", {denied.Count} need admin" : "")}");
        if (denied.Count > 0)
        {
            r.Gone = false; r.Sure = false; PaintEnd(r, false);
            GetCursorPos(out var c);
            ShrineMenu.Confirm($"{a.Name} was started as admin", a.Icon, "End it as admin", () => { Tasks.EndAsAdmin(denied); Later(Refresh); }, new Point(c.X, c.Y), upward: false);
            return;
        }
        // the row folds away, then the list reads the PC again
        r.El.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)));
        r.El.BeginAnimation(HeightProperty, new DoubleAnimation(r.El.ActualHeight, 0, TimeSpan.FromMilliseconds(340)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        Later(() => { if (r.El.Parent is Panel p) p.Children.Remove(r.El); rows.Remove(a.Key); Refresh(); });
    }
    void Later(Action a) { var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) }; t.Tick += (_, _) => { t.Stop(); if (!closing) a(); }; t.Start(); }

    void Menu(Row r)
    {
        var a = r.App; GetCursorPos(out var c); var at = new Point(c.X, c.Y);
        var items = new List<MenuEntry?>();
        if (a.Windows.Count > 0) items.Add(new("Bring to front", () => { Close(); Tasks.Front(a); }));
        if (a.Exe != null) items.Add(new("Open file location", () => { Close(); Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{a.Exe}\"") { UseShellExecute = true }); }));
        if (!a.Locked) { items.Add(null); items.Add(new($"End {a.Name}", () => { r.Sure = true; Press(r); }, Warn: true)); }
        if (items.Count > 0) ShrineMenu.Open(a.Name, a.Icon, items, at, upward: false);
    }

    // ---- keys: ↑↓ move, Enter brings it to the front, Delete ends (press twice), Esc closes
    void Key(object? _, KeyEventArgs e)
    {
        // a lone Alt (ours, from Bring) must not put the window in menu mode: that swallowed every key after it
        if (e.Key == System.Windows.Input.Key.System && e.SystemKey is System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt) { e.Handled = true; return; }
        var order = Order();
        switch (e.Key)
        {
            case System.Windows.Input.Key.Escape: Close(); break;
            case System.Windows.Input.Key.Down: cur = Math.Clamp(cur + 1, 0, Math.Max(0, order.Count - 1)); Paint(); Show(order); break;
            case System.Windows.Input.Key.Up: cur = Math.Clamp(cur - 1, 0, Math.Max(0, order.Count - 1)); Paint(); Show(order); break;
            case System.Windows.Input.Key.Enter when cur >= 0 && cur < order.Count && order[cur].App.Windows.Count > 0: Close(); Tasks.Front(order[cur].App); break;
            case System.Windows.Input.Key.Delete when cur >= 0 && cur < order.Count: Press(order[cur]); break;
            default: return;
        }
        e.Handled = true;
        void Show(List<Row> o) { foreach (var r in o) if (r.Sure && o.IndexOf(r) != cur) { r.Sure = false; PaintEnd(r, false); } if (cur >= 0 && cur < o.Count) o[cur].El.BringIntoView(); }
    }

    // ---- plumbing
    bool CursorOnMe() { GetCursorPos(out var c); return c.X >= Left && c.X <= Left + ActualWidth && c.Y >= Top && c.Y <= Top + ActualHeight; }
    static bool MenuOpen() => Application.Current.Windows.OfType<ShrineMenu>().Any();
    // a background app may not take the foreground; a tap of Alt lets it. Not needed when the dock already has the
    // foreground (opened from our Start), where the Alt landed on this window instead.
    static void Bring(Window w)
    {
        var h = new System.Windows.Interop.WindowInteropHelper(w).Handle;
        GetWindowThreadProcessId(GetForegroundWindow(), out var fg);
        bool ours = fg == (uint)Environment.ProcessId;
        if (!ours) keybd_event(0x12, 0, 0, WinKey.Mine);
        SetForegroundWindow(h);
        if (!ours) keybd_event(0x12, 0, 2, WinKey.Mine);
        w.Activate();
    }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    struct POINT { public int X, Y; }
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}

// What's running, read the way Task Manager's Processes tab groups it: each app is a process plus the helpers it
// started (Claude's 21 processes are one Claude), split where a child opens windows of its own under another program
// (Samara under Aang's brain). Windows' own processes, other sessions and services are left out.
class Tasks
{
    public sealed class App
    {
        public required string Key, Name, Line;
        public string? Exe;
        public System.Windows.Media.ImageSource? Icon;
        public List<uint> Pids = [];
        public List<IntPtr> Windows = [];
        public double MemMb, Cpu;
        public bool Hung, Locked;
    }
    public record Snapshot(List<App> Apps, double Cpu, double UsedGb, double TotalGb);

    static readonly string WinDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
    // where a family stops: programs that start other programs for him (a child of these is an app of its own)
    static readonly HashSet<string> Starters = new(StringComparer.OrdinalIgnoreCase) { "explorer.exe", "svchost.exe", "sihost.exe", "RuntimeBroker.exe", "ShrineDock.exe", "taskhostw.exe", "winlogon.exe", "userinit.exe", "services.exe", "wininit.exe", "dllhost.exe", "ApplicationFrameHost.exe", "smss.exe", "csrss.exe" };
    // a few words for apps that run without a window (sheet 20's)
    static readonly (string match, string line)[] Blurbs =
    [
        ("googledrivefs", "Keeps your files in sync"), ("onedrive", "Keeps your files in sync"), ("tailscale", "Links the Shadow and your Mac"),
        ("ollama", "Runs Qwen for Aang"), ("aang", "Your pet"), ("vbcscompiler", "Left over from a code build"), ("lively", "Draws the garden"), ("windhawk", "Shrine's Windows tweaks"), ("rainmeter", "Gauges and clock"),
        ("discord", "Still running after you closed it"), ("ms-teams", "Still running after you closed it"), ("spotify", "Still running after you closed it"),
        ("steam", "Still running after you closed it"), ("curseforge", "Still running after you closed it"), ("nvcontainer", "NVIDIA's helper"), ("nvidia", "NVIDIA's helper"),
    ];
    static readonly Dictionary<string, string> Plain = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ollama app"] = "Ollama", ["Tailscale GUI client"] = "Tailscale", ["Lively"] = "Lively Wallpaper", ["VBCSCompiler"] = "C# build helper",
        ["Microsoft Edge WebView2"] = "Edge WebView2", ["GoogleDriveFS"] = "Google Drive", ["Rainmeter desktop customization tool"] = "Rainmeter", ["Nvcontainer"] = "NVIDIA Container", ["nvcontainer"] = "NVIDIA Container",
    };
    static readonly Dictionary<string, System.Windows.Media.ImageSource?> icons = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, string> names = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<uint, (long created, long cpu)> lastCpu = [];
    long lastTick, lastIdle, lastKernel, lastUser;

    sealed class P { public uint Pid, Parent; public string File = ""; public string? Path; public long Created, Cpu; public double Mb; }

    public Snapshot Read()
    {
        uint me = (uint)Environment.ProcessId; ProcessIdToSessionId(me, out var session);
        var ps = new Dictionary<uint, P>();
        var snap = CreateToolhelp32Snapshot(2, 0);
        try
        {
            var e = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            for (bool ok = Process32First(snap, ref e); ok; ok = Process32Next(snap, ref e))
            {
                if (e.th32ProcessID == 0 || !ProcessIdToSessionId(e.th32ProcessID, out var s) || s != session) continue;
                var p = new P { Pid = e.th32ProcessID, Parent = e.th32ParentProcessID, File = e.szExeFile };
                var h = OpenProcess(0x1000 | 0x0010, false, p.Pid);
                if (h == IntPtr.Zero) h = OpenProcess(0x1000, false, p.Pid);
                if (h != IntPtr.Zero)
                {
                    try
                    {
                        var sb = new StringBuilder(1024); int n = sb.Capacity;
                        if (QueryFullProcessImageName(h, 0, sb, ref n)) p.Path = sb.ToString();
                        if (GetProcessTimes(h, out var c, out _, out var k, out var u)) { p.Created = c; p.Cpu = k + u; }
                        var m = new PROCESS_MEMORY_COUNTERS_EX { cb = (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS_EX>() };
                        if (GetProcessMemoryInfo(h, ref m, m.cb)) p.Mb = (double)(ulong)m.PrivateWorkingSetSize / (1024 * 1024); // what Task Manager calls Memory
                    }
                    finally { CloseHandle(h); }
                }
                ps[p.Pid] = p;
            }
        }
        finally { CloseHandle(snap); }

        // each window's process, and whether it's hung
        var wins = new Dictionary<uint, List<IntPtr>>(); var hung = new HashSet<uint>();
        foreach (var w in StripWindow.OpenWindows())
        {
            GetWindowThreadProcessId(w, out var pid);
            (wins.TryGetValue(pid, out var l) ? l : wins[pid] = []).Add(w);
            if (IsHungAppWindow(w)) hung.Add(pid);
        }

        bool IsWindows(P p) => p.Path == null || p.Path.StartsWith(WinDir, StringComparison.OrdinalIgnoreCase);
        // its family's head: up the parents while the parent is alive, older than it, not a starter, and not a
        // different program that has windows of its own
        P? Up(P p)
        {
            if (!ps.TryGetValue(p.Parent, out var par) || par.Pid == p.Pid) return null;
            if (p.Created != 0 && par.Created > p.Created) return null; // the parent's id was reused
            if (Starters.Contains(par.File)) return null;
            if (wins.ContainsKey(p.Pid) && !string.Equals(par.File, p.File, StringComparison.OrdinalIgnoreCase) && !IsWindows(p)) return null;
            return par;
        }
        var head = new Dictionary<uint, uint>();
        uint Head(P p) { if (head.TryGetValue(p.Pid, out var h)) return h; var up = Up(p); h = up == null ? p.Pid : Head(up); head[p.Pid] = h; return h; }

        // cpu since the last read
        long now = Environment.TickCount64; double span = Math.Max(1, (now - lastTick) * 10_000.0) * Environment.ProcessorCount; // 100 ns units
        bool first = lastTick == 0;
        var cpuOf = new Dictionary<uint, double>();
        foreach (var p in ps.Values)
        {
            if (lastCpu.TryGetValue(p.Pid, out var was) && was.created == p.Created && !first) cpuOf[p.Pid] = Math.Max(0, (p.Cpu - was.cpu) / span * 100);
            lastCpu[p.Pid] = (p.Created, p.Cpu);
        }
        foreach (var gone in lastCpu.Keys.Where(k => !ps.ContainsKey(k)).ToList()) lastCpu.Remove(gone);
        lastTick = now;

        var apps = new List<App>();
        foreach (var fam in ps.Values.GroupBy(Head))
        {
            var members = fam.ToList();
            if (members.Any(m => m.Pid == me)) { apps.Add(Locked("dock", "Shrine Dock", "This menu, your dock and Start", members, cpuOf, ps[me].Path)); continue; }
            if (members.All(m => m.File.StartsWith("Shadow", StringComparison.OrdinalIgnoreCase) || m.File.StartsWith("shadow_", StringComparison.OrdinalIgnoreCase)))
            { apps.Add(Locked("shadow", "Shadow", "Ending it would cut you off this PC", members, cpuOf, null)); continue; }
            // the family shows as its first own program, nearest the head (a .cmd's cmd.exe is Windows', what it ran isn't)
            var face = members.OrderBy(m => Depth(m)).FirstOrDefault(m => !IsWindows(m));
            if (face == null) continue;
            var w = members.SelectMany(m => wins.TryGetValue(m.Pid, out var l) ? l : []).ToList();
            var a = new App
            {
                Key = $"{fam.Key}:{ps[fam.Key].Created}", Exe = face.Path, Name = NameOf(face.Path!, w), Line = "",
                Icon = IconOf(face.Path!, w), Pids = members.Select(m => m.Pid).ToList(), Windows = w,
                MemMb = members.Sum(m => m.Mb), Cpu = Math.Min(100, members.Sum(m => cpuOf.GetValueOrDefault(m.Pid))), Hung = members.Any(m => hung.Contains(m.Pid)),
            };
            a.Line = w.Count > 1 ? $"{w.Count} windows" : w.Count == 1 ? TitleOf(w[0], a.Name) : BlurbOf(face.File);
            apps.Add(a);
        }
        // the two dock-and-Shadow rows join into their own kinds even when they came as several families
        apps = apps.GroupBy(a => a.Locked ? a.Key : Guid.NewGuid().ToString()).Select(g => g.Count() == 1 ? g.First() : Merge(g.ToList())).ToList();

        // the whole PC
        double cpu = 0;
        if (GetSystemTimes(out var idle, out var kern, out var user))
        {
            long di = idle - lastIdle, dt = (kern - lastKernel) + (user - lastUser);
            if (lastKernel != 0 && dt > 0) cpu = Math.Clamp((dt - di) * 100.0 / dt, 0, 100);
            lastIdle = idle; lastKernel = kern; lastUser = user;
        }
        var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref ms);
        double gb = 1024.0 * 1024 * 1024;
        return new Snapshot(apps, cpu, (ms.ullTotalPhys - ms.ullAvailPhys) / gb, Math.Round(ms.ullTotalPhys / gb));

        int Depth(P p) { int d = 0; for (var x = p; x != null && d < 64; x = Up(x)) d++; return d; }
    }

    App Locked(string key, string name, string line, List<P> members, Dictionary<uint, double> cpuOf, string? exe) => new()
    {
        Key = key, Name = name, Line = line, Locked = true, Exe = exe, Icon = exe != null ? IconOf(exe, []) : null, Pids = members.Select(m => m.Pid).ToList(),
        MemMb = members.Sum(m => m.Mb), Cpu = Math.Min(100, members.Sum(m => cpuOf.GetValueOrDefault(m.Pid))),
    };
    static App Merge(List<App> g) { var a = g[0]; foreach (var b in g.Skip(1)) { a.Pids.AddRange(b.Pids); a.MemMb += b.MemMb; a.Cpu = Math.Min(100, a.Cpu + b.Cpu); } return a; }

    static string BlurbOf(string file)
    {
        var f = file.ToLowerInvariant();
        foreach (var (m, l) in Blurbs) if (f.Contains(m)) return l;
        return "Running without a window";
    }
    static string TitleOf(IntPtr w, string name)
    {
        var sb = new StringBuilder(256); GetWindowText(w, sb, 256); var t = sb.ToString().Trim();
        return t.Length == 0 || t == name ? "1 window" : t;
    }
    // the program's own name (its file description), or for a shared engine like Electron its window's title
    static string NameOf(string exe, List<IntPtr> wins)
    {
        if (!names.TryGetValue(exe, out var n))
        {
            try { var v = FileVersionInfo.GetVersionInfo(exe); n = !string.IsNullOrWhiteSpace(v.FileDescription) ? v.FileDescription.Trim() : v.ProductName?.Trim() ?? ""; } catch { n = ""; }
            if (n.Length == 0) n = System.IO.Path.GetFileNameWithoutExtension(exe);
            if (Plain.TryGetValue(n, out var plain)) n = plain;
            else if (n.Length > 0 && char.IsLower(n[0])) n = char.ToUpperInvariant(n[0]) + n[1..];
            names[exe] = n;
        }
        if ((n is "Electron" || n.StartsWith("Node.js", StringComparison.Ordinal)) && wins.Count > 0)
        {
            var sb = new StringBuilder(256); GetWindowText(wins[0], sb, 256); var t = sb.ToString();
            var i = t.LastIndexOf(" - ", StringComparison.Ordinal); t = (i >= 0 ? t[(i + 3)..] : t).Trim();
            if (t.Length is > 0 and < 40) return t;
        }
        return n;
    }
    static System.Windows.Media.ImageSource? IconOf(string exe, List<IntPtr> wins)
    {
        bool shared = exe.EndsWith("electron.exe", StringComparison.OrdinalIgnoreCase);
        if (!shared && icons.TryGetValue(exe, out var c)) return c;
        System.Windows.Media.ImageSource? src = null;
        if (shared && wins.Count > 0)
        {
            SendMessageTimeout(wins[0], 0x7F, (IntPtr)1, IntPtr.Zero, 2, 100, out var hi);
            if (hi == IntPtr.Zero) hi = GetClassLongPtr(wins[0], -14);
            if (hi != IntPtr.Zero) try { var s = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(hi, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()); s.Freeze(); src = s; } catch { }
        }
        src ??= StripWindow.FromExe(exe) ?? ShellIcon(exe);
        if (!shared) icons[exe] = src;
        return src;
    }

    static System.Windows.Media.ImageSource? ShellIcon(string exe)
    {
        var fi = new SHFILEINFO();
        if (SHGetFileInfo(exe, 0, ref fi, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x100 /*ICON*/) == IntPtr.Zero || fi.hIcon == IntPtr.Zero) return null;
        try { var s = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(fi.hIcon, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()); s.Freeze(); return s; }
        catch { return null; } finally { DestroyIcon(fi.hIcon); }
    }

    /// <summary>Ends every process of the app at once, as Task Manager's End task does. Returns the ones Windows
    /// wouldn't let us end (started as admin).</summary>
    public static List<uint> End(App a)
    {
        var denied = new List<uint>();
        foreach (var pid in a.Pids.AsEnumerable().Reverse())
        {
            var h = OpenProcess(0x0001, false, pid);
            if (h == IntPtr.Zero) { if (Marshal.GetLastWin32Error() == 5) denied.Add(pid); continue; }
            try { if (!TerminateProcess(h, 1) && Marshal.GetLastWin32Error() == 5) denied.Add(pid); } finally { CloseHandle(h); }
        }
        return denied;
    }
    public static void EndAsAdmin(List<uint> pids)
    {
        try { Process.Start(new ProcessStartInfo("taskkill.exe", "/f " + string.Join(" ", pids.Select(p => "/pid " + p))) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden }); }
        catch (Exception e) { Program.Log("tasks: admin end " + e.Message); } // he said no to the admin prompt
    }
    public static void Front(App a)
    {
        var w = a.Windows.FirstOrDefault(); if (w == IntPtr.Zero) return;
        if (IsIconic(w)) ShowWindow(w, 9);
        keybd_event(0x12, 0, 0, WinKey.Mine); SetForegroundWindow(w); keybd_event(0x12, 0, 2, WinKey.Mine);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PROCESSENTRY32 { public uint dwSize, cntUsage, th32ProcessID; public IntPtr th32DefaultHeapID; public uint th32ModuleID, cntThreads, th32ParentProcessID; public int pcPriClassBase; public uint dwFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile; }
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_MEMORY_COUNTERS_EX { public uint cb, PageFaultCount; public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage, QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage, PrivateUsage, PrivateWorkingSetSize; public ulong SharedCommitUsage; }
    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX { public uint dwLength, dwMemoryLoad; public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO { public IntPtr hIcon; public int iIcon; public uint dwAttributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO fi, uint size, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32First(IntPtr s, ref PROCESSENTRY32 e);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32Next(IntPtr s, ref PROCESSENTRY32 e);
    [DllImport("kernel32.dll")] static extern bool ProcessIdToSessionId(uint pid, out uint session);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder sb, ref int n);
    [DllImport("kernel32.dll")] static extern bool GetProcessTimes(IntPtr h, out long created, out long exited, out long kernel, out long user);
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
    [DllImport("psapi.dll")] static extern bool GetProcessMemoryInfo(IntPtr h, ref PROCESS_MEMORY_COUNTERS_EX m, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool TerminateProcess(IntPtr h, uint code);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsHungAppWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] static extern IntPtr GetClassLongPtr(IntPtr h, int i);
}
