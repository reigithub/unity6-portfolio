using System;
using Game.Shared;
using Grpc.Net.Client;
using UnityEngine;

namespace Game.Shared.Realtime.Services
{
    /// <summary>
    /// MagicOnion gRPC チャンネル管理実装
    /// GrpcChannel のライフサイクルを管理し、Singleton として DI に登録
    /// </summary>
    public class MagicOnionChannelProvider : IMagicOnionChannelProvider
    {
        private GrpcChannel _channel;
        private bool _disposed;

        public bool IsConnected => _channel != null && !_disposed;

        public GrpcChannel GetChannel()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MagicOnionChannelProvider));
            }

            if (_channel == null)
            {
                _channel = CreateChannel();
            }

            return _channel;
        }

        public void Reconnect()
        {
            _channel?.Dispose();
            _channel = CreateChannel();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _channel?.Dispose();
                _channel = null;
            }
        }

        private static GrpcChannel CreateChannel()
        {
            var config = GameEnvironmentHelper.CurrentConfig;
            var grpcUrl = config?.GrpcBaseUrl;

            if (string.IsNullOrEmpty(grpcUrl))
            {
                grpcUrl = "http://localhost:5001";
                Debug.LogWarning(
                    "[MagicOnionChannelProvider] GrpcBaseUrl is not configured. Using default: " + grpcUrl);
            }

            Debug.Log("[MagicOnionChannelProvider] Connecting to gRPC server: " + grpcUrl);

            return GrpcChannel.ForAddress(grpcUrl, new GrpcChannelOptions
            {
                HttpHandler = new Grpc.Net.Client.Web.GrpcWebHandler(new System.Net.Http.HttpClientHandler()),
            });
        }
    }
}
