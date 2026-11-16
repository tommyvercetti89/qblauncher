using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using QBLauncherPro.Models;

namespace QBLauncherPro.Services;

public static class LauncherService
{
    public static void Launch(LauncherItem item, bool? overrideRunAsAdmin = null)
    {
        var rawTarget = item.Target?.Trim() ?? string.Empty;
        var rawArgs = item.Args?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(rawTarget))
        {
            throw new InvalidOperationException("Hedef yol veya adres boş olamaz.");
        }

        var target = Environment.ExpandEnvironmentVariables(rawTarget);
        var args = Environment.ExpandEnvironmentVariables(rawArgs);

        if (string.Equals(target, "antigravity", StringComparison.OrdinalIgnoreCase))
        {
            target = ResolveAntigravityPath();
        }
        else if (string.Equals(item.Category, "web", StringComparison.OrdinalIgnoreCase) &&
                 !target.Contains("://", StringComparison.Ordinal))
        {
            target = "https://" + target.TrimStart('/');
        }

        ProcessStartInfo psi;
        if (target.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) && File.Exists(target))
        {
            var scriptArgs = string.IsNullOrEmpty(args)
                ? $"-NoProfile -ExecutionPolicy Bypass -File \"{target}\""
                : $"-NoProfile -ExecutionPolicy Bypass -File \"{target}\" {args}";
            psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = scriptArgs,
                WorkingDirectory = Path.GetDirectoryName(target) ?? string.Empty,
                UseShellExecute = true
            };
        }
        else
        {
            psi = new ProcessStartInfo
            {
                FileName = target,
                Arguments = args,
                UseShellExecute = true
            };

            if (Directory.Exists(target))
            {
                psi.Arguments = string.Empty;
            }
            else if (File.Exists(target))
            {
                var dir = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(dir))
                {
                    psi.WorkingDirectory = dir;
                }
            }
        }

        if (overrideRunAsAdmin ?? item.RunAsAdmin)
        {
            psi.Verb = "runas";
        }

        try
        {
            Process.Start(psi);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User canceled the UAC elevation prompt intentionally
        }
    }

    private static readonly HashSet<string> AdminCapableExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "powershell", "powershell.exe",
        "pwsh", "pwsh.exe",
        "cmd", "cmd.exe",
        "wt", "wt.exe",
        "taskmgr", "taskmgr.exe",
        "regedit", "regedit.exe",
        "mmc", "mmc.exe"
    };

    private static readonly HashSet<string> AdminCapableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".msc", ".ps1", ".bat", ".cmd"
    };

    public static bool SupportsAdminOption(LauncherItem item)
    {
        if (item.RunAsAdmin)
        {
            return true;
        }

        if (string.Equals(item.Category, "file", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Category, "web", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expanded = Environment.ExpandEnvironmentVariables(item.Target?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(expanded) ||
            expanded.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            expanded.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(expanded))
        {
            return false;
        }

        var fileName = Path.GetFileName(expanded);
        var ext = Path.GetExtension(expanded);
        return AdminCapableExecutables.Contains(fileName) || AdminCapableExtensions.Contains(ext);
    }

    private static string ResolveAntigravityPath()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var prog = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string[] candidates =
        {
            Path.Combine(local, "Programs", "Antigravity", "Antigravity.exe"),
            Path.Combine(prog, "Antigravity", "Antigravity.exe")
        };
        foreach (var c in candidates)
        {
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
            {
                return c;
            }
        }
        return "antigravity";
    }
}
