using System;
using Cysharp.Net.Http;
using Game.Shared;
using Grpc.Net.Client;
using UnityEngine;

namespace Game.Shared.Realtime.Client
{
    /// <summary>
    /// gRPC チャンネル管理実装
    /// GrpcChannel のライフサイクルを管理し、Singleton として DI に登録
    /// YetAnotherHttpHandler でネイティブ HTTP/2 を使用（StreamingHub 双方向ストリーミング対応）
    /// </summary>
    public class GrpcChannelProvider : IGrpcChannelProvider
    {
        private GrpcChannel _channel;
        private YetAnotherHttpHandler _httpHandler;
        private bool _disposed;

        public bool IsConnected => _channel != null && !_disposed;

        public GrpcChannel GetChannel()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GrpcChannelProvider));
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
                _httpHandler?.Dispose();
                _httpHandler = null;
            }
        }

        private GrpcChannel CreateChannel()
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

            _httpHandler?.Dispose();
            _httpHandler = new YetAnotherHttpHandler { Http2Only = true };

            return GrpcChannel.ForAddress(grpcUrl, new GrpcChannelOptions
            {
                HttpHandler = _httpHandler,
            });
        }
    }
}
