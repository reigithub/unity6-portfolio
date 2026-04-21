using System.Threading.Tasks;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// バックグラウンドスレッドからメインスレッドへ渡すセッション作成リクエスト。
    /// <see cref="IUnityServerHttpListener"/> の POST /session/start エンドポイントが受け取ったデータを保持する。
    /// </summary>
    public sealed class UnityServerSessionRequest
    {
        /// <summary>Fusion セッション名（SessionName）。</summary>
        public string SessionName;

        /// <summary>ステージID。</summary>
        public int StageId;

        /// <summary>期待プレイヤー数。</summary>
        public int ExpectedPlayers;

        /// <summary>
        /// メインスレッドが処理完了後に SetResult を呼ぶことで HTTP レスポンスを返す。
        /// </summary>
        public TaskCompletionSource<bool> CompletionSource = new TaskCompletionSource<bool>();
    }
}
