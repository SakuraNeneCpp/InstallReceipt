namespace InstallReceipt.Core.Models;

public enum AttentionLevel
{
    Low,
    Medium,
    High
}

public sealed record ReceiptAppInfo
{
    public string Name { get; init; } = "Unknown app";
    public string Publisher { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
}

public sealed record ReceiptSummary
{
    public AttentionLevel AttentionLevel { get; init; }
    public int AttentionScore { get; init; }
    public int AddedFiles { get; init; }
    public long AddedSizeBytes { get; init; }
    public int StartupEntries { get; init; }
    public int Services { get; init; }
    public int ScheduledTasks { get; init; }
    public int AppDataLocations { get; init; }
    public int InstalledApps { get; init; }
    public int FileAssociations { get; init; }
    public int ContextMenuEntries { get; init; }
}

public sealed record ReceiptItem
{
    public string Type { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public int AttentionWeight { get; init; }
}

public sealed record InstallReceiptDocument
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public ReceiptAppInfo App { get; init; } = new();
    public ReceiptSummary Summary { get; init; } = new();
    public List<ReceiptItem> Items { get; init; } = [];
    public List<string> NotDetected { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
}
