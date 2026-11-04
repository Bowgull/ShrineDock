using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ShrineDock;

// The Shrine mixer's ears (sheet 17 v2, his "Yes, build it" 2026-11-02): Windows' own per-app volume (CoreAudio audio
// sessions on the default speaker, the same sliders as Windows' volume mixer), the whole-PC volume (the endpoint), and
// their live peak meters. Nothing runs while the mixer is closed except one Windows callback that puts a remembered
// level back when an app starts playing (Levels.Watch).
// Apps are grouped by the program they belong to: Chromium apps (Samara, Discord, Spotify) play from a helper process,
// so a session's process is walked up to its first parent of the same program.
sealed class AudioMixer : IDisposable
{
    public sealed class App
    {
        public required string Key, Name;
        public string? Exe;
        public readonly List<(ISimpleAudioVolume vol, IAudioMeterInformation? meter, IAudioSessionControl2 ctl)> Sessions = [];
        public float Level { get { try { Sessions[0].vol.GetMasterVolume(out var v); return v; } catch { return 0; } } }
        public bool Mute { get { try { Sessions[0].vol.GetMute(out var m); return m != 0; } catch { return false; } } }
        public void SetLevel(float v) { var g = Guid.Empty; foreach (var s in Sessions) try { s.vol.SetMasterVolume(Math.Clamp(v, 0, 1), ref g); } catch { } Levels.Remember(Key, Math.Clamp(v, 0, 1), Mute); }
        public void SetMute(bool m) { var g = Guid.Empty; foreach (var s in Sessions) try { s.vol.SetMute(m ? 1 : 0, ref g); } catch { } Levels.Remember(Key, Level, m); }
        public float Peak { get { float p = 0; foreach (var s in Sessions) try { if (s.meter != null && s.meter.GetPeakValue(out var v) == 0) p = Math.Max(p, v); } catch { } return p; } }
        public bool Playing { get { foreach (var s in Sessions) try { if (s.ctl.GetState(out var st) == 0 && st == 1) return true; } catch { } return false; } }
    }

    IMMDeviceEnumerator? en; IMMDevice? dev; IAudioSessionManager2? mgr; IAudioEndpointVolume? ep; IAudioMeterInformation? epMeter;
    public string SpeakerName { get; private set; } = "This PC";
    public readonly Dictionary<string, App> Apps = [];

    public AudioMixer()
    {
        try
        {
            en = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            if (en.GetDefaultAudioEndpoint(0, 1, out dev) != 0) return;
            mgr = Activate<IAudioSessionManager2>(dev); ep = Activate<IAudioEndpointVolume>(dev); epMeter = Activate<IAudioMeterInformation>(dev);
            SpeakerName = FriendlyName(dev) ?? SpeakerName;
        }
        catch (Exception e) { Program.Log("mixer: " + e.Message); }
    }
    static T? Activate<T>(IMMDevice d) where T : class { var iid = typeof(T).GUID; return d.Activate(ref iid, 0x17, IntPtr.Zero, out var o) == 0 ? o as T : null; }

    // the whole PC
    public float Master { get { try { ep!.GetMasterVolumeLevelScalar(out var v); return v; } catch { return 0; } } set { var g = Guid.Empty; try { ep?.SetMasterVolumeLevelScalar(Math.Clamp(value, 0, 1), ref g); } catch { } } }
    public bool MasterMute { get { try { ep!.GetMute(out var m); return m != 0; } catch { return false; } } set { var g = Guid.Empty; try { ep?.SetMute(value ? 1 : 0, ref g); } catch { } } }
    public float MasterPeak { get { try { return epMeter != null && epMeter.GetPeakValue(out var v) == 0 ? v : 0; } catch { return 0; } } }

    /// <summary>Reads the apps with a session on the speaker now (about once a second while the mixer is open).</summary>
    public void Refresh()
    {
        if (mgr == null) return;
        foreach (var a in Apps.Values) Release(a);
        var found = new Dictionary<string, App>();
        var tree = ProcessTree();
        IAudioSessionEnumerator? list = null;
        try
        {
            if (mgr.GetSessionEnumerator(out list) != 0 || list == null) return;
            list.GetCount(out var n);
            for (int i = 0; i < n; i++)
            {
                if (list.GetSession(i, out var o) != 0 || o is not IAudioSessionControl2 ctl) continue;
                if (ctl.GetState(out var st) != 0 || st == 2) { Marshal.ReleaseComObject(o); continue; }   // expired
                ctl.GetProcessId(out var pid);
                var (key, name, exe) = Identify(pid, tree);
                if (key == "shadowstreamer") { Marshal.ReleaseComObject(o); continue; }  // Shadow sending the sound to the Mac: it copies every app
                if (!found.TryGetValue(key, out var app)) found[key] = app = Apps.TryGetValue(key, out var old) ? old : new App { Key = key, Name = name, Exe = exe };
                app.Sessions.Add(((ISimpleAudioVolume)o, o as IAudioMeterInformation, ctl));
            }
        }
        catch (Exception e) { Program.Log("mixer: " + e.Message); }
        finally { if (list != null) Marshal.ReleaseComObject(list); }
        Apps.Clear(); foreach (var (k, a) in found) Apps[k] = a;
    }
    static void Release(App a) { foreach (var s in a.Sessions) try { Marshal.ReleaseComObject(s.ctl); } catch { } a.Sessions.Clear(); }

    // ---- which program a session belongs to: its key (also the remembered level's key), shown name and program file
    internal static (string key, string name, string? exe) Identify(uint pid, Dictionary<uint, (uint parent, string exe)>? tree = null)
    {
        if (pid == 0) return ("windows", "Windows sounds", null);
        tree ??= ProcessTree();
        uint root = pid;
        for (int guard = 0; guard < 16 && tree.TryGetValue(root, out var me) && tree.TryGetValue(me.parent, out var up) && up.exe.Equals(me.exe, StringComparison.OrdinalIgnoreCase); guard++) root = me.parent;
        var exe = StripWindow.ExePath(root) ?? StripWindow.ExePath(pid);
        var file = exe != null ? Path.GetFileNameWithoutExtension(exe) : tree.TryGetValue(pid, out var t) ? Path.GetFileNameWithoutExtension(t.exe) : "app";
        // Samara is Aang's Electron (castlabs build in AangApp)
        if (exe != null && file.Equals("electron", StringComparison.OrdinalIgnoreCase) && exe.Contains(@"\AangApp\", StringComparison.OrdinalIgnoreCase)) return ("samara", "Samara", exe);
        if (file.Equals("WowB", StringComparison.OrdinalIgnoreCase) || file.Equals("Wow", StringComparison.OrdinalIgnoreCase)) return ("wow", "WoW", exe);
        string name = file;
        try { if (exe != null) { var d = FileVersionInfo.GetVersionInfo(exe).FileDescription?.Trim(); if (!string.IsNullOrEmpty(d) && d.Length <= 28) name = d; } } catch { }
        return (file.ToLowerInvariant(), name, exe);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PROCESSENTRY32 { public int dwSize, cntUsage; public uint th32ProcessID; public IntPtr th32DefaultHeapID; public uint th32ModuleID, cntThreads, th32ParentProcessID; public int pcPriClassBase; public uint dwFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile; }
    [DllImport("kernel32.dll")] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32FirstW(IntPtr h, ref PROCESSENTRY32 e);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32NextW(IntPtr h, ref PROCESSENTRY32 e);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    static Dictionary<uint, (uint, string)> ProcessTree()
    {
        var d = new Dictionary<uint, (uint, string)>();
        var h = CreateToolhelp32Snapshot(2, 0); if (h == new IntPtr(-1)) return d;
        try
        {
            var e = new PROCESSENTRY32 { dwSize = Marshal.SizeOf<PROCESSENTRY32>() };
            if (Process32FirstW(h, ref e)) do d[e.th32ProcessID] = (e.th32ParentProcessID, e.szExeFile); while (Process32NextW(h, ref e));
        }
        finally { CloseHandle(h); }
        return d;
    }

    // the speaker's name as Windows shows it ("Speakers (Shadow ...)") from the device's property store
    [StructLayout(LayoutKind.Sequential)] internal struct PROPERTYKEY { public Guid fmtid; public int pid; }
    [StructLayout(LayoutKind.Sequential)] internal struct PROPVARIANT { public ushort vt; ushort r1, r2, r3; public IntPtr p; IntPtr p2; }
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT pv);
    static string? FriendlyName(IMMDevice d)
    {
        try
        {
            if (d.OpenPropertyStore(0, out var store) != 0) return null;
            var key = new PROPERTYKEY { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };
            try
            {
                if (store.GetValue(ref key, out var v) != 0) return null;
                var s = v.vt == 31 ? Marshal.PtrToStringUni(v.p) : null; PropVariantClear(ref v);
                // "Speakers (Shadow Audio)" reads as "Shadow Audio"
                if (s != null && s.IndexOf('(') is var i and > 0 && s.EndsWith(')')) s = s[(i + 1)..^1];
                s = s?.Replace("High Definition Audio", "HD Audio");
                return s;
            }
            finally { Marshal.ReleaseComObject(store); }
        }
        catch { return null; }
    }

    public void Dispose()
    {
        foreach (var a in Apps.Values) Release(a); Apps.Clear();
        foreach (var o in new object?[] { epMeter, ep, mgr, dev, en }) if (o != null) try { Marshal.ReleaseComObject(o); } catch { }
        epMeter = null; ep = null; mgr = null; dev = null; en = null;
    }

    /// <summary>The whole-PC volume for a quick change (the strip's speaker tile wheel), without holding the mixer open.</summary>
    public static float NudgeMaster(float by)
    {
        using var m = new AudioMixer();
        m.Master = m.Master + by; return m.Master;
    }

    // ---- CoreAudio, in vtable order (only up to the methods used)
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorComObject { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
    }
    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int n);
        [PreserveSig] int GetAt(int i, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT v);
    }
    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr sessionGuid, int flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionGuid, int flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator list);
        [PreserveSig] int RegisterSessionNotification(IAudioSessionNotification n);
        [PreserveSig] int UnregisterSessionNotification(IAudioSessionNotification n);
    }
    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
    }
    [ComImport, Guid("641DD20B-4D41-49CC-ABA3-174B9477BB08"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionNotification
    {
        [PreserveSig] int OnSessionCreated([MarshalAs(UnmanagedType.IUnknown)] object session);
    }
    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName(out IntPtr name);
        [PreserveSig] int SetDisplayName(IntPtr name, IntPtr ctx);
        [PreserveSig] int GetIconPath(out IntPtr path);
        [PreserveSig] int SetIconPath(IntPtr path, IntPtr ctx);
        [PreserveSig] int GetGroupingParam(out Guid param);
        [PreserveSig] int SetGroupingParam(IntPtr param, IntPtr ctx);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr client);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr client);
        [PreserveSig] int GetSessionIdentifier(out IntPtr id);
        [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);
        [PreserveSig] int GetProcessId(out uint pid);
    }
    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid ctx);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute(int mute, ref Guid ctx);
        [PreserveSig] int GetMute(out int mute);
    }
    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float peak);
    }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr n);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr n);
        [PreserveSig] int GetChannelCount(out uint n);
        [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid ctx);
        [PreserveSig] int SetMasterVolumeLevelScalar(float v, ref Guid ctx);
        [PreserveSig] int GetMasterVolumeLevel(out float db);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float v);
        [PreserveSig] int SetChannelVolumeLevel(uint c, float db, ref Guid ctx);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint c, float v, ref Guid ctx);
        [PreserveSig] int GetChannelVolumeLevel(uint c, out float db);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint c, out float v);
        [PreserveSig] int SetMute(int mute, ref Guid ctx);
        [PreserveSig] int GetMute(out int mute);
    }

    // ---- each app's level, remembered (his pick): saved when he changes it, put back when that app starts playing
    internal static class Levels
    {
        static readonly string FilePath = Path.Combine(Path.GetDirectoryName(Settings.Path)!, "levels.json");
        record Saved(float Level, bool Mute);
        static Dictionary<string, Saved>? map;
        static readonly object gate = new();
        static Dictionary<string, Saved> Map() { lock (gate) { if (map != null) return map; try { map = JsonSerializer.Deserialize<Dictionary<string, Saved>>(File.ReadAllText(FilePath)); } catch { } return map ??= []; } }
        public static void Remember(string key, float level, bool mute)
        {
            if (Settings.Load().ForgetLevels) return;
            lock (gate) { Map()[key] = new(level, mute); try { File.WriteAllText(FilePath, JsonSerializer.Serialize(map)); } catch { } }
        }
        static Notify? keep; static IAudioSessionManager2? keepMgr; static IMMDevice? keepDev;
        /// <summary>One CoreAudio callback for the life of the app, on its own MTA thread; costs nothing until a session starts.</summary>
        public static void Watch()
        {
            var t = new Thread(() =>
            {
                try
                {
                    var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                    if (en.GetDefaultAudioEndpoint(0, 1, out keepDev) != 0) return;
                    keepMgr = Activate<IAudioSessionManager2>(keepDev); if (keepMgr == null) return;
                    keepMgr.GetSessionEnumerator(out var list); Marshal.ReleaseComObject(list); // Windows only calls back after one enumeration
                    keep = new Notify(); keepMgr.RegisterSessionNotification(keep);
                    Program.Log("mixer: remembering app levels");
                }
                catch (Exception e) { Program.Log("mixer: watch " + e.Message); }
                Thread.Sleep(Timeout.Infinite);
            }) { IsBackground = true, Name = "Shrine levels" };
            t.SetApartmentState(ApartmentState.MTA); t.Start();
        }
        [ComVisible(true)]
        sealed class Notify : IAudioSessionNotification
        {
            public int OnSessionCreated(object session)
            {
                try
                {
                    if (session is not IAudioSessionControl2 ctl || Settings.Load().ForgetLevels) return 0;
                    ctl.GetProcessId(out var pid);
                    var key = Identify(pid).key;
                    if (!Map().TryGetValue(key, out var s)) return 0;
                    var g = Guid.Empty; var vol = (ISimpleAudioVolume)session;
                    vol.SetMasterVolume(s.Level, ref g); vol.SetMute(s.Mute ? 1 : 0, ref g);
                    Program.Log($"mixer: {key} back to {Math.Round(s.Level * 100)}%{(s.Mute ? " (muted)" : "")}");
                }
                catch { }
                return 0;
            }
        }
    }

    // ---- Samara's music player (2026-11-03, his ask: on the Shadow the player is how the music reaches him, so the mixer
    // shows it, as "Spotify" with Samara's mark, wherever Spotify is playing). Samara writes player-now.json next to
    // player.json while the player is open; the slider goes back as player-ask.json, which Samara reads once and removes.
    internal static class Player
    {
        public sealed record Now(int pid, bool open, string? app, bool spotify, bool playing, float? volume, string? device);
        static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aang");
        static DateTime seen; static Now? last;
        /// <summary>What the player shows now, or null when it is closed (or Samara went away without closing it).</summary>
        public static Now? Read()
        {
            var f = Path.Combine(Dir, "player-now.json");
            try
            {
                var t = File.GetLastWriteTimeUtc(f);
                if (t != seen) { seen = t; last = File.Exists(f) ? JsonSerializer.Deserialize<Now>(File.ReadAllText(f)) : null; }
                // open is enough (his rule: the mixer shows the player); with no song it says so and keeps its place
                if (last is not { open: true }) return null;
                try { using var p = Process.GetProcessById(last.pid); if (p.HasExited) return null; } catch { return null; }
                return last;
            }
            catch { seen = default; return null; }
        }
        public static void SetVolume(float v)
        {
            try
            {
                var tmp = Path.Combine(Dir, "player-ask.tmp");
                File.WriteAllText(tmp, JsonSerializer.Serialize(new { volume = Math.Round(Math.Clamp(v, 0, 1), 2) }));
                File.Move(tmp, Path.Combine(Dir, "player-ask.json"), true);
            }
            catch (Exception e) { Program.Log("mixer: player " + e.Message); }
        }
    }
}
