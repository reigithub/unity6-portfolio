using System;
using Cysharp.Threading.Tasks;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// NGOステージクライアントのインターフェース。
    /// サーバーではNullNetworkSurvivorStageClientが登録される。
    /// </summary>
    public interface ISurvivorStageNetworkConnector : IDisposable
    {
        bool IsConnected { get; }
        UniTask ConnectAsync(string address, ushort port, int stageId, string sessionToken = "");
        void Disconnect();
    }
}
