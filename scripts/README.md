# Scripts

プロジェクト管理用のスクリプト集です。

## ディレクトリ構成

| ディレクトリ | 説明 |
|-------------|------|
| `masterdata/` | マスターデータ管理（バイナリ生成、コード生成、検証） |
| `migrate/` | データベースマイグレーション管理 |
| `seeddata/` | データベースシード管理（TSV ↔ DB同期） |

## クイックスタート

### Windows

エクスプローラーで各フォルダを開き、`.bat` ファイルをダブルクリックして実行。

### macOS / Linux

```bash
# 実行権限を付与（初回のみ）
chmod +x scripts/**/*.sh

# 例: マスターデータビルド
./scripts/masterdata/build-all.sh

# 例: マイグレーション適用
./scripts/migrate/migrate-up.sh

# 例: シードデータ投入
./scripts/seeddata/seed.sh
```

## Unity Editorからの実行

Unity Editorのメニューからも同様の操作が可能です：

- **Project > MasterMemory > MasterDataWindow** - マスターデータ管理
- **Project > Database > DatabaseWindow** - マイグレーション・シード管理

## 詳細ドキュメント

各ディレクトリ内のREADME.mdを参照してください：

- [masterdata/README.md](masterdata/README.md)
- [migrate/README.md](migrate/README.md)
- [seeddata/README.md](seeddata/README.md)
