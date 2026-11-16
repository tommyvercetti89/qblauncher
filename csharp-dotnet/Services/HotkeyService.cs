using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QBLauncherPro.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 9001;
    private const int WmHotkey = 0x0312;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _registered;
    private Action? _onTriggered;

    public string LastError { get; private set; } = string.Empty;
    public bool IsRunning => _registered;

    public void Attach(Window window, Action onTriggered)
    {
        _onTriggered = onTriggered;
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    public bool UpdateRegistration(bool consent, string modStr, string keyStr)
    {
        Unregister();
        LastError = string.Empty;

        if (!consent || _hwnd == IntPtr.Zero)
        {
            return true;
        }

        if (!TryParseHotkey(modStr, keyStr, out var mods, out var vk, out var parseError))
        {
            LastError = parseError;
            return false;
        }

        if (!RegisterHotKey(_hwnd, HotkeyId, mods | ModNoRepeat, vk))
        {
            var errCode = Marshal.GetLastWin32Error();
            LastError = $"Kısayol kaydedilemedi (Win32 hata {errCode}). Başka bir uygulama bu kombinasyonu kullanıyor olabilir.";
            return false;
        }

        _registered = true;
        return true;
    }

    public void Unregister()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            _onTriggered?.Invoke();
        }
        return IntPtr.Zero;
    }

    private static bool TryParseHotkey(string modStr, string keyStr, out uint mods, out uint vk, out string error)
    {
        mods = 0;
        vk = 0;
        error = string.Empty;

        foreach (var rawPart in modStr.ToUpperInvariant().Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (rawPart.Trim())
            {
                case "ALT": mods |= ModAlt; break;
                case "CTRL":
                case "CONTROL": mods |= ModControl; break;
                case "SHIFT": mods |= ModShift; break;
                case "WIN":
                case "SUPER": mods |= ModWin; break;
            }
        }

        if (mods == 0)
        {
            error = "En az bir değiştirici tuş (Alt, Ctrl veya Shift) gereklidir.";
            return false;
        }

        var k = keyStr.Trim().ToUpperInvariant();
        vk = k switch
        {
            "SPACE" => 0x20,
            "TAB" => 0x09,
            "ENTER" => 0x0D,
            "BACKSPACE" => 0x08,
            "DELETE" or "DEL" => 0x2E,
            "PRIOR" or "PAGEUP" => 0x21,
            "NEXT" or "PAGEDOWN" => 0x22,
            "UP" => 0x26,
            "DOWN" => 0x28,
            "LEFT" => 0x25,
            "RIGHT" => 0x27,
            "INSERT" => 0x2D,
            "HOME" => 0x24,
            "END" => 0x23,
            "OEM_3" or "OEM3" or "`" => 0xC0,
            "OEM_PLUS" or "OEMPLUS" => 0xBB,
            "OEM_COMMA" or "OEMCOMMA" => 0xBC,
            "OEM_MINUS" or "OEMMINUS" => 0xBD,
            "OEM_PERIOD" or "OEMPERIOD" => 0xBE,
            "OEM_1" or "OEM1" => 0xBA,
            "OEM_2" or "OEM2" => 0xBF,
            "OEM_OPENBRACKETS" or "OEMOPENBRACKETS" => 0xDB,
            "OEM_5" or "OEM5" => 0xDC,
            "OEM_6" or "OEM6" => 0xDD,
            "OEM_7" or "OEM7" => 0xDE,
            "OEM_102" or "OEM102" => 0xE2,
            "F1" => 0x70,
            "F2" => 0x71,
            "F3" => 0x72,
            "F4" => 0x73,
            "F5" => 0x74,
            "F6" => 0x75,
            "F7" => 0x76,
            "F8" => 0x77,
            "F9" => 0x78,
            "F10" => 0x79,
            "F11" => 0x7A,
            "F12" => 0x7B,
            _ => 0
        };

        if (vk != 0) return true;

        if (k.Length == 2 && k[0] == 'D' && k[1] >= '0' && k[1] <= '9')
        {
            vk = k[1];
            return true;
        }

        if (k.StartsWith("NUMPAD", StringComparison.Ordinal) && k.Length == 7 && k[6] >= '0' && k[6] <= '9')
        {
            vk = (uint)(0x60 + (k[6] - '0'));
            return true;
        }

        if (k.Length == 1 && ((k[0] >= 'A' && k[0] <= 'Z') || (k[0] >= '0' && k[0] <= '9')))
        {
            vk = k[0];
            return true;
        }

        error = $"Desteklenmeyen tuş: {keyStr}";
        return false;
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WndProc);
        _source = null;
    }
}
