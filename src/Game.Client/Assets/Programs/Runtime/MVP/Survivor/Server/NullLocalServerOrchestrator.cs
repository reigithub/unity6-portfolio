using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shared.Unity.Server;

namespace Game.MVP.Survivor.Server
{
    /// <summary>
    /// サーバー用 Null 実装。全メソッド no-op。
    /// </summary>
    public class NullLocalServerOrchestrator : ILocalServerOrchestrator
    {
        public bool IsReady => false;
        public ushort HeadlessServerPort => 7777;

        public UniTask StartAsync(CancellationToken ct = default)
        {
            return UniTask.CompletedTask;
        }

        public void Dispose() { }
    }
}
