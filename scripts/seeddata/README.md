# SeedData Scripts

データベースシード用のスクリプト集です。ダブルクリックで実行できます。

## 前提条件

- .NET SDK 9.0 以上がインストールされていること
- PostgreSQL データベースが稼働していること
- マイグレーションが適用済みであること（`scripts/migrate/migrate-up.bat` を先に実行）

## スクリプト一覧

| スクリプト | 説明 |
|-----------|------|
| `seed.bat/.sh` | TSVファイルからデータベースにシード |
| `dump.bat/.sh` | データベースからTSVファイルにダンプ |
| `diff.bat/.sh` | 2つのTSVディレクトリを比較 |

**入出力:**
- Seed元: `masterdata/raw/*.tsv`
- Dump先: `masterdata/dump/*.tsv`

## 使い方

### Windows

1. エクスプローラーで `scripts/seeddata/` フォルダを開く
2. 実行したい `.bat` ファイルをダブルクリック
3. コンソールウィンドウに結果が表示される

### macOS / Linux

```bash
# 実行権限を付与（初回のみ）
chmod +x scripts/seeddata/*.sh

# シード実行
./scripts/seeddata/seed.sh

# ダンプ実行
./scripts/seeddata/dump.sh

# 差分確認（引数でディレクトリ指定可能）
./scripts/seeddata/diff.sh masterdata/raw/ masterdata/dump/
```

## ワークフロー例

### 1. 初期データ投入

```
1. マイグレーション適用済みを確認
2. seed.bat を実行 → masterdata/raw/ のTSVをDBに投入
```

### 2. 現在のDBデータをエクスポート

```
1. dump.bat を実行 → masterdata/dump/ にTSVファイル出力
2. 必要に応じて masterdata/raw/ にコピー
```

### 3. ラウンドトリップ検証

TSV → DB → TSV の整合性を確認：

```
1. seed.bat を実行 → TSVからDBにシード
2. dump.bat を実行 → DBからTSVにダンプ
3. diff.bat を実行 → 元TSVとダンプTSVを比較
```

## オプション

### 接続文字列の指定

```bash
dotnet run --project src/Game.Tools -- seeddata seed --connection-string "Host=...;..."
```

### スキーマの指定

```bash
# masterスキーマのみ
dotnet run --project src/Game.Tools -- seeddata seed --schema master

# userスキーマのみ
dotnet run --project src/Game.Tools -- seeddata seed --schema user
```

### カスタムディレクトリの指定

```bash
# シード元を指定
dotnet run --project src/Game.Tools -- seeddata seed --tsv-dir custom/data/

# ダンプ先を指定
dotnet run --project src/Game.Tools -- seeddata dump --out-dir custom/output/
```

## トラブルシューティング

### エラー: "No tables to seed"

→ マイグレーションが適用されていません。先に `migrate-up.bat` を実行してください。

### エラー: "TSV not found"

→ `masterdata/raw/` に対応するTSVファイルが存在するか確認してください。

### 差分がある場合

`diff.bat` で差分が検出された場合：
- カラム追加/削除があった場合は正常
- データ型の変換による差異（日付フォーマット等）は許容範囲
- 予期しない値の変化は要調査
