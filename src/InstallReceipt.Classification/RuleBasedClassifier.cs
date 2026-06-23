using InstallReceipt.Core.Models;

namespace InstallReceipt.Classification;

public sealed class RuleBasedClassifier
{
    private const int MaxFileGroups = 80;

    public InstallReceiptDocument CreateReceipt(SnapshotDiff diff)
    {
        var items = new List<ReceiptItem>();

        items.AddRange(CreateInstalledAppItems(diff.AddedInstalledApps));
        items.AddRange(CreateFileItems(diff.AddedFiles));
        items.AddRange(CreateStartupRegistryItems(diff.AddedRegistryEntries));
        items.AddRange(CreateServiceItems(diff.AddedServices));
        items.AddRange(CreateScheduledTaskItems(diff.AddedScheduledTasks));
        items.AddRange(CreateFileAssociationItems(diff.AddedFileAssociations, diff.ModifiedFileAssociations));
        items.AddRange(CreateContextMenuItems(diff.AddedContextMenuEntries, diff.ModifiedContextMenuEntries));

        var attentionScore = items.Sum(item => item.AttentionWeight);
        var startupFolderEntries = diff.AddedFiles.Count(IsStartupFolderEntry);
        var appDataLocations = CountGroupedAppDataLocations(diff.AddedFiles);
        var warnings = diff.Before.ScanWarnings
            .Concat(diff.After.ScanWarnings)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new InstallReceiptDocument
        {
            CreatedAt = DateTimeOffset.Now,
            App = PickAppInfo(diff),
            Summary = new ReceiptSummary
            {
                AttentionLevel = ToAttentionLevel(attentionScore),
                AttentionScore = attentionScore,
                AddedFiles = diff.AddedFiles.Count,
                AddedSizeBytes = diff.AddedFiles.Sum(file => Math.Max(0, file.SizeBytes)),
                StartupEntries = diff.AddedRegistryEntries.Count(IsStartupRegistryEntry) + startupFolderEntries,
                Services = diff.AddedServices.Count,
                ScheduledTasks = diff.AddedScheduledTasks.Count,
                AppDataLocations = appDataLocations,
                InstalledApps = diff.AddedInstalledApps.Count,
                FileAssociations = diff.AddedFileAssociations.Count + diff.ModifiedFileAssociations.Count,
                ContextMenuEntries = diff.AddedContextMenuEntries.Count + diff.ModifiedContextMenuEntries.Count
            },
            Items = items
                .OrderByDescending(item => item.AttentionWeight)
                .ThenBy(item => item.Type, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            NotDetected = CreateNotDetectedList(diff),
            Warnings = warnings
        };
    }

    private static IEnumerable<ReceiptItem> CreateInstalledAppItems(IEnumerable<InstalledAppEntry> apps)
    {
        foreach (var app in apps.OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ReceiptItem
            {
                Type = "uninstall-entry",
                Name = NonEmpty(app.DisplayName, app.RegistryKey),
                Path = app.RegistryKey,
                Description = "Windowsのアプリ一覧に登録されました。",
                Reason = "Uninstallキーに新しい登録が追加されました。",
                AttentionWeight = 1
            };
        }
    }

    private static IEnumerable<ReceiptItem> CreateFileItems(IEnumerable<FileSystemEntry> files)
    {
        foreach (var group in GroupFileEntries(files).Take(MaxFileGroups))
        {
            yield return group;
        }
    }

    private static IEnumerable<ReceiptItem> CreateStartupRegistryItems(IEnumerable<RegistryEntry> registryEntries)
    {
        foreach (var entry in registryEntries.Where(IsStartupRegistryEntry).OrderBy(entry => entry.ValueName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ReceiptItem
            {
                Type = "startup",
                Name = NonEmpty(entry.ValueName, "Startup entry"),
                Path = entry.KeyPath,
                Description = "PC起動時またはログオン時に自動で起動します。",
                Reason = "Runキーに新しい値が追加されました。",
                AttentionWeight = 2
            };
        }
    }

    private static IEnumerable<ReceiptItem> CreateServiceItems(IEnumerable<ServiceEntry> services)
    {
        foreach (var service in services.OrderBy(service => service.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ReceiptItem
            {
                Type = "service",
                Name = NonEmpty(service.DisplayName, service.ServiceName),
                Path = service.ExecutablePath,
                Description = $"Windowsサービスが追加されました。起動方法: {NonEmpty(service.StartType, "不明")}",
                Reason = "Servicesキーに新しいWin32サービスが追加されました。",
                AttentionWeight = 3
            };
        }
    }

    private static IEnumerable<ReceiptItem> CreateScheduledTaskItems(IEnumerable<ScheduledTaskEntry> tasks)
    {
        foreach (var task in tasks.OrderBy(task => task.TaskName, StringComparer.OrdinalIgnoreCase))
        {
            var triggerText = task.Triggers.Count == 0 ? "トリガー不明" : string.Join(", ", task.Triggers.Distinct(StringComparer.OrdinalIgnoreCase));
            yield return new ReceiptItem
            {
                Type = "scheduled-task",
                Name = task.TaskName,
                Path = task.ExecutablePath,
                Description = $"スケジュールタスクが追加されました。{triggerText}",
                Reason = "Task Schedulerの定義に新しいタスクが追加されました。",
                AttentionWeight = 2
            };
        }
    }

    private static IEnumerable<ReceiptItem> CreateFileAssociationItems(
        IEnumerable<FileAssociationEntry> added,
        IEnumerable<ModifiedItem<FileAssociationEntry>> modified)
    {
        foreach (var association in added.OrderBy(entry => entry.Extension, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ReceiptItem
            {
                Type = "file-association",
                Name = $"{association.Extension} -> {association.ProgId}",
                Path = association.SourceKey,
                Description = "ファイルの開き方に関係する登録が追加されました。",
                Reason = "FileExtsの関連付け情報に新しい登録が追加されました。",
                AttentionWeight = 2
            };
        }

        foreach (var association in modified.OrderBy(entry => entry.After.Extension, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ReceiptItem
            {
                Type = "file-association",
                Name = $"{association.After.Extension} -> {association.After.ProgId}",
                Path = association.After.SourceKey,
                Description = "ファイルの開き方に関係する登録が変更されました。",
                Reason = "FileExtsの関連付け情報が変更されました。",
                AttentionWeight = 2
            };
        }
    }

    private static IEnumerable<ReceiptItem> CreateContextMenuItems(
        IEnumerable<ContextMenuEntry> added,
        IEnumerable<ModifiedItem<ContextMenuEntry>> modified)
    {
        foreach (var menu in added.OrderBy(entry => entry.Verb, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ReceiptItem
            {
                Type = "context-menu",
                Name = $"{menu.Target}: {menu.Verb}",
                Path = menu.Command,
                Description = "右クリックメニューに項目が追加されました。",
                Reason = "代表的なshellコンテキストメニューキーに新しい項目が追加されました。",
                AttentionWeight = 2
            };
        }

        foreach (var menu in modified.OrderBy(entry => entry.After.Verb, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ReceiptItem
            {
                Type = "context-menu",
                Name = $"{menu.After.Target}: {menu.After.Verb}",
                Path = menu.After.Command,
                Description = "右クリックメニューの項目が変更されました。",
                Reason = "代表的なshellコンテキストメニューキーのコマンドが変更されました。",
                AttentionWeight = 2
            };
        }
    }

    private static IEnumerable<ReceiptItem> GroupFileEntries(IEnumerable<FileSystemEntry> files)
    {
        return files
            .GroupBy(FileGroupKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase).First();
                var (type, description, reason, weight) = ClassifyFileGroup(first);
                return new ReceiptItem
                {
                    Type = type,
                    Name = MakeFileGroupName(first, group.Count()),
                    Path = first.Path,
                    Description = description,
                    Reason = reason,
                    AttentionWeight = weight
                };
            });
    }

    private static (string Type, string Description, string Reason, int Weight) ClassifyFileGroup(FileSystemEntry entry)
    {
        if (IsStartupFolderEntry(entry))
        {
            return ("startup-folder", "スタートアップフォルダに項目が追加されました。", "Startupフォルダ配下に新しい項目が追加されました。", 2);
        }

        if (entry.Root.Contains("AppData", StringComparison.OrdinalIgnoreCase))
        {
            if (PathHasAnySegment(entry.Path, "Cache", "Caches", "Temp", "Logs", "Log"))
            {
                return ("appdata-cache", "キャッシュまたはログと思われる場所が追加されました。", "AppData配下でCache/Temp/Log系の名前を含みます。", 1);
            }

            return ("appdata", "設定やユーザーデータの保存先候補が追加されました。", "AppData配下に新しいフォルダまたはファイルが追加されました。", 1);
        }

        if (entry.Root.Contains("ProgramData", StringComparison.OrdinalIgnoreCase))
        {
            return ("program-data", "全ユーザー向けの設定やデータの保存先候補が追加されました。", "ProgramData配下に新しいフォルダまたはファイルが追加されました。", 1);
        }

        if (entry.Root.Contains("Program Files", StringComparison.OrdinalIgnoreCase))
        {
            return ("application-files", "アプリ本体と思われる場所が追加されました。", "Program Files配下に新しいフォルダまたはファイルが追加されました。", 1);
        }

        return ("file", "ファイルシステム上に項目が追加されました。", $"{entry.Root}配下に新しい項目が追加されました。", 1);
    }

    private static string MakeFileGroupName(FileSystemEntry first, int count)
    {
        var name = System.IO.Path.GetFileName(first.Path.TrimEnd(
            System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar));

        if (string.IsNullOrWhiteSpace(name))
        {
            name = first.Path;
        }

        return count <= 1 ? name : $"{name} ({count}件)";
    }

    private static string FileGroupKey(FileSystemEntry entry)
    {
        var root = entry.Root.Length == 0 ? "file" : entry.Root;
        var path = entry.Path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        var name = System.IO.Path.GetFileName(path);
        return $"{root}|{name}";
    }

    private static ReceiptAppInfo PickAppInfo(SnapshotDiff diff)
    {
        var addedApp = diff.AddedInstalledApps
            .OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(app => !string.IsNullOrWhiteSpace(app.DisplayName));

        if (addedApp is not null)
        {
            return new ReceiptAppInfo
            {
                Name = addedApp.DisplayName,
                Publisher = addedApp.Publisher,
                Version = addedApp.Version
            };
        }

        var appHint = NonEmpty(diff.After.AppHint, diff.Before.AppHint);
        return new ReceiptAppInfo
        {
            Name = string.IsNullOrWhiteSpace(appHint) ? "Unknown app" : appHint
        };
    }

    private static AttentionLevel ToAttentionLevel(int score)
    {
        if (score >= 5)
        {
            return AttentionLevel.High;
        }

        return score >= 3 ? AttentionLevel.Medium : AttentionLevel.Low;
    }

    private static List<string> CreateNotDetectedList(SnapshotDiff diff)
    {
        var notDetected = new List<string>();

        if (!diff.AddedRegistryEntries.Any(IsStartupRegistryEntry) && !diff.AddedFiles.Any(IsStartupFolderEntry))
        {
            notDetected.Add("自動起動");
        }

        if (diff.AddedServices.Count == 0)
        {
            notDetected.Add("Windowsサービス");
        }

        if (diff.AddedScheduledTasks.Count == 0)
        {
            notDetected.Add("スケジュールタスク");
        }

        if (diff.AddedInstalledApps.Count == 0)
        {
            notDetected.Add("アンインストール登録");
        }

        if (diff.AddedFileAssociations.Count == 0 && diff.ModifiedFileAssociations.Count == 0)
        {
            notDetected.Add("主要なファイル関連付け");
        }

        if (diff.AddedContextMenuEntries.Count == 0 && diff.ModifiedContextMenuEntries.Count == 0)
        {
            notDetected.Add("代表的な右クリックメニュー項目");
        }

        return notDetected;
    }

    private static int CountGroupedAppDataLocations(IEnumerable<FileSystemEntry> files)
    {
        return files
            .Where(file => file.Root.Contains("AppData", StringComparison.OrdinalIgnoreCase))
            .Select(FileGroupKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private static bool IsStartupRegistryEntry(RegistryEntry entry)
    {
        return entry.Category.Equals("Startup", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStartupFolderEntry(FileSystemEntry entry)
    {
        return entry.Root.Contains("Startup", StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathHasAnySegment(string path, params string[] segments)
    {
        return path.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
            .Any(part => segments.Any(segment => part.Equals(segment, StringComparison.OrdinalIgnoreCase)));
    }

    private static string NonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
