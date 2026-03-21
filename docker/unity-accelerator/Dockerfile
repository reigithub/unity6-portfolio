# Unity Accelerator with custom entrypoint
#
# 公式イメージをベースに、以下を追加:
# - ディレクトリ初期化処理
# - パーミッション設定
#
# ベースイメージ: https://hub.docker.com/r/unity/accelerator
# ※ unitytechnologies/accelerator は非推奨（2022年以降更新なし）

FROM unity/accelerator:v1.5.137-g4eddaf6a

# カスタムエントリポイントをコピー
COPY entrypoint.sh /custom-entrypoint.sh
RUN chmod +x /custom-entrypoint.sh

ENTRYPOINT ["/custom-entrypoint.sh"]
