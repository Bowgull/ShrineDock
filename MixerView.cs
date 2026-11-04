
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ShrineDock;

// The mixer's rows (sheet 17 v2), shared by the settings menu's Sound page and the strip's drop-down: the whole PC on
// top, then each app making sound, in the order they first played (they never move). Each row: icon, name, the smooth
// carved slider with the live-sound line under it, the level, and the mute seal. An app silent for 10 s leaves, never
// while its slider is held. It only listens while it is on screen (Start/Stop).
sealed class MixerView
{
    static readonly Color Cyan = Color.FromRgb(0x38, 0xC6, 0xFF), Ice = Color.FromRgb(0xC4, 0xF2, 0xFF), Hint = Color.FromRgb(0x5F, 0x86, 0xA0), Amber = Color.FromRgb(0xEE, 0xA0, 0x40),
        Clay = Color.FromRgb(0xB8, 0x92, 0x4C), ClayHi = Color.FromRgb(0xDD, 0xB8, 0x70), Well = Color.FromRgb(0x06, 0x12, 0x1C), WellEdge = Color.FromRgb(0x10, 0x20, 0x2A);
    static readonly FontFamily Font = new("Exo 2, Segoe UI"), Body = new("Figtree, Segoe UI");
    const double LeaveAfter = 10, QuietAfter = 1.2;

    public sealed class Row
    {
        public required string Key;
        public required Grid Element;
        public required Action<int> Nudge;
        public required Action ToggleMute;
        internal Action<float>? Live;
        internal Action? Paint;
        internal bool Held;
        internal double NameNeed;            // the width its name needs (0: no name)
        internal ColumnDefinition? NameCol;
        internal double Silent;
    }

    readonly bool compact;
    AudioMixer? audio;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    readonly List<string> order = [];               // first-seen order
    readonly Dictionary<string, Row> made = [];
    int tick;
    DateTime last = DateTime.Now;
    // Samara's music player: what it shows, the level just set here (Spotify answers a moment later), the level before a mute
    AudioMixer.Player.Now? player;
    (float v, DateTime at) playerSet;
    float playerMutedFrom;
    public readonly List<Row> Rows = [];
    public readonly TextBlock Nothing;
    public bool IsQuiet => Rows.Count <= 1;
    public event Action? Changed;

    public MixerView(bool compact)
    {
        this.compact = compact;
        Nothing = new TextBlock { Text = "Nothing playing right now", FontFamily = Font, FontStyle = FontStyles.Italic, FontSize = 13.5, Foreground = new SolidColorBrush(Color.FromRgb(0x6F, 0x93, 0xAA)),
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 12) };
        timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        if (audio != null) return;
        audio = new AudioMixer(); audio.Refresh(); player = AudioMixer.Player.Read();
        // at open: everything playing now
        foreach (var a in audio.Apps.Values) if (a.Playing && !order.Contains(a.Key)) order.Add(a.Key);
        Build(); timer.Start();
    }
    public void Stop() { timer.Stop(); audio?.Dispose(); audio = null; }

    void Tick()
    {
        if (audio == null) return;
        var now = DateTime.Now; double dt = (now - last).TotalSeconds; last = now;
        bool changed = false;
        if (++tick % 10 == 0) player = AudioMixer.Player.Read();
        if (tick % 30 == 0)
        {
            audio.Refresh();
            foreach (var a in audio.Apps.Values) if (!order.Contains(a.Key) && a.Peak > .002) order.Add(a.Key);
        }
        // the live sound, and who has gone quiet
        foreach (var key in order.ToList())
        {
            audio.Apps.TryGetValue(key, out var a);
            float peak = a?.Peak ?? 0;
            var r = made.GetValueOrDefault(key);
            if (r == null) { if (a != null) changed = true; continue; }
            r.Silent = peak > .002 ? 0 : r.Silent + dt;
            bool gone = (a == null || r.Silent > LeaveAfter) && !r.Held;
            if (gone) { order.Remove(key); made.Remove(key); changed = true; continue; }
            r.Live?.Invoke(a!.Mute ? 0 : peak * a.Level * audio.Master);
            if (tick % 8 == 0) r.Paint?.Invoke();
        }
        // the player: Spotify gives no sound to measure from another device, so its line moves gently while it plays
        bool showPlayer = ShowPlayer();
        if (showPlayer != made.ContainsKey(PlayerKey)) { if (!showPlayer) made.Remove(PlayerKey); changed = true; }
        if (showPlayer && made.TryGetValue(PlayerKey, out var pr))
        {
            double s = now.TimeOfDay.TotalSeconds;
            float wob = (float)(.55 + .25 * Math.Sin(s * 7.3) * Math.Sin(s * 2.1) + .12 * Math.Sin(s * 13.7));
            pr.Live?.Invoke(player!.playing && !PlayerMuted() ? wob * PlayerLevel() * audio.Master : 0);
            if (tick % 8 == 0) pr.Paint?.Invoke();
        }
        if (made.TryGetValue("", out var main)) { main.Live?.Invoke(audio.MasterMute ? 0 : audio.MasterPeak); if (tick % 8 == 0) main.Paint?.Invoke(); }
        if (changed) { Build(); Changed?.Invoke(); }
    }

    void Build()
    {
        Rows.Clear();
        if (!made.ContainsKey("")) made[""] = MainRow();
        Rows.Add(made[""]);
        if (ShowPlayer()) { if (!made.ContainsKey(PlayerKey)) made[PlayerKey] = PlayerRow(); Rows.Add(made[PlayerKey]); }
        foreach (var key in order)
        {
            if (audio == null || !audio.Apps.TryGetValue(key, out var a)) continue;
            if (!made.TryGetValue(key, out var r)) made[key] = r = AppRow(a);
            Rows.Add(r);
        }
        // the rule (his asks 2026-11-04), like a table: the name column is as wide as the longest name shown, no wider;
        // every slider starts there and takes all the room left, so all bars are the same length and line up. Whole PC
        // has no name: alone, its bar starts at its icon; with apps under it, it lines up with theirs.
        double names = Math.Min(NameMax, Rows.Select(x => x.NameNeed).DefaultIfEmpty(0).Max());
        foreach (var x in Rows) if (x.NameCol != null) x.NameCol.Width = new GridLength(names > 0 ? names + Gap : 0);
    }
    double NameMax => compact ? 110 : 140;
    double Gap => compact ? 8 : 9;

    Row MainRow()
    {
        Row r = null!;
        // his ask 2026-11-04: a PC in the dock folder's glowing line, in place of the words "Whole PC"
        r = MakeRow("", Art("mixer-pc.png", "Whole PC"), "", () => audio?.SpeakerName,
            () => audio?.Master ?? 0, v => { if (audio != null) audio.Master = v; },
            () => audio?.MasterMute ?? false, m => { if (audio != null) audio.MasterMute = m; }, () => false);
        return r;
    }
    Row AppRow(AudioMixer.App a)
    {
        UIElement icon;
        var src = a.Key == "samara" ? Pixel("rail-samara.png") : a.Exe != null ? StripWindow.FromExe(a.Exe) : null;
        // Cemu is Breath of the Wild: the game's own icon, whole (his pick over a face crop)
        if (a.Key == "cemu") icon = Art("mixer-botw.png", "Breath of the Wild");
        else if (src != null)
        {
            var img = new Image { Source = src, Width = IconSize, Height = IconSize };
            RenderOptions.SetBitmapScalingMode(img, a.Key == "samara" ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
            icon = img;
        }
        else icon = Speaker(false, Ice);
        return MakeRow(a.Key, icon, a.Name, () => null, () => Current(a.Key)?.Level ?? 0, v => Current(a.Key)?.SetLevel(v),
            () => Current(a.Key)?.Mute ?? false, m => Current(a.Key)?.SetMute(m), () => made.TryGetValue(a.Key, out var rr) && rr.Silent > QuietAfter);
    }
    // Spotify playing on this PC through its own app has its own row with Spotify's mark: then this one steps aside
    const string PlayerKey = "player";
    bool ShowPlayer() => player != null && !(audio?.Apps.ContainsKey("spotify") ?? false);
    float PlayerLevel() => (DateTime.Now - playerSet.at).TotalSeconds < 2.5 ? playerSet.v : player?.volume ?? 0;
    bool PlayerMuted() => PlayerLevel() <= 0 && playerMutedFrom > 0;
    void SetPlayer(float v) { playerSet = (v, DateTime.Now); if (v > 0) playerMutedFrom = 0; AudioMixer.Player.SetVolume(v); }
    Row PlayerRow()
    {
        var img = new Image { Source = Pixel("rail-samara.png"), Width = IconSize, Height = IconSize };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
        // Spotify has no mute of its own: muting takes it to 0 and remembers where it was
        return MakeRow(PlayerKey, img, "Spotify", () => player?.app == null ? "Nothing playing" : player.device ?? "Music player", PlayerLevel, SetPlayer, PlayerMuted,
            m => { if (m) { var was = PlayerLevel(); SetPlayer(0); playerMutedFrom = was > 0 ? was : .5f; } else { var back = playerMutedFrom > 0 ? playerMutedFrom : .5f; playerMutedFrom = 0; SetPlayer(back); } },
            () => player is { playing: false, app: not null }, "Paused",
            // no song: Spotify has no volume to give, so a dash, never a 0% that reads as muted
            () => player?.volume == null && (DateTime.Now - playerSet.at).TotalSeconds >= 2.5 ? "–" : null);
    }
    AudioMixer.App? Current(string key) => audio != null && audio.Apps.TryGetValue(key, out var a) ? a : null;
    double IconSize => compact ? 26 : 32;
    static ImageSource? Pixel(string f)
    {
        var p = System.IO.Path.Combine(AppContext.BaseDirectory, "art", f);
        return System.IO.File.Exists(p) ? new BitmapImage(new Uri(p)) : null;
    }

    Row MakeRow(string key, UIElement icon, string name, Func<string?> hint, Func<float> get, Action<float> set, Func<bool> muted, Action<bool> mute, Func<bool> quiet, string quietWord = "Quiet", Func<string?>? level = null)
    {
        double sliderW = compact ? 112 : 150, pctW = compact ? 40 : 44, gap = compact ? 8 : 9;
        // the row keeps the width it always had (icon, a short name, the old 150 px slider, level, seal); the slider grows into what the names leave
        var g = new Grid { MinWidth = IconSize + gap + 60 + sliderW + gap + pctW + gap + 24 + gap, Height = key == "" ? (compact ? 44 : 52) : (compact ? 40 : 48), Background = Brushes.Transparent };
        // icon | name (Build sets its width: the longest name) | slider (all the rest) | level | mute seal
        foreach (var w in new[] { IconSize, 0, double.NaN, pctW, 24 })
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = double.IsNaN(w) ? new GridLength(1, GridUnitType.Star) : new GridLength(w == 0 ? 0 : w + gap) });
        var iconHost = new Border { Width = IconSize, Height = IconSize, Child = icon, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        var label = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, gap, 0) };
        var title = new TextBlock { Text = name, FontFamily = Font, FontWeight = FontWeights.Medium, FontSize = compact ? 13.5 : 14.5, Foreground = new SolidColorBrush(Ice), TextTrimming = TextTrimming.CharacterEllipsis };
        var sub = new TextBlock { FontFamily = Body, FontSize = 11, Margin = new Thickness(0, -1, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        label.Children.Add(title); if (!compact) label.Children.Add(sub);
        Grid.SetColumn(label, 1);

        // the slider: sheet 10's carved well, smooth; the live line under it
        bool wide = name == "";   // no name (Whole PC shows only its icon): nothing between icon and slider
        var slider = new Grid { MinWidth = 60, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, gap, 0), Background = Brushes.Transparent, Cursor = Cursors.Hand };
        slider.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) }); slider.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
        var well = new Border { Height = 10, BorderThickness = new Thickness(2), BorderBrush = new SolidColorBrush(WellEdge), Background = new SolidColorBrush(Well), VerticalAlignment = VerticalAlignment.Center };
        var fill = new Rectangle { Height = 6, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0), Fill = new LinearGradientBrush(Color.FromRgb(0x15, 0x5A, 0x80), Cyan, 0) };
        var knob = new Ellipse { Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Fill = SettingsPanel.Seal(),
            Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 6, ShadowDepth = 0, Opacity = .7 } };
        var lineWell = new Border { Height = 3, CornerRadius = new CornerRadius(1.5), Background = new SolidColorBrush(Color.FromArgb(0x16, 0x38, 0xC6, 0xFF)), VerticalAlignment = VerticalAlignment.Top };
        var line = new Border { Height = 3, Width = 0, CornerRadius = new CornerRadius(1.5), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Background = new LinearGradientBrush(Cyan, Ice, 0), Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 7, ShadowDepth = 0, Opacity = .9 } };
        Grid.SetRow(lineWell, 1); Grid.SetRow(line, 1);
        slider.Children.Add(well); slider.Children.Add(fill); slider.Children.Add(knob); slider.Children.Add(lineWell); slider.Children.Add(line);
        Grid.SetColumn(slider, 2);
        double W() => slider.ActualWidth > 0 ? slider.ActualWidth : sliderW;

        var pct = new TextBlock { FontFamily = Font, FontStyle = FontStyles.Italic, FontWeight = FontWeights.SemiBold, FontSize = 13, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, gap, 0) };
        Grid.SetColumn(pct, 3);
        // the mute seal: the switch's seal on its own; amber when muted
        var seal = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(2), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(seal, 4);
        g.Children.Add(iconHost); if (!wide) g.Children.Add(label); g.Children.Add(slider); g.Children.Add(pct); g.Children.Add(seal);

        Row r = null!;
        void Paint()
        {
            float v = get(); bool m = muted();
            double w = W(), p = Math.Clamp(v, 0, 1);
            // the knob stays inside the well: its centre runs from 9 px in to 9 px from the end (at 100% it was half cut off)
            fill.Width = Math.Max(0, 7 + p * (w - 18)); knob.Margin = new Thickness(p * (w - 18), 0, 0, 0);
            pct.Text = m ? "Muted" : level?.Invoke() ?? Math.Round(v * 100) + "%";
            pct.Foreground = m ? new SolidColorBrush(Amber) : Brushes.White;
            iconHost.Opacity = title.Opacity = slider.Opacity = m ? .42 : 1;
            bool q = quiet();
            sub.Text = q ? quietWord : hint() ?? ""; sub.Foreground = new SolidColorBrush(q ? Color.FromRgb(0x8A, 0x7A, 0x5A) : Hint);
            sub.Visibility = sub.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            seal.BorderBrush = new SolidColorBrush(m ? Color.FromRgb(0x9A, 0x64, 0x20) : Clay);
            seal.Background = new RadialGradientBrush(m ? Color.FromRgb(0x2A, 0x1A, 0x08) : Color.FromRgb(0x17, 0x32, 0x47), m ? Color.FromRgb(0x14, 0x0C, 0x04) : Color.FromRgb(0x0B, 0x1A, 0x26)) { GradientOrigin = new Point(.5, .5) };
            seal.Child = Speaker(m, Ice, 13);
            seal.ToolTip = null;
        }
        void Set(float v) { set(Math.Clamp(v, 0, 1)); Paint(); }
        void At(MouseEventArgs e) => Set((float)Math.Round(Math.Clamp((e.GetPosition(slider).X - 9) / (W() - 18), 0, 1) * 100) / 100f);
        slider.MouseLeftButtonDown += (_, e) => { r.Held = true; slider.CaptureMouse(); At(e); e.Handled = true; };
        slider.MouseMove += (_, e) => { if (slider.IsMouseCaptured) At(e); };
        slider.MouseLeftButtonUp += (_, e) => { slider.ReleaseMouseCapture(); r.Held = false; e.Handled = true; };
        slider.LostMouseCapture += (_, _) => r.Held = false;
        g.MouseWheel += (_, e) => { Set(get() + (e.Delta > 0 ? .02f : -.02f)); e.Handled = true; };
        seal.MouseEnter += (_, _) => seal.Effect = new DropShadowEffect { Color = ClayHi, BlurRadius = 10, ShadowDepth = 0, Opacity = .5 };
        seal.MouseLeave += (_, _) => seal.Effect = null;
        seal.MouseLeftButtonUp += (_, e) => { mute(!muted()); Paint(); e.Handled = true; };
        r = new Row
        {
            Key = key, Element = g,
            Nudge = d => Set(get() + d * .02f),
            ToggleMute = () => { mute(!muted()); Paint(); },
            Live = lvl => line.Width = Math.Clamp(lvl, 0, 1) * W(),
            Paint = Paint,
        };
        slider.SizeChanged += (_, _) => Paint();
        if (!wide) { title.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); r.NameNeed = Math.Ceiling(title.DesiredSize.Width); }
        r.NameCol = g.ColumnDefinitions[1];
        Paint();
        return r;
    }

    /// <summary>The speaker glyph from the sheet (a cone and two waves; muted: amber with a cross).</summary>
    FrameworkElement Art(string file, string tip)
    {
        var img = new Image { Source = Pixel(file), Width = IconSize, Height = IconSize, ToolTip = tip };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        return img;
    }
    internal static FrameworkElement Speaker(bool muted, Color c, double size = 26)
    {
        var col = new SolidColorBrush(muted ? Color.FromRgb(0xEE, 0xA0, 0x40) : c);
        var cone = new Path { Data = Geometry.Parse("M4,9.5 H7.5 L12,5.5 V18.5 L7.5,14.5 H4 Z"), Stroke = col, StrokeThickness = 2.2, StrokeLineJoin = PenLineJoin.Round,
            Fill = new SolidColorBrush(Color.FromArgb(0x40, col.Color.R, col.Color.G, col.Color.B)) };
        var waves = new Path { Data = Geometry.Parse(muted ? "M16,9.5 L21,14.5 M21,9.5 L16,14.5" : "M15.5,9 A4,4 0 0 1 15.5,15 M18.3,6.5 A7.5,7.5 0 0 1 18.3,17.5"),
            Stroke = col, StrokeThickness = 2.2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        var canvas = new Canvas { Width = 24, Height = 24, Children = { cone, waves } };
        return new Viewbox { Width = size, Height = size, Child = canvas, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Effect = muted ? null : new DropShadowEffect { Color = Cyan, BlurRadius = 6, ShadowDepth = 0, Opacity = .6 } };
    }
}
