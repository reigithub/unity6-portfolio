using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace Game.Shared.Services.Network.Connectivity
{
    /// <summary>
    /// ネットワーク接続状態監視の実装
    /// UnityのApplication.internetReachabilityを定期的にポーリング
    /// </summary>
    public class ConnectivityChecker : IConnectivityChecker
    {
        private const float DefaultCheckIntervalSeconds = 5f;

        private readonly float _checkIntervalSeconds;
        private readonly ReactiveProperty<bool> _connectivityProperty;
        private CancellationTokenSource _monitoringCts;
        private bool _isMonitoring;
        private bool _isDisposed;

        public bool IsConnected => _connectivityProperty.Value;

        public Observable<bool> OnConnectivityChanged => _connectivityProperty.DistinctUntilChanged();

        public ConnectivityChecker() : this(DefaultCheckIntervalSeconds)
        {
        }

        public ConnectivityChecker(float checkIntervalSeconds)
        {
            _checkIntervalSeconds = checkIntervalSeconds;
            _connectivityProperty = new ReactiveProperty<bool>(CheckConnectivityInternal());
        }

        public void StartMonitoring()
        {
            if (_isMonitoring || _isDisposed) return;

            _isMonitoring = true;
            _monitoringCts = new CancellationTokenSource();
            MonitoringLoop(_monitoringCts.Token).Forget();

            Debug.Log("[ConnectivityChecker] Monitoring started");
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring) return;

            _isMonitoring = false;
            _monitoringCts?.Cancel();
            _monitoringCts?.Dispose();
            _monitoringCts = null;

            Debug.Log("[ConnectivityChecker] Monitoring stopped");
        }

        public bool CheckConnectivity()
        {
            var isConnected = CheckConnectivityInternal();
            UpdateConnectivityState(isConnected);
            return isConnected;
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            StopMonitoring();
            _connectivityProperty.Dispose();
        }

        private async UniTaskVoid MonitoringLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var isConnected = CheckConnectivityInternal();
                    UpdateConnectivityState(isConnected);

                    await UniTask.Delay(
                        TimeSpan.FromSeconds(_checkIntervalSeconds),
                        cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ConnectivityChecker] Error in monitoring loop: {ex.Message}");
                }
            }
        }

        private void UpdateConnectivityState(bool isConnected)
        {
            if (_connectivityProperty.Value != isConnected)
            {
                Debug.Log($"[ConnectivityChecker] Connectivity changed: {isConnected}");
                _connectivityProperty.Value = isConnected;
            }
        }

        private static bool CheckConnectivityInternal()
        {
            return Application.internetReachability != NetworkReachability.NotReachable;
        }
    }
}
