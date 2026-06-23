using System.Xml.Linq;
using InstallReceipt.Core.Models;

namespace InstallReceipt.Platform.Windows.Scanning;

public sealed class ScheduledTaskScanner
{
    public List<ScheduledTaskEntry> CaptureTasks(List<string> warnings, CancellationToken cancellationToken)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "Tasks");
        var tasks = new List<ScheduledTaskEntry>();

        if (!Directory.Exists(root))
        {
            return tasks;
        }

        foreach (var file in EnumerateTaskFiles(root, warnings, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var task = TryReadTask(root, file, warnings);
            if (task is not null)
            {
                tasks.Add(task);
            }
        }

        return tasks
            .OrderBy(task => task.TaskName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> EnumerateTaskFiles(string root, List<string> warnings, CancellationToken cancellationToken)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current).ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                warnings.Add($"Scheduled Tasks: {current} を読み取れませんでした。{ex.Message}");
                continue;
            }

            foreach (var directory in directories)
            {
                stack.Push(directory);
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current).ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                warnings.Add($"Scheduled Tasks: {current} のファイル一覧を読み取れませんでした。{ex.Message}");
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }
        }
    }

    private static ScheduledTaskEntry? TryReadTask(string root, string file, List<string> warnings)
    {
        try
        {
            var document = XDocument.Load(file, LoadOptions.None);
            var ns = document.Root?.Name.Namespace ?? XNamespace.None;
            var exec = document.Descendants(ns + "Exec").FirstOrDefault();
            var command = exec?.Element(ns + "Command")?.Value ?? string.Empty;
            var arguments = exec?.Element(ns + "Arguments")?.Value ?? string.Empty;
            var enabledText = document.Descendants(ns + "Settings").Elements(ns + "Enabled").FirstOrDefault()?.Value;
            var enabled = !string.Equals(enabledText, "false", StringComparison.OrdinalIgnoreCase);
            var triggers = document.Descendants(ns + "Triggers")
                .Elements()
                .Select(element => element.Name.LocalName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ScheduledTaskEntry
            {
                TaskName = "\\" + Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '\\'),
                ExecutablePath = Environment.ExpandEnvironmentVariables(command),
                Arguments = arguments,
                Triggers = triggers,
                Enabled = enabled
            };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Xml.XmlException)
        {
            warnings.Add($"Scheduled Task: {file} を読み取れませんでした。{ex.Message}");
            return null;
        }
    }
}
