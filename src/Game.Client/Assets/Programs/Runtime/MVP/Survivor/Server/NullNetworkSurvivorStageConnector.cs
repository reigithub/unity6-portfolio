using Cysharp.Threading.Tasks;
using Game.Shared.Netcode.Client;

namespace Game.MVP.Survivor.Server
{
    /// <summary>
    /// サーバー用Null実装。全メソッドno-op。
    /// </summary>
    public class NullNetworkSurvivorStageConnector : INetworkSurvivorStageConnector
    {
        public bool IsConnected => false;

        public UniTask ConnectAsync(string address, ushort port, int stageId, string sessionToken = "")
        {
            return UniTask.CompletedTask;
        }

        public void Disconnect() { }

        public void Dispose() { }
    }
}
