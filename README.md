# Install Receipt

**Install Receipt** は、アプリをインストールした前後のPC状態を比較し、増えたもの・変更されたものを「インストールレシート」として表示するWindows向けデスクトップツールです。

このプロジェクトの目的は、アプリがPCに残した足あとを、専門知識がなくても確認できるようにすることです。

> アプリがPCに残した足あとを、1枚で。

## Concept

多くのアプリは、インストール時に本体ファイルだけでなく、自動起動、バックグラウンドサービス、スケジュールタスク、設定フォルダ、キャッシュ、関連付けなどを追加します。

通常、これらの変更はユーザーから見えにくく、アンインストール後にも一部が残ることがあります。

Install Receipt は、インストール前後の状態をスナップショットとして記録し、その差分を人間向けに整理して表示します。

このツールは、セキュリティソフトでも、クリーナーでも、アンインストーラーでもありません。

**PCに何が増えたのかを見える化する透明性ツール**です。

## Core Idea

```text
インストール前の状態を記録
↓
ユーザーが通常どおりアプリをインストール
↓
インストール後の状態を記録
↓
前後の差分を抽出
↓
人間向けのレシートとして表示
```

## MVP Scope

最初のバージョンでは、AIは使用しません。

理由は、Install Receipt の基本価値が推論ではなく、正確な差分取得とわかりやすい表示にあるためです。

MVPでは以下を対象にします。

- Program Files 配下の追加
- Program Files (x86) 配下の追加
- ProgramData 配下の追加
- AppData Local / Roaming 配下の追加
- 自動起動項目
- Windowsサービス
- スケジュールタスク
- アンインストール登録
- スタートアップフォルダ
- 主要なファイル関連付け
- 右クリックメニュー関連の代表的な変更

## Non-goals

MVPでは、以下は対象外です。

- マルウェア判定
- 危険アプリの自動検出
- ワンクリック完全復元
- 完全削除
- カーネルドライバによるリアルタイム監視
- すべてのレジストリ差分の詳細表示
- AIによる安全性判定
- クラウドへの自動送信

Install Receipt は、ユーザーの代わりに断定的な判断をするのではなく、判断材料をわかりやすく提供します。

## Main Features

### 1. Installation Receipt

インストール後に、以下のようなレシートを表示します。

```text
ExampleApp のインストールレシート

追加されたもの:
- アプリ本体: 1件
- 自動起動: 1件
- Windowsサービス: 1件
- スケジュールタスク: 1件
- AppDataフォルダ: 2件

追加されなかったもの:
- ブラウザ拡張
- ドライバ
- 証明書

注意度: 中
PC起動時やバックグラウンド動作に関係する変更があります。
```

### 2. Startup Detection

以下のような自動起動項目の追加を検出します。

- Registry Run keys
- Startup folder
- その他、代表的なログオン時起動設定

表示例:

```text
自動起動に追加されました

ExampleApp
PC起動時に自動で起動します。
```

### 3. Windows Service Detection

新しく追加されたWindowsサービスを検出します。

表示例:

```text
サービスが追加されました

Example Update Service
起動方法: 自動
場所: C:\Program Files\ExampleApp\updater.exe
```

### 4. Scheduled Task Detection

インストールによって追加されたスケジュールタスクを検出します。

表示例:

```text
定期実行タスクが追加されました

ExampleApp Update Check
毎日 10:00 に更新確認を行います。
```

### 5. AppData and ProgramData Footprints

設定、キャッシュ、ログなどの保存先候補を表示します。

表示例:

```text
アンインストール後に残る可能性があります

設定:
%APPDATA%\ExampleApp

キャッシュ:
%LOCALAPPDATA%\ExampleApp\Cache
```

### 6. Receipt Export

レシートは以下の形式で保存できるようにします。

- JSON
- HTML
- Markdown

将来的にはPDF出力も検討します。

## Architecture

```text
Install Receipt
├─ Snapshot Engine
│  ├─ File Scanner
│  ├─ Registry Scanner
│  ├─ Service Scanner
│  ├─ Task Scheduler Scanner
│  └─ App List Scanner
│
├─ Diff Engine
│  ├─ Added Items
│  ├─ Modified Items
│  └─ Removed Items
│
├─ Classifier
│  ├─ Application Files
│  ├─ Startup Entries
│  ├─ Services
│  ├─ Scheduled Tasks
│  ├─ AppData Footprints
│  ├─ Uninstall Entries
│  └─ Noise Filtering
│
├─ Receipt Renderer
│  ├─ Summary
│  ├─ Details
│  ├─ Attention Level
│  └─ Export
│
└─ Desktop UI
```

## Snapshot Model

スナップショットは、インストール前後のPC状態を保存するためのデータです。

### File Snapshot

保存する主な情報:

- path
- size
- created_at
- modified_at
- extension
- file_type
- signer
- hash optional

ファイルの中身は原則として読みません。

### Registry Snapshot

保存する主な情報:

- key_path
- value_name
- value_data
- value_type

MVPでは全レジストリを対象にせず、インストールに関係しやすい代表的な場所に絞ります。

### Service Snapshot

保存する主な情報:

- service_name
- display_name
- executable_path
- start_type
- status
- signer

### Scheduled Task Snapshot

保存する主な情報:

- task_name
- executable_path
- arguments
- triggers
- enabled

### App List Snapshot

保存する主な情報:

- display_name
- publisher
- version
- install_location
- uninstall_command
- install_date

## Important Windows Locations

### Application Files

```text
C:\Program Files
C:\Program Files (x86)
C:\ProgramData
```

### User Data

```text
%APPDATA%
%LOCALAPPDATA%
```

### Startup

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
HKLM\Software\Microsoft\Windows\CurrentVersion\Run
shell:startup
shell:common startup
```

### Services

```text
HKLM\SYSTEM\CurrentControlSet\Services
```

または Service Control Manager API を使用します。

### Scheduled Tasks

```text
C:\Windows\System32\Tasks
```

または Task Scheduler API を使用します。

### Uninstall Entries

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall
HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall
HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall
```

## Classification Rules

Install Receipt は、AIではなくルールベースで変更を分類します。

例:

```text
Program Files 配下の新規フォルダ
→ アプリ本体

AppData\Roaming 配下の新規フォルダ
→ 設定データの可能性

AppData\Local 配下で Cache / Temp / Logs を含む
→ キャッシュまたはログの可能性

Run key に追加
→ 自動起動

Services に追加
→ Windowsサービス

Scheduled Tasks に追加
→ 定期実行タスク

Uninstall に追加
→ アプリ登録
```

## Attention Level

注意度は、断定的な安全・危険判定ではありません。

PC全体への影響がどれくらいありそうかを、ユーザーが確認しやすくするための目安です。

例:

```text
アプリ登録のみ                  +1
AppData追加                    +1
自動起動追加                   +2
スケジュールタスク追加          +2
ファイル関連付け変更            +2
Windowsサービス追加             +3
ブラウザ拡張追加                +4
ドライバ追加                    +5
証明書追加                      +5
```

表示例:

```text
注意度: 低
アプリ本体と設定フォルダが追加されています。

注意度: 中
PC起動時やバックグラウンド動作に関係する変更があります。

注意度: 高
PC全体に影響する変更があります。内容を確認してください。
```

## Privacy Principles

Install Receipt は、信頼されるために以下の原則を守ります。

- AIなしで動作する
- クラウド送信なしで動作する
- ファイル本文は読まない
- 個人ファイルの中身を解析しない
- 収集対象を明示する
- 変更内容の判定理由を表示する
- 自動削除や自動復元は行わない

## Suggested Tech Stack

### Windows Desktop App

候補:

- C#
- .NET
- WPF or WinUI 3

理由:

- Windows APIとの相性が良い
- レジストリ操作がしやすい
- サービス一覧を扱いやすい
- タスクスケジューラ連携がしやすい
- 配布しやすい

### Data Storage

候補:

- SQLite
- JSON files for early MVP

保存対象:

- snapshot
- diff result
- receipt
- app metadata
- user settings

## Suggested Repository Structure

```text
install-receipt/
├─ README.md
├─ LICENSE
├─ docs/
│  ├─ product.md
│  ├─ architecture.md
│  ├─ privacy.md
│  └─ roadmap.md
│
├─ src/
│  ├─ InstallReceipt.App/
│  ├─ InstallReceipt.Core/
│  ├─ InstallReceipt.Snapshot/
│  ├─ InstallReceipt.Diff/
│  ├─ InstallReceipt.Classification/
│  ├─ InstallReceipt.Rendering/
│  └─ InstallReceipt.Platform.Windows/
│
├─ tests/
│  ├─ InstallReceipt.Core.Tests/
│  ├─ InstallReceipt.Diff.Tests/
│  └─ InstallReceipt.Classification.Tests/
│
└─ samples/
   ├─ receipts/
   └─ snapshots/
```

## Development Status

This project is currently in the planning / MVP design stage.

Initial implementation goals:

- [ ] Create snapshot model
- [ ] Implement file scanner
- [ ] Implement registry scanner for startup and uninstall entries
- [ ] Implement service scanner
- [ ] Implement scheduled task scanner
- [ ] Implement diff engine
- [ ] Implement rule-based classifier
- [ ] Implement receipt renderer
- [ ] Build minimal desktop UI
- [ ] Add HTML export
- [ ] Add JSON export

## Roadmap

### Phase 1: Local MVP

- Manual start / stop monitoring
- Before / after snapshots
- Basic diff view
- Startup detection
- Service detection
- Scheduled task detection
- Program Files and AppData detection
- JSON / HTML receipt export

### Phase 2: Better Receipts

- Noise filtering
- Better grouping
- App name / publisher matching
- Digital signature display
- Past receipt history
- Searchable receipt library
- Markdown export

### Phase 3: Uninstall Receipt

- Compare install receipt and post-uninstall state
- Show removed items
- Show remaining files and folders
- Suggest cleanup candidates without automatic deletion

### Phase 4: Pro Features

- Receipt comparison
- Multiple PC history
- Team export
- Policy checks
- Optional AI explanation and summarization

## Future AI Features

The core product should remain useful without AI.

AI may be added later as an optional Pro feature, focused on explanation rather than judgment.

Good AI use cases:

- Summarize a long receipt
- Explain what a service or task probably does
- Highlight items worth reviewing
- Translate technical changes into beginner-friendly language
- Compare multiple receipts
- Generate an uninstall checklist

Avoided AI use cases:

- Declaring an app safe or dangerous
- Replacing antivirus software
- Automatic cleanup decisions
- Sending local file content to the cloud

Positioning:

```text
Basic:
AIなし。PCに増えたものを正確に見える化。

Pro:
AIがレシートを読み解き、注意点と次の操作をわかりやすく提案。
```

## Example Receipt JSON

```json
{
  "app": {
    "name": "ExampleApp",
    "publisher": "Example Inc.",
    "version": "1.2.3"
  },
  "summary": {
    "attention_level": "medium",
    "added_files": 1284,
    "added_size_mb": 342,
    "startup_entries": 1,
    "services": 1,
    "scheduled_tasks": 1,
    "appdata_locations": 2
  },
  "items": [
    {
      "type": "startup",
      "name": "ExampleApp",
      "path": "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run",
      "description": "PC起動時に自動で起動します。"
    },
    {
      "type": "service",
      "name": "Example Update Service",
      "path": "C:\\Program Files\\ExampleApp\\updater.exe",
      "description": "バックグラウンドで更新確認を行う可能性があります。"
    }
  ]
}
```

## UX Principles

- 怖がらせない
- 断定しすぎない
- 専門用語を最初に出さない
- 重要な変更から見せる
- 生データは折りたたむ
- 「追加されなかったもの」も表示して安心感を出す
- すぐ削除させるのではなく、まず理解させる

## Product Positioning

Install Receipt is not:

- an antivirus
- a cleaner
- a full uninstaller
- a malware detector
- a registry diff tool for experts

Install Receipt is:

- an installation transparency tool
- a PC change receipt generator
- a lightweight app footprint viewer
- a local-first utility for people who want to understand their computer

## License

TBD.

## Name Ideas

Primary:

- Install Receipt

Alternative:

- App Footprint
- AfterInstall
- Install Ledger
- 足あとレシート

## Tagline Ideas

- アプリがPCに残した足あとを、1枚で。
- 入れたあとに、何が増えたか分かる。
- AIなし。クラウドなし。PCの変化を見える化。
- 判断は透明に。説明はわかりやすく。
