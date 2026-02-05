# MasterData Scripts

マスターデータ管理用のスクリプト集です。ダブルクリックで実行できます。

## 前提条件

- .NET SDK 9.0 以上がインストールされていること
- プロジェクトルートで `dotnet restore` が完了していること

## スクリプト一覧

### バイナリ生成

| スクリプト | 説明 |
|-----------|------|
| `build-client.bat/.sh` | クライアント用バイナリを生成 |
| `build-server.bat/.sh` | サーバー用バイナリを生成 |
| `build-all.bat/.sh` | クライアント＋サーバー両方を生成 |

**出力先:**
- Client: `src/Game.Client/Assets/MasterData/MasterDataBinary.bytes`
- Server: `src/Game.Server/MasterData/masterdata.bytes`

### コード生成

| スクリプト | 説明 |
|-----------|------|
| `codegen.bat/.sh` | ProtoスキーマからC# MemoryTableクラスを生成 |

**出力先:**
- Client: `src/Game.Client/Assets/Programs/Runtime/Shared/MasterData/*.Generated.cs`
- Server: `src/Game.Server/MasterData/*.Generated.cs`

### 検証・エクスポート

| スクリプト | 説明 |
|-----------|------|
| `validate.bat/.sh` | TSVファイルをProtoスキーマに対して検証 |
| `export-json.bat/.sh` | バイナリをJSON形式でエクスポート |

## 使い方

### Windows

1. エクスプローラーで `scripts/masterdata/` フォルダを開く
2. 実行したい `.bat` ファイルをダブルクリック
3. コンソールウィンドウに結果が表示される

### macOS / Linux

```bash
# 実行権限を付与（初回のみ）
chmod +x scripts/masterdata/*.sh

# 実行
./scripts/masterdata/build-all.sh
```

## ワークフロー例

### 1. 新しいマスターテーブルを追加する場合

```
1. masterdata/proto/ に .proto ファイルを作成
2. codegen.bat を実行 → C#クラス生成
3. Unity/Visual Studioでビルド
4. masterdata/raw/ に TSVファイルを作成
5. validate.bat を実行 → TSV検証
6. build-all.bat を実行 → バイナリ生成
```

### 2. 既存データを更新する場合

```
1. masterdata/raw/*.tsv を編集
2. validate.bat を実行 → TSV検証（オプション）
3. build-all.bat を実行 → バイナリ生成
```

### 3. スキーマを変更する場合

```
1. masterdata/proto/*.proto を編集
2. codegen.bat を実行 → C#クラス再生成
3. Unity/Visual Studioでビルド
4. masterdata/raw/*.tsv を必要に応じて更新
5. build-all.bat を実行 → バイナリ生成
```

## 直接CLIを使う場合

```bash
# ヘルプ表示
dotnet run --project src/Game.Tools -- masterdata --help

# 個別コマンドのヘルプ
dotnet run --project src/Game.Tools -- masterdata build --help
```

## トラブルシューティング

### エラー: "type not found in assembly"

→ `codegen` 実行後、Unity/Visual Studioでプロジェクトをビルドしてください。

### エラー: "TSV not found"

→ `masterdata/raw/` に対応するTSVファイルが存在するか確認してください。

### エラー: "protoc not found"

→ `dotnet restore` を実行してGoogle.Protobuf.Toolsパッケージを復元してください。
