using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QBLauncherPro.Models;

public sealed class LauncherItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("subtitle")]
    public string Subtitle { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public string Args { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = "app";

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "app";

    [JsonPropertyName("runAsAdmin")]
    public bool RunAsAdmin { get; set; }
}

public sealed class AppConfig
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "dark";

    [JsonPropertyName("hotkeyMod")]
    public string HotkeyMod { get; set; } = "ALT";

    [JsonPropertyName("hotkeyKey")]
    public string HotkeyKey { get; set; } = "SPACE";

    [JsonPropertyName("hotkeyLabel")]
    public string HotkeyLabel { get; set; } = "Alt + Space";

    [JsonPropertyName("listenerConsent")]
    public bool ListenerConsent { get; set; }

    [JsonPropertyName("autoHideOnLaunch")]
    public bool AutoHideOnLaunch { get; set; } = true;

    [JsonPropertyName("quickNumberKeys")]
    public bool QuickNumberKeys { get; set; } = true;

    [JsonPropertyName("enableAnimations")]
    public bool EnableAnimations { get; set; } = true;

    [JsonPropertyName("runOnStartup")]
    public bool RunOnStartup { get; set; }

    [JsonPropertyName("launcherStyle")]
    public string LauncherStyle { get; set; } = "square";

    [JsonPropertyName("items")]
    public List<LauncherItem> Items { get; set; } = new();
}

public static class ConfigStorage
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    private static string GetFallbackConfigPath()
    {
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localApp, "QBLauncherPro", "qblauncher-config.json");
    }

    public static string GetPortableConfigPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "qblauncher-config.json");
    }

    public static AppConfig LoadOrCreate()
    {
        var primaryPath = GetPortableConfigPath();
        var fallbackPath = GetFallbackConfigPath();
        var pathToLoad = File.Exists(primaryPath)
            ? primaryPath
            : (File.Exists(fallbackPath) ? fallbackPath : primaryPath);

        if (File.Exists(pathToLoad))
        {
            try
            {
                var raw = File.ReadAllText(pathToLoad);
                var cfg = JsonSerializer.Deserialize<AppConfig>(raw, JsonOpts);
                if (cfg != null)
                {
                    var validThemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "dark", "light", "lavender", "mint", "sky", "amber"
                    };
                    cfg.Theme = !string.IsNullOrWhiteSpace(cfg.Theme) && validThemes.Contains(cfg.Theme)
                        ? cfg.Theme.ToLowerInvariant()
                        : "dark";
                    if (cfg.LauncherStyle != "square" && cfg.LauncherStyle != "classic")
                        cfg.LauncherStyle = "square";
                    if (string.IsNullOrWhiteSpace(cfg.HotkeyMod)) cfg.HotkeyMod = "ALT";
                    if (string.IsNullOrWhiteSpace(cfg.HotkeyKey)) cfg.HotkeyKey = "SPACE";
                    if (string.IsNullOrWhiteSpace(cfg.HotkeyLabel)) cfg.HotkeyLabel = "Alt + Space";
                    cfg.Items ??= new List<LauncherItem>();
                    if (NormalizePortableTargets(cfg))
                    {
                        Save(cfg);
                    }
                    return cfg;
                }
            }
            catch
            {
                // Fallback to defaults if config file is corrupted
            }
        }

        var def = CreateDefault();
        Save(def);
        return def;
    }

    public static void Save(AppConfig cfg)
    {
        var json = JsonSerializer.Serialize(cfg, JsonOpts);
        try
        {
            WriteFileAtomic(GetPortableConfigPath(), json);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            WriteFileAtomic(GetFallbackConfigPath(), json);
        }
    }

    private static void WriteFileAtomic(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var tmpPath = path + ".tmp";
        File.WriteAllText(tmpPath, content);
        File.Move(tmpPath, path, overwrite: true);
    }

    private static bool NormalizePortableTargets(AppConfig cfg)
    {
        bool changed = false;
        foreach (var item in cfg.Items)
        {
            if (item.Id == null) { item.Id = $"item-{Guid.NewGuid():N}"; changed = true; }
            if (item.Name == null) { item.Name = "Kısayol"; changed = true; }
            if (item.Subtitle == null) { item.Subtitle = string.Empty; changed = true; }
            if (item.Target == null) { item.Target = string.Empty; changed = true; }
            if (item.Args == null) { item.Args = string.Empty; changed = true; }
            if (string.IsNullOrWhiteSpace(item.Category)) { item.Category = "app"; changed = true; }
            if (string.IsNullOrWhiteSpace(item.Icon)) { item.Icon = "app"; changed = true; }

            var t = item.Target.Trim();
            if (string.IsNullOrEmpty(t)) continue;

            if (t.EndsWith(@"\Programs\Antigravity\Antigravity.exe", StringComparison.OrdinalIgnoreCase))
            {
                item.Target = "antigravity";
                changed = true;
            }
            else if (t.StartsWith(@"C:\Users\", StringComparison.OrdinalIgnoreCase) &&
                     t.EndsWith(@"\Desktop", StringComparison.OrdinalIgnoreCase))
            {
                item.Target = @"%USERPROFILE%\Desktop";
                changed = true;
            }
            else if (t.StartsWith(@"C:\Users\", StringComparison.OrdinalIgnoreCase) &&
                     t.EndsWith(@"\Downloads", StringComparison.OrdinalIgnoreCase))
            {
                item.Target = @"%USERPROFILE%\Downloads";
                changed = true;
            }
        }
        return changed;
    }

    private static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            Theme = "dark",
            HotkeyMod = "ALT",
            HotkeyKey = "SPACE",
            HotkeyLabel = "Alt + Space",
            ListenerConsent = false,
            AutoHideOnLaunch = true,
            QuickNumberKeys = true,
            EnableAnimations = true,
            LauncherStyle = "square",
            Items = new List<LauncherItem>
            {
                new()
                {
                    Id = "item-antigravity",
                    Name = "Antigravity",
                    Subtitle = "AI Kodlama Ortamı",
                    Target = "antigravity",
                    Category = "app",
                    Icon = "antigravity"
                },
                new()
                {
                    Id = "item-powershell",
                    Name = "PowerShell",
                    Subtitle = "Sistem Kabuğu",
                    Target = "powershell.exe",
                    Category = "system",
                    Icon = "powershell"
                },
                new()
                {
                    Id = "item-taskmgr",
                    Name = "Görev Yöneticisi",
                    Subtitle = "Performans ve İşlemler",
                    Target = "taskmgr.exe",
                    Category = "system",
                    Icon = "cpu"
                },
                new()
                {
                    Id = "item-desktop",
                    Name = "Masaüstü Klasörü",
                    Subtitle = "Hızlı Dosya Erişimi",
                    Target = @"%USERPROFILE%\Desktop",
                    Category = "file",
                    Icon = "folder"
                },
                new()
                {
                    Id = "item-github",
                    Name = "GitHub",
                    Subtitle = "github.com",
                    Target = "https://github.com",
                    Category = "web",
                    Icon = "globe"
                },
                new()
                {
                    Id = "item-calc",
                    Name = "Hesap Makinesi",
                    Subtitle = "Sistem Aracı",
                    Target = "calc.exe",
                    Category = "system",
                    Icon = "calculator"
                }
            }
        };
    }
}
