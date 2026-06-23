namespace InstallReceipt.Core.Models;

public sealed record Snapshot
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public string MachineName { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string AppHint { get; init; } = string.Empty;
    public List<FileSystemEntry> FileSystemEntries { get; init; } = [];
    public List<RegistryEntry> RegistryEntries { get; init; } = [];
    public List<ServiceEntry> Services { get; init; } = [];
    public List<ScheduledTaskEntry> ScheduledTasks { get; init; } = [];
    public List<InstalledAppEntry> InstalledApps { get; init; } = [];
    public List<FileAssociationEntry> FileAssociations { get; init; } = [];
    public List<ContextMenuEntry> ContextMenuEntries { get; init; } = [];
    public List<string> ScanWarnings { get; init; } = [];
}
