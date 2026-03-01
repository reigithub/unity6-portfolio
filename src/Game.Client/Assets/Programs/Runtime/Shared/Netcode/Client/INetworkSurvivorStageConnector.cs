using System;
using Cysharp.Threading.Tasks;

namespace Game.Shared.Netcode.Client
{
    /// <summary>
    /// NGOステージクライアントのインターフェース。
    /// サーバーではNullNetworkSurvivorStageClientが登録される。
    /// </summary>
    public interface INetworkSurvivorStageConnector : IDisposable
    {
        bool IsConnected { get; }
        UniTask ConnectAsync(string address, ushort port, int stageId, string sessionToken = "");
        void Disconnect();
    }
}
