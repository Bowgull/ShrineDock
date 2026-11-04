using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ShrineDock;

record MenuEntry(string Text, Action Do, bool Warn = false);

// The Shrine right-click menu for the dock and the strip: a slate pane with a carved gold-clay rim and corner seals
// (the clock frame's family), Exo 2 text, cyan glow under the hovered row, amber only for closing a window.
// Sized to its contents, kept on screen, one quick fade; it closes on a pick, Esc or a click elsewhere.
class ShrineMenu : Window
{
    static ShrineMenu? current;
    static readonly Color Cyan = Color.FromRgb(0x38, 0xC6, 0xFF), Ice = Color.FromRgb(0xC4, 0xF2, 0xFF), Amber = Color.FromRgb(0xEE, 0xA0, 0x40);
    static readonly FontFamily Font = new("Exo 2, Segoe UI");

    /// <param name="at">where the click was, in screen DIPs</param>
    /// <param name="upward">open above the point (the dock) instead of below it (the strip)</param>
    public static void Open(string title, ImageSource? icon, IList<MenuEntry?> entries, Point at, bool upward)
    {
        current?.Close();
        current = new ShrineMenu(title, icon, entries);
        current.Closed += (s, _) => { if (current == s) current = null; };
        current.Place(at, upward);
    }

    /// <summary>His rule (2026-11-03, he unpinned by accident once): a step that is hard to undo asks first, in the menu's
    /// own look, as Restart and Shut down do: the question, the amber yes, and Cancel.</summary>
    public static void Confirm(string question, ImageSource? icon, string yes, Action act, Point at, bool upward) =>
        Open(question, icon, [new(yes, act, Warn: true), new("Cancel", () => { })], at, upward);

    ShrineMenu(string title, ImageSource? icon, IList<MenuEntry?> entries)
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; SizeToContent = SizeToContent.WidthAndHeight; Title = "Shrine menu"; Opacity = 0;

        var list = new StackPanel { Margin = new Thickness(6, 4, 6, 8), MinWidth = 190 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 10, 14, 6) };
        if (icon != null) head.Children.Add(new Image { Source = icon, Width = 20, Height = 20, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        head.Children.Add(new TextBlock
        {
            Text = title, FontFamily = Font, FontStyle = FontStyles.Italic, FontWeight = FontWeights.SemiBold, FontSize = 15, Foreground = new SolidColorBrush(Ice),
            MaxWidth = 280, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
            Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 8, ShadowDepth = 0, Opacity = .7 },
        });
        list.Children.Add(head);
        list.Children.Add(Rule());
        foreach (var e in entries) list.Children.Add(e == null ? Rule() : Row(e));
        Content = Pane(list);

        bool closing = false;
        Closing += (_, _) => closing = true;
        Deactivated += (_, _) => { if (!closing) Close(); }; // a pick closes it first, and the deactivation that follows must not close it twice
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    /// <summary>The pane around a menu's contents (also the strip's volume drop-down): slate with grain-dark depth, a
    /// carved clay rim (lit top, shaded bottom), a cyan inner line and seals in the corners.</summary>
    internal static Grid Pane(UIElement list)
    {
        var pane = new Border
        {
            CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(2.5), Child = list, Margin = new Thickness(14),
            Background = new LinearGradientBrush(Color.FromArgb(0xF6, 0x12, 0x2C, 0x42), Color.FromArgb(0xF6, 0x06, 0x10, 0x1A), 90),
            BorderBrush = new LinearGradientBrush(new GradientStopCollection { new(Color.FromRgb(0xF0, 0xCE, 0x88), 0), new(Color.FromRgb(0xB8, 0x92, 0x4C), .45), new(Color.FromRgb(0x6E, 0x52, 0x26), 1) }, 90),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 18, ShadowDepth = 5, Direction = 270, Opacity = .6 },
        };
        var inner = new Border { CornerRadius = new CornerRadius(15.5), BorderThickness = new Thickness(1), Margin = new Thickness(16.5), IsHitTestVisible = false,
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x48, 0x38, 0xC6, 0xFF)), Effect = new BlurEffect { Radius = 1.2 } };
        var grid = new Grid();
        grid.Children.Add(pane); grid.Children.Add(inner);
        var seal = new BitmapImage(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "art", "menu-seal.png")));
        foreach (var (h, v) in new[] { (HorizontalAlignment.Left, VerticalAlignment.Top), (HorizontalAlignment.Right, VerticalAlignment.Top), (HorizontalAlignment.Left, VerticalAlignment.Bottom), (HorizontalAlignment.Right, VerticalAlignment.Bottom) })
            grid.Children.Add(new Image { Source = seal, Width = 20, Height = 20, HorizontalAlignment = h, VerticalAlignment = v, Margin = new Thickness(9.3), IsHitTestVisible = false });
        // 2026-11-04 (soft edges): rounder corners (18), and the seals sit ON the curve, so it no longer reads as a square box
        return grid;
    }

    void Place(Point at, bool upward)
    {
        Left = at.X; Top = at.Y; // open on the clicked screen from the start (it's still invisible), then fit it there
        FullScreen.GiveBackOnClose(this);   // closing it hands the keys back (a game on the other screen keeps playing)
        Show(); Activate(); UpdateLayout();
        var wa = Screens.WorkAreaAt(at); // the screen that was clicked, never just the main one
        double w = ActualWidth, h = ActualHeight;
        double x = Math.Clamp(at.X - 24, wa.Left + 4, wa.Right - w - 4);
        double y = upward ? at.Y - h + 10 : at.Y - 10;
        Left = x; Top = Math.Clamp(y, wa.Top + 4, wa.Bottom - h - 4);
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(90)));
    }

    internal static UIElement Rule() => new Rectangle { Height = 1, Margin = new Thickness(10, 4, 10, 4),
        Fill = new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(0, 0xB8, 0x92, 0x4C), 0), new(Color.FromArgb(0xC0, 0xDD, 0xB8, 0x70), .5), new(Color.FromArgb(0, 0xB8, 0x92, 0x4C), 1) }, 0) };

    UIElement Row(MenuEntry e)
    {
        var col = e.Warn ? Amber : Ice;
        var bar = new Rectangle { Width = 3, RadiusX = 1.5, RadiusY = 1.5, Margin = new Thickness(0, 5, 8, 5), Fill = new SolidColorBrush(e.Warn ? Amber : Cyan), Opacity = 0,
            Effect = new DropShadowEffect { Color = e.Warn ? Amber : Cyan, BlurRadius = 8, ShadowDepth = 0 } };
        var text = new TextBlock { Text = e.Text, FontFamily = Font, FontSize = 14, Foreground = new SolidColorBrush(col), VerticalAlignment = VerticalAlignment.Center };
        var row = new Border { CornerRadius = new CornerRadius(7), Padding = new Thickness(6, 6, 16, 7), Background = Brushes.Transparent, Cursor = Cursors.Hand,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { bar, text } } };
        row.MouseEnter += (_, _) =>
        {
            row.Background = new SolidColorBrush(e.Warn ? Color.FromArgb(0x38, 0xEE, 0xA0, 0x40) : Color.FromArgb(0x40, 0x38, 0xC6, 0xFF)); bar.Opacity = 1;
            text.Effect = new DropShadowEffect { Color = e.Warn ? Amber : Cyan, BlurRadius = 8, ShadowDepth = 0, Opacity = .8 };
        };
        row.MouseLeave += (_, _) => { row.Background = Brushes.Transparent; bar.Opacity = 0; text.Effect = null; };
        row.MouseLeftButtonUp += (_, _) => { Close(); try { e.Do(); } catch (Exception ex) { Program.Log("menu: " + ex.Message); } };
        return row;
    }
}
