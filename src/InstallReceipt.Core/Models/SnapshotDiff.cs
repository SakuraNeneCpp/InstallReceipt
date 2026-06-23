namespace InstallReceipt.Core.Models;

public sealed record ModifiedItem<T>
{
    public required T Before { get; init; }
    public required T After { get; init; }
}

public sealed record SnapshotDiff
{
    public required Snapshot Before { get; init; }
    public required Snapshot After { get; init; }
    public List<FileSystemEntry> AddedFiles { get; init; } = [];
    public List<ModifiedItem<FileSystemEntry>> ModifiedFiles { get; init; } = [];
    public List<FileSystemEntry> RemovedFiles { get; init; } = [];
    public List<RegistryEntry> AddedRegistryEntries { get; init; } = [];
    public List<ModifiedItem<RegistryEntry>> ModifiedRegistryEntries { get; init; } = [];
    public List<RegistryEntry> RemovedRegistryEntries { get; init; } = [];
    public List<ServiceEntry> AddedServices { get; init; } = [];
    public List<ModifiedItem<ServiceEntry>> ModifiedServices { get; init; } = [];
    public List<ServiceEntry> RemovedServices { get; init; } = [];
    public List<ScheduledTaskEntry> AddedScheduledTasks { get; init; } = [];
    public List<ModifiedItem<ScheduledTaskEntry>> ModifiedScheduledTasks { get; init; } = [];
    public List<ScheduledTaskEntry> RemovedScheduledTasks { get; init; } = [];
    public List<InstalledAppEntry> AddedInstalledApps { get; init; } = [];
    public List<ModifiedItem<InstalledAppEntry>> ModifiedInstalledApps { get; init; } = [];
    public List<InstalledAppEntry> RemovedInstalledApps { get; init; } = [];
    public List<FileAssociationEntry> AddedFileAssociations { get; init; } = [];
    public List<ModifiedItem<FileAssociationEntry>> ModifiedFileAssociations { get; init; } = [];
    public List<FileAssociationEntry> RemovedFileAssociations { get; init; } = [];
    public List<ContextMenuEntry> AddedContextMenuEntries { get; init; } = [];
    public List<ModifiedItem<ContextMenuEntry>> ModifiedContextMenuEntries { get; init; } = [];
    public List<ContextMenuEntry> RemovedContextMenuEntries { get; init; } = [];
}
