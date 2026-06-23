namespace InstallReceipt.Platform.Windows.Scanning;

public sealed record ScanRoot(string Label, string Path, int MaxDepth = 1, int MaxEntries = 20000);
