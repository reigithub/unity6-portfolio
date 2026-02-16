using System;
using Grpc.Net.Client;

namespace Game.Shared.Realtime.Services
{
    /// <summary>
    /// MagicOnion gRPC チャンネル管理インターフェース
    /// </summary>
    public interface IMagicOnionChannelProvider : IDisposable
    {
        /// <summary>
        /// gRPC チャンネルを取得（遅延初期化）
        /// </summary>
        GrpcChannel GetChannel();

        /// <summary>
        /// チャンネルが接続済みかどうか
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// チャンネルを再接続
        /// </summary>
        void Reconnect();
    }
}
