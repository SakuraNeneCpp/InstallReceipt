namespace InstallReceipt.Core.Models;

public sealed record ContextMenuEntry
{
    public string Target { get; init; } = string.Empty;
    public string Verb { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public string RegistryKey { get; init; } = string.Empty;
}
