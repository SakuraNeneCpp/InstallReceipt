using InstallReceipt.Core.Models;
using Microsoft.Win32;

namespace InstallReceipt.Platform.Windows.Scanning;

public sealed class RegistrySnapshotScanner
{
    public List<RegistryEntry> CaptureStartupEntries(List<string> warnings)
    {
        var entries = new List<RegistryEntry>();

        ReadValueEntries(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU", "Startup", entries, warnings);
        ReadValueEntries(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM", "Startup", entries, warnings);
        ReadValueEntries(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM", "Startup", entries, warnings);

        return entries
            .GroupBy(entry => $"{entry.KeyPath}\\{entry.ValueName}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.KeyPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.ValueName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<InstalledAppEntry> CaptureInstalledApps(List<string> warnings)
    {
        var apps = new List<InstalledAppEntry>();

        ReadUninstallEntries(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall", "HKCU", apps, warnings);
        ReadUninstallEntries(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM", apps, warnings);
        ReadUninstallEntries(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM", apps, warnings);

        return apps
            .GroupBy(app => app.RegistryKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<FileAssociationEntry> CaptureFileAssociations(List<string> warnings)
    {
        var associations = new List<FileAssociationEntry>();
        const string fileExtsPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts";

        try
        {
            using var fileExts = Registry.CurrentUser.OpenSubKey(fileExtsPath);
            if (fileExts is null)
            {
                return associations;
            }

            foreach (var extension in fileExts.GetSubKeyNames().Where(name => name.StartsWith(".", StringComparison.Ordinal)))
            {
                using var extensionKey = fileExts.OpenSubKey(extension);
                if (extensionKey is null)
                {
                    continue;
                }

                using var userChoice = extensionKey.OpenSubKey("UserChoice");
                var progId = userChoice?.GetValue("ProgId")?.ToString();
                if (!string.IsNullOrWhiteSpace(progId))
                {
                    associations.Add(new FileAssociationEntry
                    {
                        Extension = extension,
                        ProgId = progId,
                        SourceKey = $@"HKCU\{fileExtsPath}\{extension}\UserChoice"
                    });
                }

                using var openWithProgids = extensionKey.OpenSubKey("OpenWithProgids");
                if (openWithProgids is null)
                {
                    continue;
                }

                foreach (var valueName in openWithProgids.GetValueNames())
                {
                    if (string.IsNullOrWhiteSpace(valueName))
                    {
                        continue;
                    }

                    associations.Add(new FileAssociationEntry
                    {
                        Extension = extension,
                        ProgId = valueName,
                        SourceKey = $@"HKCU\{fileExtsPath}\{extension}\OpenWithProgids"
                    });
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            warnings.Add($"FileExts関連付けを読み取れませんでした。{ex.Message}");
        }

        return associations
            .GroupBy(entry => $"{entry.Extension}|{entry.ProgId}|{entry.SourceKey}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.Extension, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.ProgId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<ContextMenuEntry> CaptureContextMenuEntries(List<string> warnings)
    {
        var entries = new List<ContextMenuEntry>();
        var targets = new[]
        {
            new ShellTarget(Registry.CurrentUser, "HKCU", @"Software\Classes\*\shell", "All files"),
            new ShellTarget(Registry.CurrentUser, "HKCU", @"Software\Classes\Directory\shell", "Directory"),
            new ShellTarget(Registry.CurrentUser, "HKCU", @"Software\Classes\Directory\Background\shell", "Directory background"),
            new ShellTarget(Registry.CurrentUser, "HKCU", @"Software\Classes\Folder\shell", "Folder"),
            new ShellTarget(Registry.LocalMachine, "HKLM", @"Software\Classes\*\shell", "All files"),
            new ShellTarget(Registry.LocalMachine, "HKLM", @"Software\Classes\Directory\shell", "Directory"),
            new ShellTarget(Registry.LocalMachine, "HKLM", @"Software\Classes\Directory\Background\shell", "Directory background"),
            new ShellTarget(Registry.LocalMachine, "HKLM", @"Software\Classes\Folder\shell", "Folder")
        };

        foreach (var target in targets)
        {
            ReadShellEntries(target, entries, warnings);
        }

        return entries
            .GroupBy(entry => $"{entry.Target}|{entry.Verb}|{entry.RegistryKey}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.Target, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Verb, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ReadValueEntries(
        RegistryKey root,
        string subKeyPath,
        string rootLabel,
        string category,
        List<RegistryEntry> entries,
        List<string> warnings)
    {
        try
        {
            using var key = root.OpenSubKey(subKeyPath);
            if (key is null)
            {
                return;
            }

            foreach (var valueName in key.GetValueNames())
            {
                entries.Add(new RegistryEntry
                {
                    KeyPath = $@"{rootLabel}\{subKeyPath}",
                    ValueName = valueName.Length == 0 ? "(既定)" : valueName,
                    ValueData = FormatRegistryValue(key.GetValue(valueName)),
                    ValueType = key.GetValueKind(valueName).ToString(),
                    Category = category
                });
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            warnings.Add($@"{rootLabel}\{subKeyPath} を読み取れませんでした。{ex.Message}");
        }
    }

    private static void ReadUninstallEntries(
        RegistryKey root,
        string subKeyPath,
        string rootLabel,
        List<InstalledAppEntry> apps,
        List<string> warnings)
    {
        try
        {
            using var uninstallKey = root.OpenSubKey(subKeyPath);
            if (uninstallKey is null)
            {
                return;
            }

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                using var appKey = uninstallKey.OpenSubKey(subKeyName);
                if (appKey is null)
                {
                    continue;
                }

                var displayName = appKey.GetValue("DisplayName")?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                apps.Add(new InstalledAppEntry
                {
                    DisplayName = displayName,
                    Publisher = appKey.GetValue("Publisher")?.ToString() ?? string.Empty,
                    Version = appKey.GetValue("DisplayVersion")?.ToString() ?? string.Empty,
                    InstallLocation = appKey.GetValue("InstallLocation")?.ToString() ?? string.Empty,
                    UninstallCommand = appKey.GetValue("UninstallString")?.ToString() ?? string.Empty,
                    InstallDate = appKey.GetValue("InstallDate")?.ToString() ?? string.Empty,
                    RegistryKey = $@"{rootLabel}\{subKeyPath}\{subKeyName}"
                });
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            warnings.Add($@"{rootLabel}\{subKeyPath} を読み取れませんでした。{ex.Message}");
        }
    }

    private static void ReadShellEntries(ShellTarget target, List<ContextMenuEntry> entries, List<string> warnings)
    {
        try
        {
            using var shell = target.Root.OpenSubKey(target.SubKeyPath);
            if (shell is null)
            {
                return;
            }

            foreach (var verb in shell.GetSubKeyNames())
            {
                using var verbKey = shell.OpenSubKey(verb);
                using var commandKey = verbKey?.OpenSubKey("command");
                var command = commandKey?.GetValue(null)?.ToString() ?? string.Empty;
                var displayName = verbKey?.GetValue(null)?.ToString();

                entries.Add(new ContextMenuEntry
                {
                    Target = target.TargetName,
                    Verb = string.IsNullOrWhiteSpace(displayName) ? verb : $"{verb} ({displayName})",
                    Command = command,
                    RegistryKey = $@"{target.RootLabel}\{target.SubKeyPath}\{verb}"
                });
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            warnings.Add($@"{target.RootLabel}\{target.SubKeyPath} を読み取れませんでした。{ex.Message}");
        }
    }

    private static string FormatRegistryValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string[] values => string.Join("; ", values),
            byte[] bytes => Convert.ToHexString(bytes),
            _ => value.ToString() ?? string.Empty
        };
    }

    private sealed record ShellTarget(RegistryKey Root, string RootLabel, string SubKeyPath, string TargetName);
}
