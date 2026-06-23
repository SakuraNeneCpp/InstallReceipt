using System.Net;
using System.Text;
using System.Text.Json;
using InstallReceipt.Core.Models;
using InstallReceipt.Core.Persistence;

namespace InstallReceipt.Rendering;

public sealed class ReceiptRenderer
{
    public string RenderJson(InstallReceiptDocument receipt)
    {
        return JsonSerializer.Serialize(receipt, SnapshotJsonStore.CreateOptions());
    }

    public string RenderMarkdown(InstallReceiptDocument receipt)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {receipt.App.Name} のインストールレシート");
        builder.AppendLine();
        builder.AppendLine($"作成日時: {receipt.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine();
        builder.AppendLine("## 概要");
        builder.AppendLine();
        builder.AppendLine($"- 注意度: {ToJapanese(receipt.Summary.AttentionLevel)}");
        builder.AppendLine($"- 注意スコア: {receipt.Summary.AttentionScore}");
        builder.AppendLine($"- 追加ファイル/フォルダ: {receipt.Summary.AddedFiles}件");
        builder.AppendLine($"- 追加サイズ: {FormatBytes(receipt.Summary.AddedSizeBytes)}");
        builder.AppendLine($"- 自動起動: {receipt.Summary.StartupEntries}件");
        builder.AppendLine($"- Windowsサービス: {receipt.Summary.Services}件");
        builder.AppendLine($"- スケジュールタスク: {receipt.Summary.ScheduledTasks}件");
        builder.AppendLine($"- AppData候補: {receipt.Summary.AppDataLocations}件");
        builder.AppendLine($"- アンインストール登録: {receipt.Summary.InstalledApps}件");
        builder.AppendLine($"- ファイル関連付け: {receipt.Summary.FileAssociations}件");
        builder.AppendLine($"- 右クリックメニュー: {receipt.Summary.ContextMenuEntries}件");
        builder.AppendLine();
        builder.AppendLine(ToAttentionMessage(receipt.Summary.AttentionLevel));
        builder.AppendLine();

        builder.AppendLine("## 追加・変更されたもの");
        builder.AppendLine();
        if (receipt.Items.Count == 0)
        {
            builder.AppendLine("差分は検出されませんでした。");
        }
        else
        {
            foreach (var item in receipt.Items)
            {
                builder.AppendLine($"### {item.Name}");
                builder.AppendLine();
                builder.AppendLine($"- 種類: {item.Type}");
                builder.AppendLine($"- 場所: `{item.Path}`");
                builder.AppendLine($"- 説明: {item.Description}");
                builder.AppendLine($"- 判定理由: {item.Reason}");
                builder.AppendLine();
            }
        }

        builder.AppendLine("## 追加されなかったもの");
        builder.AppendLine();
        if (receipt.NotDetected.Count == 0)
        {
            builder.AppendLine("代表カテゴリでは未検出項目はありません。");
        }
        else
        {
            foreach (var item in receipt.NotDetected)
            {
                builder.AppendLine($"- {item}");
            }
        }

        if (receipt.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## スキャン警告");
            builder.AppendLine();
            foreach (var warning in receipt.Warnings)
            {
                builder.AppendLine($"- {warning}");
            }
        }

        return builder.ToString();
    }

    public string RenderHtml(InstallReceiptDocument receipt)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"ja\">");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\">");
        builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine($"  <title>{Escape(receipt.App.Name)} のインストールレシート</title>");
        builder.AppendLine("  <style>");
        builder.AppendLine("    :root { color-scheme: light; font-family: 'Segoe UI', system-ui, sans-serif; }");
        builder.AppendLine("    body { margin: 0; background: #f6f7f8; color: #1f2328; }");
        builder.AppendLine("    main { max-width: 1080px; margin: 0 auto; padding: 32px 20px 48px; }");
        builder.AppendLine("    h1, h2, h3 { margin: 0 0 12px; }");
        builder.AppendLine("    p { line-height: 1.6; }");
        builder.AppendLine("    .summary, .item, .warnings { background: white; border: 1px solid #d8dee4; border-radius: 8px; padding: 16px; margin: 16px 0; }");
        builder.AppendLine("    .summary-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 8px; }");
        builder.AppendLine("    .metric { background: #f6f7f8; border-radius: 6px; padding: 10px; }");
        builder.AppendLine("    .metric strong { display: block; font-size: 1.2rem; }");
        builder.AppendLine("    code { word-break: break-all; }");
        builder.AppendLine("    .tag { display: inline-block; border-radius: 999px; padding: 2px 8px; background: #eaeef2; font-size: 0.85rem; }");
        builder.AppendLine("  </style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("  <main>");
        builder.AppendLine($"    <h1>{Escape(receipt.App.Name)} のインストールレシート</h1>");
        builder.AppendLine($"    <p>作成日時: {receipt.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}</p>");
        builder.AppendLine("    <section class=\"summary\">");
        builder.AppendLine("      <h2>概要</h2>");
        builder.AppendLine($"      <p><span class=\"tag\">注意度: {Escape(ToJapanese(receipt.Summary.AttentionLevel))}</span> {Escape(ToAttentionMessage(receipt.Summary.AttentionLevel))}</p>");
        builder.AppendLine("      <div class=\"summary-grid\">");
        AppendMetric(builder, "追加ファイル/フォルダ", $"{receipt.Summary.AddedFiles}件");
        AppendMetric(builder, "追加サイズ", FormatBytes(receipt.Summary.AddedSizeBytes));
        AppendMetric(builder, "自動起動", $"{receipt.Summary.StartupEntries}件");
        AppendMetric(builder, "Windowsサービス", $"{receipt.Summary.Services}件");
        AppendMetric(builder, "スケジュールタスク", $"{receipt.Summary.ScheduledTasks}件");
        AppendMetric(builder, "AppData候補", $"{receipt.Summary.AppDataLocations}件");
        AppendMetric(builder, "アンインストール登録", $"{receipt.Summary.InstalledApps}件");
        AppendMetric(builder, "ファイル関連付け", $"{receipt.Summary.FileAssociations}件");
        AppendMetric(builder, "右クリックメニュー", $"{receipt.Summary.ContextMenuEntries}件");
        builder.AppendLine("      </div>");
        builder.AppendLine("    </section>");

        builder.AppendLine("    <section>");
        builder.AppendLine("      <h2>追加・変更されたもの</h2>");
        if (receipt.Items.Count == 0)
        {
            builder.AppendLine("      <p>差分は検出されませんでした。</p>");
        }
        else
        {
            foreach (var item in receipt.Items)
            {
                builder.AppendLine("      <article class=\"item\">");
                builder.AppendLine($"        <h3>{Escape(item.Name)}</h3>");
                builder.AppendLine($"        <p><span class=\"tag\">{Escape(item.Type)}</span></p>");
                builder.AppendLine($"        <p>{Escape(item.Description)}</p>");
                builder.AppendLine($"        <p><strong>場所:</strong> <code>{Escape(item.Path)}</code></p>");
                builder.AppendLine($"        <p><strong>判定理由:</strong> {Escape(item.Reason)}</p>");
                builder.AppendLine("      </article>");
            }
        }
        builder.AppendLine("    </section>");

        builder.AppendLine("    <section>");
        builder.AppendLine("      <h2>追加されなかったもの</h2>");
        builder.AppendLine("      <ul>");
        foreach (var item in receipt.NotDetected)
        {
            builder.AppendLine($"        <li>{Escape(item)}</li>");
        }
        builder.AppendLine("      </ul>");
        builder.AppendLine("    </section>");

        if (receipt.Warnings.Count > 0)
        {
            builder.AppendLine("    <section class=\"warnings\">");
            builder.AppendLine("      <h2>スキャン警告</h2>");
            builder.AppendLine("      <ul>");
            foreach (var warning in receipt.Warnings)
            {
                builder.AppendLine($"        <li>{Escape(warning)}</li>");
            }
            builder.AppendLine("      </ul>");
            builder.AppendLine("    </section>");
        }

        builder.AppendLine("  </main>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static void AppendMetric(StringBuilder builder, string label, string value)
    {
        builder.AppendLine("        <div class=\"metric\">");
        builder.AppendLine($"          <span>{Escape(label)}</span>");
        builder.AppendLine($"          <strong>{Escape(value)}</strong>");
        builder.AppendLine("        </div>");
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

    private static string ToJapanese(AttentionLevel level)
    {
        return level switch
        {
            AttentionLevel.High => "高",
            AttentionLevel.Medium => "中",
            _ => "低"
        };
    }

    private static string ToAttentionMessage(AttentionLevel level)
    {
        return level switch
        {
            AttentionLevel.High => "PC全体に影響する変更があります。内容を確認してください。",
            AttentionLevel.Medium => "PC起動時やバックグラウンド動作に関係する変更があります。",
            _ => "アプリ本体と設定フォルダが中心の変更です。"
        };
    }

    private static string Escape(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
