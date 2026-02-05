# Migrate Scripts

データベースマイグレーション用のスクリプト集です。ダブルクリックで実行できます。

## 前提条件

- .NET SDK 9.0 以上がインストールされていること
- PostgreSQL データベースが稼働していること
- 接続文字列が `src/Game.Server/appsettings.json` または環境変数で設定されていること

## スクリプト一覧

| スクリプト | 説明 |
|-----------|------|
| `migrate-up.bat/.sh` | 保留中のマイグレーションを適用 |
| `migrate-down.bat/.sh` | 最後のマイグレーションをロールバック |
| `migrate-status.bat/.sh` | 現在のマイグレーション状態を表示 |
| `migrate-reset.bat/.sh` | データベースをリセット（全テーブル削除＋再作成） |

## 使い方

### Windows

1. エクスプローラーで `scripts/migrate/` フォルダを開く
2. 実行したい `.bat` ファイルをダブルクリック
3. コンソールウィンドウに結果が表示される

### macOS / Linux

```bash
# 実行権限を付与（初回のみ）
chmod +x scripts/migrate/*.sh

# 実行
./scripts/migrate/migrate-up.sh

# ロールバック（引数でステップ数を指定可能）
./scripts/migrate/migrate-down.sh 2
```

## オプション

### 接続文字列の指定

デフォルトでは `src/Game.Server/appsettings.json` の接続文字列を使用します。
別の接続先を指定する場合は、CLIで直接実行してください：

```bash
dotnet run --project src/Game.Tools -- migrate up --connection-string "Host=...;..."
```

### スキーマの指定

特定のスキーマのみを対象にする場合：

```bash
# masterスキーマのみ
dotnet run --project src/Game.Tools -- migrate up --schema master

# userスキーマのみ
dotnet run --project src/Game.Tools -- migrate up --schema user
```

## ワークフロー例

### 1. 開発環境セットアップ

```
1. PostgreSQL を起動
2. migrate-up.bat を実行 → スキーマ作成
3. ../seeddata/seed.bat を実行 → 初期データ投入
```

### 2. 新しいマイグレーションを作成

```
1. src/Game.Server/Database/Migrations/ に新しいマイグレーションクラスを追加
2. migrate-up.bat を実行 → マイグレーション適用
```

### 3. データベースを完全リセット

```
1. migrate-reset.bat を実行
2. 確認プロンプトで y を入力
3. シード実行の確認で y を入力（必要に応じて）
```

## トラブルシューティング

### エラー: "connection refused"

→ PostgreSQL が起動していることを確認してください。

### エラー: "authentication failed"

→ 接続文字列のユーザー名/パスワードが正しいことを確認してください。

### エラー: "relation already exists"

→ `migrate-status.bat` で現在の状態を確認し、必要に応じて `migrate-reset.bat` を実行してください。
