using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace ShrineDock;

// The strip's volume drop-down (sheet 17 v2): the same mixer rows as the Sound page, smaller, in the strip's own
// right-click menu pane, under the speaker tile. "All sound settings…" opens the Sound page. Esc or a click outside closes it.
class VolumePopup : Window
{
    static VolumePopup? current;
    static DateTime closedByOutsideClick;
    static readonly Color Cyan = Color.FromRgb(0x38, 0xC6, 0xFF), Ice = Color.FromRgb(0xC4, 0xF2, 0xFF);
    static readonly FontFamily Font = new("Exo 2, Segoe UI");
    readonly MixerView mixer = new(compact: true);
    readonly StackPanel rows = new();
    // his pick (2026-11-03): resting on the speaker tile opens it too. Opened by hover it closes half a second after the
    // mouse leaves the tile and the pane; a click keeps it open (pinned) until a click elsewhere or Esc.
    Rect tile = Rect.Empty;   // the speaker tile on screen, in DIPs: clicks and the mouse there count as on the pane
    bool pinned = true;
    DateTime awaySince = DateTime.MaxValue;
    const double LeaveMs = 500;

    /// <param name="under">the speaker tile's bottom centre, in screen DIPs</param>
    /// <param name="tileRect">the tile itself, in screen DIPs</param>
    public static void Toggle(Point under, Rect tileRect = default)
    {
        // open from a hover: the click pins it instead of closing it
        if (current != null && !current.pinned) { current.pinned = true; current.Activate(); return; }
        if (current != null) { current.Close(); return; }
        if ((DateTime.Now - closedByOutsideClick).TotalMilliseconds < 500) return; // the click on the tile that just closed it
        Open(under, tileRect, pinned: true);
    }
    /// <summary>The mouse rested on the speaker tile.</summary>
    public static void Hover(Point under, Rect tileRect)
    {
        if (current != null) return;
        Open(under, tileRect, pinned: false);
    }
    static void Open(Point under, Rect tileRect, bool pinned)
    {
        current = new VolumePopup { tile = tileRect, pinned = pinned, ShowActivated = pinned };
        current.Closed += (s, _) => { if (current == s) current = null; };
        current.Place(under);
    }

    VolumePopup()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; SizeToContent = SizeToContent.WidthAndHeight; Title = "Shrine volume"; Opacity = 0;
        var list = new StackPanel { Margin = new Thickness(6, 4, 6, 8), Width = 336 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 10, 14, 6) };
        var spk = MixerView.Speaker(false, Ice, 20); spk.Margin = new Thickness(0, 0, 8, 0); head.Children.Add(spk);
        head.Children.Add(new TextBlock { Text = "Volume", FontFamily = Font, FontStyle = FontStyles.Italic, FontWeight = FontWeights.SemiBold, FontSize = 15, Foreground = new SolidColorBrush(Ice),
            VerticalAlignment = VerticalAlignment.Center, Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 8, ShadowDepth = 0, Opacity = .7 } });
        list.Children.Add(head);
        list.Children.Add(ShrineMenu.Rule());
        list.Children.Add(rows);
        list.Children.Add(ShrineMenu.Rule());
        var all = new Border { CornerRadius = new CornerRadius(7), Padding = new Thickness(10, 7, 16, 7), Margin = new Thickness(0, 0, 0, 0), Background = Brushes.Transparent, Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "All sound settings…", FontFamily = Font, FontSize = 14, Foreground = new SolidColorBrush(Ice) } };
        all.MouseEnter += (_, _) => { all.Background = new SolidColorBrush(Color.FromArgb(0x40, 0x38, 0xC6, 0xFF)); all.Child.Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 8, ShadowDepth = 0, Opacity = .8 }; };
        all.MouseLeave += (_, _) => { all.Background = Brushes.Transparent; all.Child.Effect = null; };
        all.MouseLeftButtonUp += (_, _) => { Close(); SettingsPanel.OpenOn("Sound"); };
        list.Children.Add(all);
        Content = ShrineMenu.Pane(list);

        mixer.Changed += Fill;
        bool closing = false;
        var watch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        Closing += (_, _) => { closing = true; watch.Stop(); mixer.Stop(); };
        Deactivated += (_, _) => { if (!closing) Close(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        // his rule for every popup: Esc or a click anywhere outside closes it, focused or not
        watch.Tick += (_, _) =>
        {
            if (closing) return;
            if ((GetAsyncKeyState(0x1B) & 0x8000) != 0) { Close(); return; }
            bool down = (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0;
            bool here = CursorOnPane(), onTile = tile.Contains(MouseAt());
            if (down && !here && !onTile) { closedByOutsideClick = DateTime.Now; Close(); return; }
            if (pinned) return;
            // opened by hover: touching it keeps it (a drag may leave the pane); leaving the tile and the pane closes it
            if (down && here) { pinned = true; return; }
            if (here || onTile) awaySince = DateTime.MaxValue;
            else if (awaySince == DateTime.MaxValue) awaySince = DateTime.Now;
            else if ((DateTime.Now - awaySince).TotalMilliseconds > LeaveMs) Close();
        };
        Loaded += (_, _) => watch.Start();
    }

    void Fill()
    {
        rows.Children.Clear();
        foreach (var r in mixer.Rows)
        {
            if (r.Element.Parent is Border old) old.Child = null;
            var row = new Border { CornerRadius = new CornerRadius(7), Padding = new Thickness(10, 0, 6, 0), Background = Brushes.Transparent, Child = r.Element };
            row.MouseEnter += (_, _) => row.Background = new SolidColorBrush(Color.FromArgb(0x29, 0x38, 0xC6, 0xFF));
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
            rows.Children.Add(row);
        }
        // no "Nothing playing right now" line when it's quiet (his ask 2026-11-04: it only took up space)
    }

    void Place(Point under)
    {
        mixer.Start(); Fill();
        Left = under.X; Top = under.Y; if (pinned) FullScreen.GiveBackOnClose(this); Show(); if (pinned) Activate(); UpdateLayout();
        var wa = Screens.WorkAreaAt(under);
        // its right edge lines up a little past the tile, like the sheet; the pane's 14 px shadow margin is outside
        Left = Math.Clamp(under.X - ActualWidth + 48, wa.Left + 4, wa.Right - ActualWidth - 4);
        Top = Math.Clamp(under.Y - 6, wa.Top + 4, wa.Bottom - ActualHeight - 4);
        var lift = new TranslateTransform(0, -8); RenderTransform = null;
        if (Content is FrameworkElement f) { f.RenderTransform = lift; lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } }); }
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
    }

    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    struct POINT { public int X, Y; }
    Point MouseAt() { GetCursorPos(out var p); var d = VisualTreeHelper.GetDpi(this); return new Point(p.X / d.DpiScaleX, p.Y / d.DpiScaleY); }
    bool CursorOnPane()
    {
        var c = MouseAt();
        return c.X >= Left + 14 && c.X <= Left + ActualWidth - 14 && c.Y >= Top + 14 && c.Y <= Top + ActualHeight - 14;
    }
}
