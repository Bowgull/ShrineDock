using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ShrineDock;

// The Night Lantern clock (sheet 09 B, his pick): pre-painted layers, and only the hands turn. The second hand
// sweeps as a GPU animation (24 fps), so the clock costs almost nothing; Rainmeter's own redraws were far heavier.
// Shown when the clock face is set to analog (ClockSettings.inc); it sits on the desktop under every window,
// can be dragged unless the widgets are locked, and stops while WoW or Cemu runs.
class ClockWindow : Window
{
    const double D = 400, ArchY = 296, WinX = 112, WinW = 54, WinH = 26;
    const string Res = @"C:\Users\Shadow\Documents\Rainmeter\Skins\Shrine\@Resources\";
    static string L(string f) => Res + @"Images\lantern\" + f;
    readonly RotateTransform hour = new(0, D / 2, D / 2), min = new(0, D / 2, D / 2), sec = new(0, D / 2, D / 2), glyph = new(0, D / 2, D / 2), disc = new(0, D / 2, ArchY);
    readonly List<Ellipse> motes = [];
    readonly Image secImg, stars, wxIcon = new();
    readonly TextBlock wxText = new(), date = new(), next = new();
    readonly Border nextBox;
    readonly LinearGradientBrush sky = new() { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
    // Toronto's sunrise and sunset for yesterday, today and tomorrow (open-meteo, with the weather); a plain 7-to-5
    // day until the first fetch lands
    DateTime[] rises = [.. Enumerable.Range(-1, 3).Select(n => DateTime.Today.AddDays(n).AddHours(6.92))];
    DateTime[] sets = [.. Enumerable.Range(-1, 3).Select(n => DateTime.Today.AddDays(n).AddHours(16.92))];
    readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly DispatcherTimer weather = new() { Interval = TimeSpan.FromMinutes(30) };
    readonly Func<Settings> settings;
    readonly ScaleTransform zoom = new(1, 1);
    bool asleep;
    IntPtr me;
    public IntPtr Hwnd => me;

    public ClockWindow(Func<Settings> settings)
    {
        this.settings = settings;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; ShowActivated = false; Width = D; Height = D; Title = "Shrine Clock";
        var c = new Canvas { Width = D, Height = D };
        // the arch at 6 (sheet 09 v9, his pick): a sky that changes with the real light, stars that fade in at night,
        // and the sun-and-moon disc turning from the real sunrise and sunset, all behind the face's cut-out arch
        var skyRect = new Rectangle { Width = 91, Height = 46, Fill = sky };
        Canvas.SetLeft(skyRect, D / 2 - 45.5); Canvas.SetTop(skyRect, ArchY - 43); c.Children.Add(skyRect);
        stars = Layer(c, "stars.png");
        Layer(c, "daynight.png", disc);
        Layer(c, "face.png");
        Layer(c, "glyphs.png", glyph);
        Motes(c);
        // weather at 9 and the date at 3, in the face's windows 112 px either side of the true centre
        var wx = new StackPanel { Orientation = Orientation.Horizontal };
        wxIcon.Width = 18; wxIcon.Height = 18; wxIcon.Margin = new Thickness(0, 0, 3, 0);
        Text(wxText, 12.5, FontWeights.Bold);
        wx.Children.Add(wxIcon); wx.Children.Add(wxText); wx.HorizontalAlignment = HorizontalAlignment.Center;
        var wxBox = new Grid { Width = WinW, Height = WinH }; wx.VerticalAlignment = VerticalAlignment.Center; wxBox.Children.Add(wx);
        Canvas.SetLeft(wxBox, D / 2 - WinX - WinW / 2); Canvas.SetTop(wxBox, D / 2 - WinH / 2); c.Children.Add(wxBox);
        Text(date, 12, FontWeights.Bold);
        var dateBox = new Grid { Width = WinW, Height = WinH, Children = { date } };
        Canvas.SetLeft(dateBox, D / 2 + WinX - WinW / 2); Canvas.SetTop(dateBox, D / 2 - WinH / 2); c.Children.Add(dateBox);
        Layer(c, "hour.png", hour); Layer(c, "min.png", min); secImg = Layer(c, "sec.png", sec);
        // rest the mouse on the arch: the next sunrise or sunset fades in just above it
        // sized to read at his smallest clock (300 px draws the 400 px design at 0.75): about 13 px on screen
        Text(next, 17, FontWeights.Bold);
        nextBox = new Border { Child = next, Padding = new Thickness(9, 3, 9, 4), CornerRadius = new CornerRadius(7), Opacity = 0, IsHitTestVisible = false,
            Background = new SolidColorBrush(Color.FromArgb(0xD1, 0x06, 0x0E, 0x16)), BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x8C, 0xDD, 0xB8, 0x70)) };
        var nextRow = new Grid { Width = 200, Children = { nextBox } }; nextBox.HorizontalAlignment = HorizontalAlignment.Center; nextRow.IsHitTestVisible = false;
        Canvas.SetLeft(nextRow, D / 2 - 100); Canvas.SetTop(nextRow, 214); c.Children.Add(nextRow);
        var arch = new Border { Width = 91, Height = 53, Background = Brushes.Transparent, CornerRadius = new CornerRadius(45, 45, 4, 4) };
        Canvas.SetLeft(arch, D / 2 - 45.5); Canvas.SetTop(arch, ArchY - 45); c.Children.Add(arch);
        arch.MouseEnter += (_, _) => Fade(1); arch.MouseLeave += (_, _) => Fade(0);
        c.LayoutTransform = zoom;
        Content = c;

        Loaded += (_, _) =>
        {
            me = new WindowInteropHelper(this).Handle;
            SetWindowLong(me, -20, GetWindowLong(me, -20) | 0x80 | 0x08000000); // a tool window that never takes focus
            HwndSource.FromHwnd(me).AddHook(KeepAtBottom); ToBottom();
        };
        MouseLeftButtonDown += (_, e) => { if (!settings().LockWidgets) { DragMove(); WidgetKeeper.Current?.Expect(); Settings.Save(Settings.Load() with { ClockX = (int)Left, ClockY = (int)Top }); } };
        tick.Tick += (_, _) => Tick(); weather.Tick += async (_, _) => await Weather();
        Apply();
    }

    // a few fireflies on a faint arc through the face's empty corners (never near the numerals or windows); each
    // breathes in and out on its own slow beat, out of step with the others, like the garden's fireflies
    void Motes(Canvas c)
    {
        (double angle, double beat)[] spots = [(-152, 3.4), (-128, 4.3), (-52, 3.8), (-28, 4.7), (146, 4.0)];
        foreach (var (a, beat) in spots)
        {
            double r = 100, x = D / 2 + r * Math.Cos(a * Math.PI / 180), y = D / 2 + r * Math.Sin(a * Math.PI / 180);
            var e = new Ellipse { Width = 9, Height = 9, Opacity = .1, IsHitTestVisible = false, Tag = beat,
                Fill = new RadialGradientBrush(new GradientStopCollection { new(Color.FromArgb(255, 234, 255, 200), 0), new(Color.FromArgb(160, 214, 255, 176), .35), new(Color.FromArgb(0, 214, 255, 176), 1) }) };
            Canvas.SetLeft(e, x - 4.5); Canvas.SetTop(e, y - 4.5); c.Children.Add(e); motes.Add(e);
        }
    }
    /// <summary>Draws the live clock to a PNG at 2x (what the window shows, even when other windows cover it).</summary>
    public void Snapshot(string path)
    {
        if (Content is not FrameworkElement c) return;
        var r = new RenderTargetBitmap((int)(c.ActualWidth * 2), (int)(c.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
        r.Render(c);
        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(r));
        using var f = File.Create(path); enc.Save(f);
    }

    void Breathe(bool on)
    {
        for (int i = 0; i < motes.Count; i++)
        {
            if (!on) { motes[i].BeginAnimation(OpacityProperty, null); continue; }
            var beat = (double)motes[i].Tag;
            var a = new DoubleAnimation(.08, .85, TimeSpan.FromSeconds(beat / 2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(i * .9), EasingFunction = new SineEase() };
            Timeline.SetDesiredFrameRate(a, 15);
            motes[i].BeginAnimation(OpacityProperty, a);
        }
    }

    static BitmapImage Img(string path) { var b = new BitmapImage(); b.BeginInit(); b.UriSource = new Uri(path); b.CacheOption = BitmapCacheOption.OnLoad; b.EndInit(); b.Freeze(); return b; }
    Image Layer(Canvas c, string file, Transform? t = null)
    {
        var i = new Image { Source = Img(L(file)), Width = D, Height = D, RenderTransform = t };
        RenderOptions.SetBitmapScalingMode(i, BitmapScalingMode.HighQuality);
        c.Children.Add(i); return i;
    }
    static void Text(TextBlock t, double size, FontWeight w)
    {
        t.FontFamily = new FontFamily("Exo 2, Segoe UI"); t.FontStyle = FontStyles.Italic; t.FontWeight = w; t.FontSize = size; t.TextAlignment = TextAlignment.Center;
        t.Foreground = new SolidColorBrush(Color.FromRgb(0xC4, 0xF2, 0xFF)); t.VerticalAlignment = VerticalAlignment.Center;
        t.Effect = new DropShadowEffect { Color = Color.FromRgb(0x38, 0xC6, 0xFF), BlurRadius = 6, ShadowDepth = 0, Opacity = .8 };
    }

    // ---- shown only when the face is analog; follows the panel's settings
    public static (bool analog, bool second) Face()
    {
        try
        {
            var t = File.ReadAllText(Res + "ClockSettings.inc");
            return (t.Contains("IsAnalog=1"), !t.Contains("SecondHand=0"));
        }
        catch { return (true, true); }
    }
    public void Apply()
    {
        var (analog, second) = Face(); var s = settings();
        double size = Math.Clamp(s.ClockSize, 240, 640);
        zoom.ScaleX = zoom.ScaleY = size / D; Width = Height = size; // the layers are painted at 512 px, so every size stays sharp
        Left = s.ClockX; Top = s.ClockY;
        secImg.Visibility = second ? Visibility.Visible : Visibility.Collapsed;
        if (analog && !asleep) Wake(); else Sleep();
    }
    void Wake()
    {
        if (!IsVisible) Show();
        Tick(); _ = Weather(); tick.Start(); weather.Start(); Breathe(true);
        var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(240)) { RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(spin, 10); glyph.BeginAnimation(RotateTransform.AngleProperty, spin);
    }
    void Sleep()
    {
        tick.Stop(); weather.Stop(); Breathe(false);
        sec.BeginAnimation(RotateTransform.AngleProperty, null); glyph.BeginAnimation(RotateTransform.AngleProperty, null);
        Hide();
    }
    public void Game(bool running) { asleep = running; Apply(); }

    int lastMinute = -1;
    void Tick()
    {
        var n = DateTime.Now;
        hour.Angle = (n.Hour % 12 + n.Minute / 60.0 + n.Second / 3600.0) * 30;
        min.Angle = (n.Minute + n.Second / 60.0) * 6;
        if (n.Minute != lastMinute)
        {
            lastMinute = n.Minute;
            date.Text = n.ToString("ddd d");
            Sky(n);
            // the sweep: one smooth turn a minute on the GPU, re-synced to the real second every minute
            double from = (n.Second + n.Millisecond / 1000.0) * 6;
            var sweep = new DoubleAnimation(from, from + 360, TimeSpan.FromSeconds(60)) { RepeatBehavior = RepeatBehavior.Forever };
            Timeline.SetDesiredFrameRate(sweep, 24);
            sec.BeginAnimation(RotateTransform.AngleProperty, sweep);
        }
    }

    // the disc: -90° at sunrise (sun at the arch's left edge), 0° at midday, 90° at sunset, then the moon crosses the
    // night from 90° to 270°; the sky behind follows cos(angle): 1 at midday, 0 at sunrise and sunset, -1 at midnight
    static readonly (double s, Color top, Color hor)[] SkyStops =
    [
        (-1, Color.FromRgb(6, 12, 30), Color.FromRgb(16, 24, 54)), (-.3, Color.FromRgb(8, 14, 36), Color.FromRgb(22, 30, 64)),
        (-.08, Color.FromRgb(34, 36, 84), Color.FromRgb(150, 84, 100)), (0, Color.FromRgb(70, 86, 150), Color.FromRgb(236, 150, 78)),
        (.22, Color.FromRgb(66, 132, 196), Color.FromRgb(214, 196, 150)), (1, Color.FromRgb(64, 140, 208), Color.FromRgb(150, 204, 232)),
    ];
    void Sky(DateTime n)
    {
        if (n < rises[0] || n >= sets[2]) // offline past the fetched days: fall back to a plain 7-to-5 day around today
        {
            rises = [.. Enumerable.Range(-1, 3).Select(d => n.Date.AddDays(d).AddHours(6.92))];
            sets = [.. Enumerable.Range(-1, 3).Select(d => n.Date.AddDays(d).AddHours(16.92))];
        }
        double angle = 0; bool day = true; DateTime? upcoming = null;
        for (int i = 0; i < 3; i++)
        {
            if (n >= rises[i] && n < sets[i]) { angle = -90 + 180 * (n - rises[i]) / (sets[i] - rises[i]); upcoming = sets[i]; break; }
            if (i < 2 && n >= sets[i] && n < rises[i + 1]) { angle = 90 + 180 * (n - sets[i]) / (rises[i + 1] - sets[i]); day = false; upcoming = rises[i + 1]; break; }
        }
        disc.Angle = angle;
        double s = Math.Cos(angle * Math.PI / 180);
        for (int i = 1; i < SkyStops.Length; i++)
        {
            var (s0, t0, h0) = SkyStops[i - 1]; var (s1, t1, h1) = SkyStops[i];
            if (s > s1) continue;
            double k = (s - s0) / (s1 - s0);
            Color Mix(Color a, Color b) => Color.FromRgb((byte)Math.Round(a.R + (b.R - a.R) * k), (byte)Math.Round(a.G + (b.G - a.G) * k), (byte)Math.Round(a.B + (b.B - a.B) * k));
            sky.GradientStops = [new(Mix(t0, t1), 0), new(Mix(h0, h1), .92)];
            break;
        }
        stars.Opacity = Math.Clamp(-s * 4 - .3, 0, 1);
        next.Text = upcoming is { } u ? (day ? "Sets " : "Rises ") + u.ToString("h:mm tt") : "";
    }
    void Fade(double to) => nextBox.BeginAnimation(OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(250)));

    static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
    /// <summary>Toronto's weather as the clock last read it ("7° · clear"), or null before the first read.</summary>
    internal static string? WeatherLine;
    async Task Weather()
    {
        try
        {
            var j = JsonDocument.Parse(await http.GetStringAsync("https://api.open-meteo.com/v1/forecast?latitude=43.65&longitude=-79.38&current=temperature_2m,weather_code&daily=sunrise,sunset&past_days=1&forecast_days=2&timezone=America%2FToronto"));
            var daily = j.RootElement.GetProperty("daily");
            DateTime[] Times(string k) => [.. daily.GetProperty(k).EnumerateArray().Select(t => DateTime.Parse(t.GetString()!))];
            var (r, st) = (Times("sunrise"), Times("sunset"));
            if (r.Length == 3 && st.Length == 3) await Dispatcher.InvokeAsync(() => { rises = r; sets = st; Sky(DateTime.Now); });
            var cur = j.RootElement.GetProperty("current");
            int code = cur.GetProperty("weather_code").GetInt32();
            var temp = $"{Math.Round(cur.GetProperty("temperature_2m").GetDouble())}°";
            string icon = code == 0 ? "sun" : code <= 2 ? "partly" : code == 3 ? "cloud" : code <= 48 ? "fog" : code <= 67 || (code >= 80 && code <= 82) ? "rain" : code <= 77 || code == 85 || code == 86 ? "snow" : "storm";
            // the same weather, in words, for our Start's time (sheet 18 idea 2)
            WeatherLine = $"{temp} · " + icon switch { "sun" => "clear", "partly" => "partly cloudy", "cloud" => "cloudy", "fog" => "fog", "rain" => "rain", "snow" => "snow", _ => "storms" };
            // the first fetch starts before the app's dispatcher runs, so hand the result back to the window's thread
            await Dispatcher.InvokeAsync(() => { wxText.Text = temp; wxIcon.Source = Img(Res + $@"Images\wx_{icon}.png"); });
        }
        catch (Exception e) { Program.Log("weather: " + e.Message); await Dispatcher.InvokeAsync(() => { if (string.IsNullOrEmpty(wxText.Text)) wxText.Text = "--°"; }); }
    }

    // ---- live on the desktop: under every window, like the other widgets
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    void ToBottom() => SetWindowPos(me, (IntPtr)1, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
    [StructLayout(LayoutKind.Sequential)] struct WINDOWPOS { public IntPtr hwnd, after; public int x, y, cx, cy; public uint flags; }
    IntPtr KeepAtBottom(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == 0x0046 /*WM_WINDOWPOSCHANGING*/)
        {
            var p = Marshal.PtrToStructure<WINDOWPOS>(l); p.after = (IntPtr)1; p.flags &= ~0x0004u; Marshal.StructureToPtr(p, l, false);
        }
        return IntPtr.Zero;
    }
}
