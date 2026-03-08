using Cysharp.Threading.Tasks;
using Game.Shared.Network.Survivor;

namespace Game.MVP.Survivor.Server
{
    /// <summary>
    /// サーバー用Null実装。全メソッドno-op。
    /// </summary>
    public class NullSurvivorNetworkStageConnector : ISurvivorNetworkStageConnector
    {
        public bool IsConnected => false;

        public UniTask ConnectAsync(string address, ushort port, int stageId, string sessionToken = "")
        {
            return UniTask.CompletedTask;
        }

        public UniTask StartHostAsync(int stageId)
        {
            return UniTask.CompletedTask;
        }

        public UniTask StartServerAsync(int stageId)
        {
            return UniTask.CompletedTask;
        }

        public void Disconnect() { }

        public void Dispose() { }
    }
}
