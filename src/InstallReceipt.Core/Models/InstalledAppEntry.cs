namespace InstallReceipt.Core.Models;

public sealed record InstalledAppEntry
{
    public string DisplayName { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string InstallLocation { get; init; } = string.Empty;
    public string UninstallCommand { get; init; } = string.Empty;
    public string InstallDate { get; init; } = string.Empty;
    public string RegistryKey { get; init; } = string.Empty;
}
