namespace InstallReceipt.Core.Models;

public sealed record FileSystemEntry
{
    public string Path { get; init; } = string.Empty;
    public string Root { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? ModifiedAt { get; init; }
    public string Extension { get; init; } = string.Empty;
    public string FileType { get; init; } = string.Empty;
    public string Signer { get; init; } = string.Empty;
}
