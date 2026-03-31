using System.Runtime.InteropServices;
using ReSwitch.Models;

namespace ReSwitch.Services;

/// <summary>Работа с Win32 ChangeDisplaySettingsEx / EnumDisplaySettings.</summary>
public static class DisplaySettingsService
{
    private const int ENUM_CURRENT_SETTINGS = -1;
    private const uint CDS_UPDATEREGISTRY = 0x00000001;
    private const uint CDS_TEST = 0x00000002;
    private const int DISP_CHANGE_SUCCESSFUL = 0;
    private const int DISP_CHANGE_RESTART = 1;
    private const int DISP_CHANGE_BADMODE = -2;

    private const uint DM_BITSPERPEL = 0x00040000;
    private const uint DM_PELSWIDTH = 0x00080000;
    private const uint DM_PELSHEIGHT = 0x00100000;
    private const uint DM_DISPLAYFREQUENCY = 0x00400000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;

        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;

        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern int EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode devMode);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern int ChangeDisplaySettingsEx(string? deviceName, ref DevMode devMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

    public static int DevModeStructSize => Marshal.SizeOf<DevMode>();

    public static bool TryGetCurrentMode(out DisplayProfile profile, out DevMode raw)
    {
        raw = default;
        raw.dmDeviceName = new string('\0', 32);
        raw.dmFormName = new string('\0', 32);
        raw.dmSize = (ushort)DevModeStructSize;

        if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref raw) == 0)
        {
            profile = new DisplayProfile();
            return false;
        }

        profile = new DisplayProfile
        {
            Name = "",
            Width = (int)raw.dmPelsWidth,
            Height = (int)raw.dmPelsHeight,
            RefreshRate = (int)raw.dmDisplayFrequency,
            BitsPerPixel = (int)raw.dmBitsPerPel
        };
        return true;
    }

    public static bool ProfileMatchesCurrent(DisplayProfile p, DisplayProfile current)
    {
        if (p.Width != current.Width || p.Height != current.Height || p.BitsPerPixel != current.BitsPerPixel)
            return false;
        var d = Math.Abs(p.RefreshRate - current.RefreshRate);
        return d <= 1;
    }

    /// <summary>Проверка режима без применения.</summary>
    public static bool TryTestMode(DisplayProfile profile, out string? error)
    {
        error = null;
        if (!TryGetCurrentMode(out _, out var currentRaw))
        {
            error = LocalizationService.T("Errors.ReadCurrentFailed");
            return false;
        }

        var dm = BuildDevMode(profile, currentRaw);
        var r = ChangeDisplaySettingsEx(null, ref dm, IntPtr.Zero, CDS_TEST, IntPtr.Zero);
        if (r == DISP_CHANGE_SUCCESSFUL || r == DISP_CHANGE_RESTART)
            return true;
        if (r == DISP_CHANGE_BADMODE)
        {
            error = LocalizationService.T("Errors.ModeNotSupported");
            return false;
        }

        error = LocalizationService.T("Errors.ModeTestCode", r);
        return false;
    }

    public static bool TryApplyMode(DisplayProfile profile, out string? error)
    {
        error = null;
        if (!TryGetCurrentMode(out _, out var currentRaw))
        {
            error = LocalizationService.T("Errors.ReadCurrentFailed");
            return false;
        }

        var dm = BuildDevMode(profile, currentRaw);
        var r = ChangeDisplaySettingsEx(null, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
        if (r == DISP_CHANGE_SUCCESSFUL)
            return true;
        if (r == DISP_CHANGE_RESTART)
        {
            error = LocalizationService.T("Errors.RestartRequired");
            return false;
        }

        error = LocalizationService.T("Errors.ApplyFailed", r);
        return false;
    }

    public static bool TryApplyRaw(in DevMode raw, out string? error)
    {
        error = null;
        var dm = raw;
        dm.dmSize = (ushort)DevModeStructSize;
        var r = ChangeDisplaySettingsEx(null, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
        if (r == DISP_CHANGE_SUCCESSFUL)
            return true;
        if (r == DISP_CHANGE_RESTART)
        {
            error = LocalizationService.T("Errors.RestartRequiredShort");
            return false;
        }

        error = LocalizationService.T("Errors.RestoreFailed", r);
        return false;
    }

    private static DevMode BuildDevMode(DisplayProfile profile, DevMode template)
    {
        var dm = template;
        dm.dmFields |= DM_PELSWIDTH | DM_PELSHEIGHT | DM_BITSPERPEL | DM_DISPLAYFREQUENCY;
        dm.dmPelsWidth = (uint)profile.Width;
        dm.dmPelsHeight = (uint)profile.Height;
        dm.dmBitsPerPel = (uint)profile.BitsPerPixel;
        if (profile.RefreshRate > 0)
        {
            dm.dmFields |= DM_DISPLAYFREQUENCY;
            dm.dmDisplayFrequency = (uint)profile.RefreshRate;
        }
        else
        {
            dm.dmFields &= ~DM_DISPLAYFREQUENCY;
        }

        return dm;
    }
}
