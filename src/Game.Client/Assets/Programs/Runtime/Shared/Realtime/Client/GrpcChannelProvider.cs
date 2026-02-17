using System;
using Grpc.Core;
using UnityEngine;

namespace Game.Shared.Realtime.Client
{
    /// <summary>
    /// gRPC チャンネル管理実装
    /// GrpcChannelx（MagicOnion Unity 統合）によるチャンネル管理
    /// GrpcChannelProviderHost が HttpHandler のライフサイクルを管理
    /// </summary>
    public class GrpcChannelProvider : IGrpcChannelProvider
    {
        private ChannelBase _channel;
        private bool _disposed;

        public bool IsConnected => _channel != null && !_disposed;

        public ChannelBase GetChannel()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GrpcChannelProvider));

            if (_channel == null)
                _channel = CreateChannel();

            return _channel;
        }

        public void Reconnect()
        {
            (_channel as IDisposable)?.Dispose();
            _channel = CreateChannel();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                (_channel as IDisposable)?.Dispose();
                _channel = null;
            }
        }

        private ChannelBase CreateChannel()
        {
            var config = GameEnvironmentHelper.CurrentConfig;
            var grpcUrl = config?.GrpcBaseUrl;

            if (string.IsNullOrEmpty(grpcUrl))
            {
                grpcUrl = "http://localhost:5001";
                Debug.LogWarning(
                    "[GrpcChannelProvider] GrpcBaseUrl is not configured. Using default: " + grpcUrl);
            }

            Debug.Log("[GrpcChannelProvider] Connecting to gRPC server: " + grpcUrl);

            // GrpcChannelx: GrpcChannelProviderHost で管理される Unity 統合チャンネル
            return MagicOnion.GrpcChannelx.ForAddress(grpcUrl);
        }
    }
}
