using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using InstallReceipt.Classification;
using InstallReceipt.Core.Models;
using InstallReceipt.Core.Persistence;
using InstallReceipt.Diff;
using InstallReceipt.Platform.Windows;
using InstallReceipt.Rendering;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace InstallReceipt.App;

public partial class MainWindow : Window
{
    private readonly WindowsSnapshotService _snapshotService = new();
    private readonly SnapshotDiffer _differ = new();
    private readonly RuleBasedClassifier _classifier = new();
    private readonly ReceiptRenderer _renderer = new();

    private Snapshot? _beforeSnapshot;
    private Snapshot? _afterSnapshot;
    private InstallReceiptDocument? _receipt;

    public MainWindow()
    {
        InitializeComponent();
        ApplyTheme(useDarkMode: false);
    }

    private void DarkModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        ApplyTheme(DarkModeToggle.IsChecked == true);
    }

    private async void CaptureBefore_Click(object sender, RoutedEventArgs e)
    {
        await RunWithUiGuardAsync(async () =>
        {
            _beforeSnapshot = await CaptureSnapshotAsync("before");
            _afterSnapshot = null;
            _receipt = null;
            RenderReceipt();
            StatusTextBlock.Text = $"事前スナップショットを保存しました: {_beforeSnapshot.CreatedAt:yyyy-MM-dd HH:mm:ss}";
        });
    }

    private async void CaptureAfter_Click(object sender, RoutedEventArgs e)
    {
        if (_beforeSnapshot is null)
        {
            MessageBox.Show(this, "先に事前スナップショットを取得するか、既存の事前スナップショットを読み込んでください。", "事前スナップショットが必要です", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await RunWithUiGuardAsync(async () =>
        {
            _afterSnapshot = await CaptureSnapshotAsync("after");
            BuildReceipt();
            StatusTextBlock.Text = $"差分を作成しました: {_receipt?.Items.Count ?? 0}件";
        });
    }

    private async void LoadBefore_Click(object sender, RoutedEventArgs e)
    {
        var fileName = PickSnapshotFile();
        if (fileName is null)
        {
            return;
        }

        await RunWithUiGuardAsync(async () =>
        {
            _beforeSnapshot = await SnapshotJsonStore.LoadSnapshotAsync(fileName);
            _afterSnapshot = null;
            _receipt = null;
            RenderReceipt();
            StatusTextBlock.Text = $"事前スナップショットを読み込みました: {fileName}";
        });
    }

    private async void LoadAfter_Click(object sender, RoutedEventArgs e)
    {
        if (_beforeSnapshot is null)
        {
            MessageBox.Show(this, "先に事前スナップショットを取得するか、既存の事前スナップショットを読み込んでください。", "事前スナップショットが必要です", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var fileName = PickSnapshotFile();
        if (fileName is null)
        {
            return;
        }

        await RunWithUiGuardAsync(async () =>
        {
            _afterSnapshot = await SnapshotJsonStore.LoadSnapshotAsync(fileName);
            BuildReceipt();
            StatusTextBlock.Text = $"差分を作成しました: {_receipt?.Items.Count ?? 0}件";
        });
    }

    private async void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        await ExportReceiptAsync("JSONレシート (*.json)|*.json", "json", receipt => _renderer.RenderJson(receipt));
    }

    private async void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        await ExportReceiptAsync("HTMLレシート (*.html)|*.html", "html", receipt => _renderer.RenderHtml(receipt));
    }

    private async void ExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        await ExportReceiptAsync("Markdownレシート (*.md)|*.md", "md", receipt => _renderer.RenderMarkdown(receipt));
    }

    private async Task<Snapshot> CaptureSnapshotAsync(string role)
    {
        var progress = new Progress<string>(message => StatusTextBlock.Text = message);
        var snapshot = await _snapshotService.CaptureAsync(AppHintTextBox.Text, progress);
        var fileName = Path.Combine(
            SnapshotDirectory,
            $"{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{role}.snapshot.json");

        await SnapshotJsonStore.SaveSnapshotAsync(snapshot, fileName);
        return snapshot;
    }

    private void BuildReceipt()
    {
        if (_beforeSnapshot is null || _afterSnapshot is null)
        {
            return;
        }

        var diff = _differ.Compare(_beforeSnapshot, _afterSnapshot);
        _receipt = _classifier.CreateReceipt(diff);
        RenderReceipt();
    }

    private void RenderReceipt()
    {
        if (_receipt is null)
        {
            AttentionTextBlock.Text = "-";
            AddedFilesTextBlock.Text = "-";
            StartupTextBlock.Text = "-";
            ServicesTextBlock.Text = "-";
            TasksTextBlock.Text = "-";
            AppDataTextBlock.Text = "-";
            InstalledAppsTextBlock.Text = "-";
            AssociationsTextBlock.Text = "-";
            ContextMenuTextBlock.Text = "-";
            SizeTextBlock.Text = "-";
            NotDetectedItemsControl.ItemsSource = null;
            WarningsTextBox.Text = string.Empty;
            ReceiptItemsListView.ItemsSource = null;
            UpdateExportButtons();
            return;
        }

        AttentionTextBlock.Text = $"{ToJapanese(_receipt.Summary.AttentionLevel)} ({_receipt.Summary.AttentionScore})";
        AddedFilesTextBlock.Text = _receipt.Summary.AddedFiles.ToString("N0");
        StartupTextBlock.Text = _receipt.Summary.StartupEntries.ToString("N0");
        ServicesTextBlock.Text = _receipt.Summary.Services.ToString("N0");
        TasksTextBlock.Text = _receipt.Summary.ScheduledTasks.ToString("N0");
        AppDataTextBlock.Text = _receipt.Summary.AppDataLocations.ToString("N0");
        InstalledAppsTextBlock.Text = _receipt.Summary.InstalledApps.ToString("N0");
        AssociationsTextBlock.Text = _receipt.Summary.FileAssociations.ToString("N0");
        ContextMenuTextBlock.Text = _receipt.Summary.ContextMenuEntries.ToString("N0");
        SizeTextBlock.Text = FormatBytes(_receipt.Summary.AddedSizeBytes);
        NotDetectedItemsControl.ItemsSource = _receipt.NotDetected;
        WarningsTextBox.Text = _receipt.Warnings.Count == 0 ? "なし" : string.Join(Environment.NewLine, _receipt.Warnings);
        ReceiptItemsListView.ItemsSource = _receipt.Items;
        UpdateExportButtons();
    }

    private async Task ExportReceiptAsync(string filter, string extension, Func<InstallReceiptDocument, string> render)
    {
        if (_receipt is null)
        {
            return;
        }

        var dialog = new WpfSaveFileDialog
        {
            Filter = filter,
            FileName = $"{SafeFileName(_receipt.App.Name)}-install-receipt.{extension}",
            InitialDirectory = ReceiptDirectory
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await RunWithUiGuardAsync(async () =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dialog.FileName) ?? ".");
            await File.WriteAllTextAsync(dialog.FileName, render(_receipt), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            StatusTextBlock.Text = $"レシートを保存しました: {dialog.FileName}";
        });
    }

    private string? PickSnapshotFile()
    {
        var dialog = new WpfOpenFileDialog
        {
            Filter = "Snapshot JSON (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = SnapshotDirectory,
            CheckFileExists = true
        };

        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private async Task RunWithUiGuardAsync(Func<Task> action)
    {
        SetBusy(true);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Install Receipt", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusTextBlock.Text = "処理に失敗しました。";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        BusyProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        CaptureBeforeButton.IsEnabled = !busy;
        CaptureAfterButton.IsEnabled = !busy;
        LoadBeforeButton.IsEnabled = !busy;
        LoadAfterButton.IsEnabled = !busy;
        UpdateExportButtons(!busy);
    }

    private void UpdateExportButtons(bool canInteract = true)
    {
        var canExport = canInteract && _receipt is not null;
        ExportJsonButton.IsEnabled = canExport;
        ExportHtmlButton.IsEnabled = canExport;
        ExportMarkdownButton.IsEnabled = canExport;
    }

    private void ApplyTheme(bool useDarkMode)
    {
        if (useDarkMode)
        {
            SetThemeBrush("AppBackgroundBrush", "#101214");
            SetThemeBrush("PanelBackgroundBrush", "#171A1D");
            SetThemeBrush("PanelBorderBrush", "#2A2F35");
            SetThemeBrush("PrimaryTextBrush", "#F3F4F6");
            SetThemeBrush("SecondaryTextBrush", "#A9B1BA");
            SetThemeBrush("InputBackgroundBrush", "#111417");
            SetThemeBrush("InputBorderBrush", "#3A4149");
            SetThemeBrush("ButtonBackgroundBrush", "#202428");
            SetThemeBrush("ListHeaderBackgroundBrush", "#202428");
            return;
        }

        SetThemeBrush("AppBackgroundBrush", "#F6F7F8");
        SetThemeBrush("PanelBackgroundBrush", "#FFFFFF");
        SetThemeBrush("PanelBorderBrush", "#D8DEE4");
        SetThemeBrush("PrimaryTextBrush", "#1F2328");
        SetThemeBrush("SecondaryTextBrush", "#57606A");
        SetThemeBrush("InputBackgroundBrush", "#FFFFFF");
        SetThemeBrush("InputBorderBrush", "#D0D7DE");
        SetThemeBrush("ButtonBackgroundBrush", "#FFFFFF");
        SetThemeBrush("ListHeaderBackgroundBrush", "#F6F8FA");
    }

    private void SetThemeBrush(string resourceKey, string color)
    {
        Resources[resourceKey] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private static string ToJapanese(AttentionLevel level)
    {
        return level switch
        {
            AttentionLevel.High => "高",
            AttentionLevel.Medium => "中",
            _ => "低"
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)Math.Max(0, bytes);
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    private static string SafeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "install-receipt" : safe;
    }

    private static string SnapshotDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InstallReceipt",
        "snapshots");

    private static string ReceiptDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InstallReceipt",
        "receipts");
}
