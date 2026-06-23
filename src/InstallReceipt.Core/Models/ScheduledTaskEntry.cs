namespace InstallReceipt.Core.Models;

public sealed record ScheduledTaskEntry
{
    public string TaskName { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
    public List<string> Triggers { get; init; } = [];
    public bool Enabled { get; init; }
}
