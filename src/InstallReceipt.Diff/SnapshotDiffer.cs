using InstallReceipt.Core.Models;

namespace InstallReceipt.Diff;

public sealed class SnapshotDiffer
{
    public SnapshotDiff Compare(Snapshot before, Snapshot after)
    {
        var fileDiff = DiffBy(
            before.FileSystemEntries,
            after.FileSystemEntries,
            entry => NormalizePath(entry.Path),
            FilesEqual);

        var registryDiff = DiffBy(
            before.RegistryEntries,
            after.RegistryEntries,
            entry => $"{entry.KeyPath}\\{entry.ValueName}",
            RegistryEntriesEqual);

        var serviceDiff = DiffBy(
            before.Services,
            after.Services,
            entry => entry.ServiceName,
            ServicesEqual);

        var taskDiff = DiffBy(
            before.ScheduledTasks,
            after.ScheduledTasks,
            entry => entry.TaskName,
            ScheduledTasksEqual);

        var appDiff = DiffBy(
            before.InstalledApps,
            after.InstalledApps,
            entry => entry.RegistryKey.Length > 0 ? entry.RegistryKey : entry.DisplayName,
            InstalledAppsEqual);

        var associationDiff = DiffBy(
            before.FileAssociations,
            after.FileAssociations,
            entry => $"{entry.Extension}|{entry.ProgId}|{entry.SourceKey}",
            FileAssociationsEqual);

        var contextMenuDiff = DiffBy(
            before.ContextMenuEntries,
            after.ContextMenuEntries,
            entry => $"{entry.Target}|{entry.Verb}|{entry.RegistryKey}",
            ContextMenusEqual);

        return new SnapshotDiff
        {
            Before = before,
            After = after,
            AddedFiles = fileDiff.Added,
            ModifiedFiles = fileDiff.Modified,
            RemovedFiles = fileDiff.Removed,
            AddedRegistryEntries = registryDiff.Added,
            ModifiedRegistryEntries = registryDiff.Modified,
            RemovedRegistryEntries = registryDiff.Removed,
            AddedServices = serviceDiff.Added,
            ModifiedServices = serviceDiff.Modified,
            RemovedServices = serviceDiff.Removed,
            AddedScheduledTasks = taskDiff.Added,
            ModifiedScheduledTasks = taskDiff.Modified,
            RemovedScheduledTasks = taskDiff.Removed,
            AddedInstalledApps = appDiff.Added,
            ModifiedInstalledApps = appDiff.Modified,
            RemovedInstalledApps = appDiff.Removed,
            AddedFileAssociations = associationDiff.Added,
            ModifiedFileAssociations = associationDiff.Modified,
            RemovedFileAssociations = associationDiff.Removed,
            AddedContextMenuEntries = contextMenuDiff.Added,
            ModifiedContextMenuEntries = contextMenuDiff.Modified,
            RemovedContextMenuEntries = contextMenuDiff.Removed
        };
    }

    private static CollectionDiff<T> DiffBy<T>(
        IEnumerable<T> before,
        IEnumerable<T> after,
        Func<T, string> keySelector,
        Func<T, T, bool> equals)
    {
        var beforeByKey = before
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var afterByKey = after
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var added = new List<T>();
        var modified = new List<ModifiedItem<T>>();
        var removed = new List<T>();

        foreach (var (key, afterItem) in afterByKey)
        {
            if (!beforeByKey.TryGetValue(key, out var beforeItem))
            {
                added.Add(afterItem);
                continue;
            }

            if (!equals(beforeItem, afterItem))
            {
                modified.Add(new ModifiedItem<T>
                {
                    Before = beforeItem,
                    After = afterItem
                });
            }
        }

        foreach (var (key, beforeItem) in beforeByKey)
        {
            if (!afterByKey.ContainsKey(key))
            {
                removed.Add(beforeItem);
            }
        }

        return new CollectionDiff<T>(added, modified, removed);
    }

    private static bool FilesEqual(FileSystemEntry before, FileSystemEntry after)
    {
        return before.IsDirectory == after.IsDirectory
            && before.SizeBytes == after.SizeBytes
            && before.ModifiedAt == after.ModifiedAt
            && string.Equals(before.FileType, after.FileType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RegistryEntriesEqual(RegistryEntry before, RegistryEntry after)
    {
        return string.Equals(before.ValueData, after.ValueData, StringComparison.Ordinal)
            && string.Equals(before.ValueType, after.ValueType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(before.Category, after.Category, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ServicesEqual(ServiceEntry before, ServiceEntry after)
    {
        return string.Equals(before.ExecutablePath, after.ExecutablePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(before.StartType, after.StartType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(before.DisplayName, after.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ScheduledTasksEqual(ScheduledTaskEntry before, ScheduledTaskEntry after)
    {
        return string.Equals(before.ExecutablePath, after.ExecutablePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(before.Arguments, after.Arguments, StringComparison.Ordinal)
            && before.Enabled == after.Enabled
            && before.Triggers.SequenceEqual(after.Triggers, StringComparer.OrdinalIgnoreCase);
    }

    private static bool InstalledAppsEqual(InstalledAppEntry before, InstalledAppEntry after)
    {
        return string.Equals(before.DisplayName, after.DisplayName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(before.Publisher, after.Publisher, StringComparison.OrdinalIgnoreCase)
            && string.Equals(before.Version, after.Version, StringComparison.OrdinalIgnoreCase)
            && string.Equals(before.InstallLocation, after.InstallLocation, StringComparison.OrdinalIgnoreCase)
            && string.Equals(before.UninstallCommand, after.UninstallCommand, StringComparison.OrdinalIgnoreCase);
    }

    private static bool FileAssociationsEqual(FileAssociationEntry before, FileAssociationEntry after)
    {
        return string.Equals(before.Extension, after.Extension, StringComparison.OrdinalIgnoreCase)
            && string.Equals(before.ProgId, after.ProgId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContextMenusEqual(ContextMenuEntry before, ContextMenuEntry after)
    {
        return string.Equals(before.Command, after.Command, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
    }

    private sealed record CollectionDiff<T>(
        List<T> Added,
        List<ModifiedItem<T>> Modified,
        List<T> Removed);
}
