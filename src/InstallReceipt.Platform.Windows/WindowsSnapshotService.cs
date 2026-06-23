using InstallReceipt.Core.Models;
using InstallReceipt.Platform.Windows.Scanning;

namespace InstallReceipt.Platform.Windows;

public sealed class WindowsSnapshotService
{
    private readonly FileSystemSnapshotScanner _fileScanner = new();
    private readonly RegistrySnapshotScanner _registryScanner = new();
    private readonly ServiceRegistryScanner _serviceScanner = new();
    private readonly ScheduledTaskScanner _taskScanner = new();

    public Task<Snapshot> CaptureAsync(
        string? appHint,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Install Receipt MVP is Windows-only.");
            }

            var warnings = new List<string>();
            var snapshot = new Snapshot
            {
                CreatedAt = DateTimeOffset.Now,
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                AppHint = appHint?.Trim() ?? string.Empty
            };

            progress?.Report("ファイルシステムをスキャンしています...");
            snapshot.FileSystemEntries.AddRange(_fileScanner.Capture(_fileScanner.CreateDefaultRoots(), warnings, cancellationToken));

            progress?.Report("自動起動とアンインストール登録をスキャンしています...");
            snapshot.RegistryEntries.AddRange(_registryScanner.CaptureStartupEntries(warnings));
            snapshot.InstalledApps.AddRange(_registryScanner.CaptureInstalledApps(warnings));

            progress?.Report("ファイル関連付けと右クリックメニューをスキャンしています...");
            snapshot.FileAssociations.AddRange(_registryScanner.CaptureFileAssociations(warnings));
            snapshot.ContextMenuEntries.AddRange(_registryScanner.CaptureContextMenuEntries(warnings));

            progress?.Report("Windowsサービスをスキャンしています...");
            snapshot.Services.AddRange(_serviceScanner.CaptureServices(warnings));

            progress?.Report("スケジュールタスクをスキャンしています...");
            snapshot.ScheduledTasks.AddRange(_taskScanner.CaptureTasks(warnings, cancellationToken));

            snapshot.ScanWarnings.AddRange(warnings
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase));

            progress?.Report("スナップショット取得が完了しました。");
            return snapshot;
        }, cancellationToken);
    }
}
