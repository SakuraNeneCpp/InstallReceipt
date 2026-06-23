using InstallReceipt.Classification;
using InstallReceipt.Core.Models;
using InstallReceipt.Diff;
using InstallReceipt.Rendering;

var before = new Snapshot
{
    MachineName = "TEST-PC",
    UserName = "tester",
    AppHint = "ExampleApp",
    FileSystemEntries =
    {
        new FileSystemEntry
        {
            Path = @"C:\Program Files\Existing",
            Root = "Program Files",
            IsDirectory = true,
            FileType = "Directory"
        }
    },
    RegistryEntries =
    {
        new RegistryEntry
        {
            KeyPath = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run",
            ValueName = "Existing",
            ValueData = @"C:\Existing\existing.exe",
            ValueType = "String",
            Category = "Startup"
        }
    }
};

var after = new Snapshot
{
    MachineName = "TEST-PC",
    UserName = "tester",
    AppHint = "ExampleApp",
    FileSystemEntries =
    {
        before.FileSystemEntries[0],
        new FileSystemEntry
        {
            Path = @"C:\Program Files\ExampleApp",
            Root = "Program Files",
            IsDirectory = true,
            FileType = "Directory"
        },
        new FileSystemEntry
        {
            Path = @"C:\Users\tester\AppData\Roaming\ExampleApp",
            Root = "AppData Roaming",
            IsDirectory = true,
            FileType = "Directory"
        }
    },
    RegistryEntries =
    {
        before.RegistryEntries[0],
        new RegistryEntry
        {
            KeyPath = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run",
            ValueName = "ExampleApp",
            ValueData = @"C:\Program Files\ExampleApp\ExampleApp.exe",
            ValueType = "String",
            Category = "Startup"
        }
    },
    Services =
    {
        new ServiceEntry
        {
            ServiceName = "ExampleUpdate",
            DisplayName = "Example Update Service",
            ExecutablePath = @"C:\Program Files\ExampleApp\updater.exe",
            StartType = "Automatic"
        }
    },
    ScheduledTasks =
    {
        new ScheduledTaskEntry
        {
            TaskName = @"\ExampleApp\Update Check",
            ExecutablePath = @"C:\Program Files\ExampleApp\updater.exe",
            Arguments = "--check",
            Enabled = true,
            Triggers = { "CalendarTrigger" }
        }
    },
    InstalledApps =
    {
        new InstalledAppEntry
        {
            DisplayName = "ExampleApp",
            Publisher = "Example Inc.",
            Version = "1.2.3",
            RegistryKey = @"HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\ExampleApp"
        }
    },
    FileAssociations =
    {
        new FileAssociationEntry
        {
            Extension = ".example",
            ProgId = "ExampleApp.File",
            SourceKey = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.example\UserChoice"
        }
    },
    ContextMenuEntries =
    {
        new ContextMenuEntry
        {
            Target = "All files",
            Verb = "OpenWithExample",
            Command = @"C:\Program Files\ExampleApp\ExampleApp.exe ""%1""",
            RegistryKey = @"HKCU\Software\Classes\*\shell\OpenWithExample"
        }
    }
};

var diff = new SnapshotDiffer().Compare(before, after);
AssertEqual(2, diff.AddedFiles.Count, "added files");
AssertEqual(1, diff.AddedRegistryEntries.Count, "added startup registry entry");
AssertEqual(1, diff.AddedServices.Count, "added service");
AssertEqual(1, diff.AddedScheduledTasks.Count, "added scheduled task");
AssertEqual(1, diff.AddedInstalledApps.Count, "added installed app");

var receipt = new RuleBasedClassifier().CreateReceipt(diff);
AssertEqual("ExampleApp", receipt.App.Name, "receipt app name");
AssertEqual(AttentionLevel.High, receipt.Summary.AttentionLevel, "attention level");
AssertEqual(1, receipt.Summary.StartupEntries, "startup count");
AssertTrue(receipt.Items.Any(item => item.Type == "service"), "service item exists");
AssertTrue(receipt.Items.Any(item => item.Type == "scheduled-task"), "task item exists");
AssertTrue(receipt.Items.Any(item => item.Type == "file-association"), "association item exists");
AssertTrue(receipt.Items.Any(item => item.Type == "context-menu"), "context menu item exists");

var renderer = new ReceiptRenderer();
var markdown = renderer.RenderMarkdown(receipt);
var html = renderer.RenderHtml(receipt);
var json = renderer.RenderJson(receipt);

AssertTrue(markdown.Contains("ExampleApp のインストールレシート", StringComparison.Ordinal), "markdown title");
AssertTrue(html.Contains("<!doctype html>", StringComparison.OrdinalIgnoreCase), "html document");
AssertTrue(json.Contains("\"name\": \"ExampleApp\"", StringComparison.Ordinal), "json app name");

Console.WriteLine("InstallReceipt.Tests: all checks passed.");

static void AssertEqual<T>(T expected, T actual, string name)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{name}: expected {expected}, actual {actual}");
    }
}

static void AssertTrue(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"{name}: expected true");
    }
}
