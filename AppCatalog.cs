using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ShrineDock;

// Every app in his Start list (the shell's AppsFolder: desktop apps and packaged ones like Settings alike), with its
// real icon from the shell. Read on a background STA thread, refreshed each time Start opens, icons cached by ID.
static class AppCatalog
{
    public record Entry(string Name, string Id);

    static List<Entry> apps = [];
    static readonly Dictionary<string, ImageSource?> icons = [];
    static readonly object gate = new();
    static bool reading;
    public static event Action? Changed;

    public static List<Entry> All { get { lock (gate) return apps; } }
    public static Entry? Find(string name) => All.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Re-reads the list (and any icons not cached yet) in the background, then raises Changed on the UI thread.</summary>
    public static void Refresh(Dispatcher ui)
    {
        lock (gate) { if (reading) return; reading = true; }
        var t = new Thread(() =>
        {
            try
            {
                var list = Read();
                foreach (var a in list)
                {
                    bool have; lock (gate) have = icons.ContainsKey(a.Id);
                    if (!have) { var i = Icon(a.Id); lock (gate) icons[a.Id] = i; }
                }
                lock (gate) apps = list;
                ui.BeginInvoke(() => Changed?.Invoke());
            }
            catch (Exception ex) { Program.Log("start: reading the apps failed: " + ex.Message); }
            finally { lock (gate) reading = false; }
        }) { IsBackground = true };
        t.SetApartmentState(ApartmentState.STA); t.Start();
    }
    public static ImageSource? IconOf(string id) { lock (gate) return icons.TryGetValue(id, out var i) ? i : null; }

    static List<Entry> Read()
    {
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
        var list = new List<Entry>();
        foreach (dynamic item in shell.NameSpace("shell:AppsFolder").Items())
        {
            string name = item.Name, id = item.Path;
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id)) list.Add(new(name, id));
        }
        return [.. list.GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)];
    }

    public static void Launch(Entry a)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{a.Id}") { UseShellExecute = true }); }
        catch (Exception ex) { Program.Log($"start: couldn't open {a.Name}: {ex.Message}"); }
    }

    // ---- the shell's own icon for an AppsFolder item, alpha kept
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory { [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm); }
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential)] struct BITMAP { public int type, w, h, stride; public ushort planes, bpp; public IntPtr bits; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
    [DllImport("gdi32.dll")] static extern int GetObject(IntPtr h, int c, ref BITMAP b);

    static ImageSource? Icon(string id)
    {
        try
        {
            SHCreateItemFromParsingName($"shell:AppsFolder\\{id}", IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var f);
            if (f.GetImage(new SIZE { cx = 64, cy = 64 }, 0x1 /*BIGGERSIZEOK*/ | 0x4 /*ICONONLY*/, out var hb) != 0) return null;
            try
            {
                var bm = new BITMAP(); GetObject(hb, Marshal.SizeOf<BITMAP>(), ref bm);
                if (bm.bits == IntPtr.Zero || bm.bpp != 32) return null;
                var buf = new byte[bm.stride * bm.h]; Marshal.Copy(bm.bits, buf, 0, buf.Length);
                // the shell's DIB is bottom-up and premultiplied, which is exactly Pbgra32 once flipped
                var flip = new byte[buf.Length];
                for (int y = 0; y < bm.h; y++) Buffer.BlockCopy(buf, (bm.h - 1 - y) * bm.stride, flip, y * bm.stride, bm.stride);
                var src = BitmapSource.Create(bm.w, bm.h, 96, 96, PixelFormats.Pbgra32, null, flip, bm.stride); src.Freeze();
                return src;
            }
            finally { DeleteObject(hb); }
        }
        catch { return null; }
    }
}
