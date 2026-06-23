namespace InstallReceipt.Core.Models;

public sealed record RegistryEntry
{
    public string KeyPath { get; init; } = string.Empty;
    public string ValueName { get; init; } = string.Empty;
    public string ValueData { get; init; } = string.Empty;
    public string ValueType { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
}
