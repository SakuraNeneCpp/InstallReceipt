using InstallReceipt.Core.Models;

namespace InstallReceipt.Platform.Windows.Scanning;

public sealed class FileSystemSnapshotScanner
{
    public IReadOnlyList<ScanRoot> CreateDefaultRoots()
    {
        var roots = new List<ScanRoot>
        {
            new("Program Files", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)),
            new("Program Files (x86)", Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? string.Empty),
            new("ProgramData", Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)),
            new("AppData Roaming", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
            new("AppData Local", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
            new("Startup Folder", Environment.GetFolderPath(Environment.SpecialFolder.Startup), MaxDepth: 2),
            new("Common Startup Folder", Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), MaxDepth: 2)
        };

        return roots
            .Where(root => !string.IsNullOrWhiteSpace(root.Path))
            .GroupBy(root => NormalizePath(root.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public List<FileSystemEntry> Capture(IEnumerable<ScanRoot> roots, List<string> warnings, CancellationToken cancellationToken)
    {
        var entries = new List<FileSystemEntry>();

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(root.Path))
            {
                continue;
            }

            var countBefore = entries.Count;
            ScanDirectory(root, root.Path, depth: 0, entries, warnings, cancellationToken);

            if (entries.Count - countBefore >= root.MaxEntries)
            {
                warnings.Add($"{root.Label}: 最大取得件数 {root.MaxEntries} に達したため、一部を省略しました。");
            }
        }

        return entries
            .GroupBy(entry => NormalizePath(entry.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ScanDirectory(
        ScanRoot root,
        string directory,
        int depth,
        List<FileSystemEntry> entries,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (entries.Count(entry => entry.Root.Equals(root.Label, StringComparison.OrdinalIgnoreCase)) >= root.MaxEntries)
        {
            return;
        }

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(directory).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            warnings.Add($"{root.Label}: {directory} を読み取れませんでした。{ex.Message}");
            return;
        }

        foreach (var childDirectory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = CreateDirectoryEntry(root.Label, childDirectory);
            entries.Add(entry);

            if (depth < root.MaxDepth && !IsReparsePoint(childDirectory))
            {
                ScanDirectory(root, childDirectory, depth + 1, entries, warnings, cancellationToken);
            }
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            warnings.Add($"{root.Label}: {directory} のファイル一覧を読み取れませんでした。{ex.Message}");
            return;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(CreateFileEntry(root.Label, file));
        }
    }

    private static FileSystemEntry CreateDirectoryEntry(string rootLabel, string path)
    {
        var info = new DirectoryInfo(path);
        return new FileSystemEntry
        {
            Path = path,
            Root = rootLabel,
            IsDirectory = true,
            SizeBytes = 0,
            CreatedAt = SafeTime(() => info.CreationTimeUtc),
            ModifiedAt = SafeTime(() => info.LastWriteTimeUtc),
            Extension = string.Empty,
            FileType = "Directory"
        };
    }

    private static FileSystemEntry CreateFileEntry(string rootLabel, string path)
    {
        var info = new FileInfo(path);
        return new FileSystemEntry
        {
            Path = path,
            Root = rootLabel,
            IsDirectory = false,
            SizeBytes = SafeLong(() => info.Length),
            CreatedAt = SafeTime(() => info.CreationTimeUtc),
            ModifiedAt = SafeTime(() => info.LastWriteTimeUtc),
            Extension = info.Extension,
            FileType = info.Extension.Length == 0 ? "File" : info.Extension.TrimStart('.').ToUpperInvariant()
        };
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return true;
        }
    }

    private static DateTimeOffset? SafeTime(Func<DateTime> read)
    {
        try
        {
            return new DateTimeOffset(DateTime.SpecifyKind(read(), DateTimeKind.Utc));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static long SafeLong(Func<long> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return 0;
        }
    }

    private static string NormalizePath(string path)
    {
        return path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
    }
}
