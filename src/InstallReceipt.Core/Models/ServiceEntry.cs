namespace InstallReceipt.Core.Models;

public sealed record ServiceEntry
{
    public string ServiceName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;
    public string StartType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Signer { get; init; } = string.Empty;
}
