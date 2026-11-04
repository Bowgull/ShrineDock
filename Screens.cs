using System.Runtime.InteropServices;
using System.Windows;

namespace ShrineDock;

// The screens of the Shadow PC, read live (screen 2 comes and goes, and Shadow can move it on every reconnect),
// plus the guard that keeps the Acer (monitor BLD06AB, his second screen) on the left of the main one.
static class Screens
{
    public record Screen(string Device, Rect Area, bool Primary);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MONITORINFOEX { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice; }
    delegate bool MonitorProc(IntPtr mon, IntPtr hdc, ref RECT r, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorProc proc, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFOEX info);

    /// <summary>Every connected screen, in pixels (his screens run at 100%, so pixels are WPF units too).</summary>
    public static List<Screen> All()
    {
        var list = new List<Screen>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr m, IntPtr _, ref RECT __, IntPtr ___) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(m, ref mi))
                list.Add(new(mi.szDevice, new Rect(mi.rcMonitor.L, mi.rcMonitor.T, mi.rcMonitor.R - mi.rcMonitor.L, mi.rcMonitor.B - mi.rcMonitor.T), (mi.dwFlags & 1) != 0));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT p, uint flags);

    /// <summary>The work area of the screen under a point (pixels; his screens run at 100%), for popups that must
    /// open on the screen they belong to. Works for the Acer's negative x.</summary>
    public static Rect WorkAreaAt(Point p)
    {
        var m = MonitorFromPoint(new POINT { X = (int)Math.Round(p.X), Y = (int)Math.Round(p.Y) }, 2);
        var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        if (m == IntPtr.Zero || !GetMonitorInfo(m, ref mi)) return SystemParameters.WorkArea;
        return new Rect(mi.rcWork.L, mi.rcWork.T, mi.rcWork.R - mi.rcWork.L, mi.rcWork.B - mi.rcWork.T);
    }

    // ---- keep the Acer on the left
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency, dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplayDevices(string? device, uint index, ref DISPLAY_DEVICE dd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int ChangeDisplaySettingsEx(string? device, ref DEVMODE dm, IntPtr hwnd, uint flags, IntPtr param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int ChangeDisplaySettingsEx(string? device, IntPtr dm, IntPtr hwnd, uint flags, IntPtr param);
    const string Acer = "BLD06AB";
    static bool seenLeft; // log "already left" once, not on every display change

    /// <summary>If the Acer is connected and not left of the main screen, move it to sit flush on the left (its top kept).
    /// Keyed on the monitor's ID, not the DISPLAY number, which Shadow changes. Never touches the main screen.
    /// Returns true when it moved it (a display change follows).</summary>
    /// <summary>"BLD06AB" from MONITOR\BLD06AB\{...}: the monitor's own ID, which stays put when Shadow renumbers screens.</summary>
    public static string MonitorId(string device)
    {
        var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        if (!EnumDisplayDevices(device, 0, ref dd, 0) || string.IsNullOrEmpty(dd.DeviceID)) return device;
        var parts = dd.DeviceID.Split('\\');
        return parts.Length > 1 ? parts[1] : dd.DeviceID;
    }

    public static bool KeepAcerLeft()
    {
        for (uint i = 0; ; i++)
        {
            var ad = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            if (!EnumDisplayDevices(null, i, ref ad, 0)) return false;
            if ((ad.StateFlags & 1) == 0 || (ad.StateFlags & 4) != 0) continue; // attached to the desktop, and not the primary
            var mon = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            if (!EnumDisplayDevices(ad.DeviceName, 0, ref mon, 0) || !(mon.DeviceID ?? "").Contains(Acer, StringComparison.OrdinalIgnoreCase)) continue;
            var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
            if (!EnumDisplaySettings(ad.DeviceName, -1 /*ENUM_CURRENT_SETTINGS*/, ref dm)) return false;
            if (dm.dmPositionX + dm.dmPelsWidth <= 0)
            {
                if (!seenLeft) Program.Log($"screens: the Acer ({ad.DeviceName}) is on the left at x={dm.dmPositionX}, y={dm.dmPositionY}");
                seenLeft = true; return false;
            }
            seenLeft = false;
            int fromX = dm.dmPositionX;
            dm.dmPositionX = -dm.dmPelsWidth; dm.dmFields = 0x20; // DM_POSITION, top kept
            int r = ChangeDisplaySettingsEx(ad.DeviceName, ref dm, IntPtr.Zero, 0x01 /*UPDATEREGISTRY*/ | 0x10000000 /*NORESET*/, IntPtr.Zero);
            int a = ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
            Program.Log($"screens: the Acer ({ad.DeviceName}) was at x={fromX}; moved it to x={dm.dmPositionX}, y={dm.dmPositionY} (results {r}/{a})");
            return r == 0 && a == 0;
        }
    }
}
