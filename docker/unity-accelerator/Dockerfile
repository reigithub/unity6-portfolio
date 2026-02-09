# Unity Accelerator with custom entrypoint
#
# 公式イメージをベースに、以下を追加:
# - ディレクトリ初期化処理
# - パーミッション設定
#
# Note: ベースイメージのパッケージリストが古いため curl インストール不可
#       ヘルスチェックは Runner からの接続で代替

FROM unitytechnologies/accelerator:latest

# カスタムエントリポイントをコピー
COPY entrypoint.sh /custom-entrypoint.sh
RUN chmod +x /custom-entrypoint.sh

ENTRYPOINT ["/custom-entrypoint.sh"]
