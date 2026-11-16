using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QBLauncherPro.Services;

public static class IconHelper
{
    private static readonly Dictionary<string, string> PathMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["antigravity"] = "M12 2L20 19L12 15L4 19L12 2Z M12 9.5A1.5 1.5 0 1 0 12 12.5A1.5 1.5 0 1 0 12 9.5Z",
        ["powershell"] = "M3 5C3 3.9 3.9 3 5 3H19C20.1 3 21 3.9 21 5V19C21 20.1 20.1 21 19 21H5C3.9 21 3 20.1 3 19V5ZM7 9L11.5 12L7 15V9ZM12 15H17",
        ["terminal"] = "M4 5H20V19H4V5ZM7.5 10L10.5 12.5L7.5 15M12.5 15H16.5",
        ["folder"] = "M3 7C3 5.9 3.9 5 5 5H9.5L11.5 7H19C20.1 7 21 7.9 21 9V17C21 18.1 20.1 19 19 19H5C3.9 19 3 18.1 3 17V7Z",
        ["file"] = "M14 2H6C4.9 2 4 2.9 4 4V20C4 21.1 4.9 22 6 22H18C19.1 22 20 21.1 20 20V8L14 2ZM14 2V8H20",
        ["globe"] = "M12 2A10 10 0 1 0 12 22A10 10 0 1 0 12 2ZM2 12H22M12 2C14.5 5 16 8.3 16 12C16 15.7 14.5 19 12 22C9.5 19 8 15.7 8 12C8 8.3 9.5 5 12 2Z",
        ["app"] = "M4 6C4 4.9 4.9 4 6 4H18C19.1 4 20 4.9 20 6V18C20 19.1 19.1 20 18 20H6C4.9 20 4 19.1 4 18V6ZM9 9H15V15H9V9Z",
        ["cpu"] = "M6 6H18V18H6V6ZM9 2V6M15 2V6M9 18V22M15 18V22M2 9H6M2 15H6M18 9H22M18 15H22",
        ["calculator"] = "M6 3H18C19.1 3 20 3.9 20 5V19C20 20.1 19.1 21 18 21H6C4.9 21 4 20.1 4 19V5C4 3.9 4.9 3 6 3ZM7 7H17V10H7V7ZM8 13H9.5M11.2 13H12.8M14.5 13H16M8 16.5H9.5M11.2 16.5H12.8M14.5 16.5H16",
        ["bolt"] = "M13 2L4 14H11L10 22L20 10H13L13 2Z"
    };

    private static readonly HashSet<string> ValidIconKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "antigravity", "powershell", "terminal", "cpu", "folder", "globe", "calculator", "app", "bolt", "file", "brand"
    };

    private static readonly Dictionary<string, BitmapImage> ImageCache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource GetIconImageSource(string? iconKey)
    {
        var key = !string.IsNullOrWhiteSpace(iconKey) && ValidIconKeys.Contains(iconKey.Trim())
            ? iconKey.Trim().ToLowerInvariant()
            : "app";

        if (ImageCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var uri = new Uri($"pack://application:,,,/Assets/Icons/{key}.png", UriKind.Absolute);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = uri;
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        ImageCache[key] = bmp;
        return bmp;
    }

    public static Geometry GetGeometry(string? iconKey)
    {
        if (!string.IsNullOrWhiteSpace(iconKey) && PathMap.TryGetValue(iconKey, out var pathData))
        {
            return Geometry.Parse(pathData);
        }
        return Geometry.Parse(PathMap["app"]);
    }

    public static string CategoryLabel(string? cat) => cat switch
    {
        "app" => "Uygulama",
        "file" => "Dosya / Klasör",
        "web" => "Web Sitesi",
        "system" => "PowerShell / Sistem",
        _ => "Uygulama"
    };
}
