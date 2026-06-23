namespace InstallReceipt.Core.Models;

public sealed record FileAssociationEntry
{
    public string Extension { get; init; } = string.Empty;
    public string ProgId { get; init; } = string.Empty;
    public string SourceKey { get; init; } = string.Empty;
}
