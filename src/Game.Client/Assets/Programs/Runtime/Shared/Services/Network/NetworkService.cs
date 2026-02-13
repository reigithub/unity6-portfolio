using System;
using Game.Shared.Services.Network.Connectivity;
using Game.Shared.Services.Network.Policies;
using R3;
using UnityEngine;

namespace Game.Shared.Services.Network
{
    /// <summary>
    /// ネットワーク通信環境を検証するサービス
    /// </summary>
    public class NetworkService : INetworkService, IDisposable
    {
        private readonly IConnectivityChecker _connectivityChecker;
        private readonly CircuitBreakerPolicy _circuitBreaker;
        private readonly Subject<CircuitState> _onCircuitStateChanged = new();
        private CircuitState _lastCircuitState = CircuitState.Closed;
        private bool _isDisposed;

        public bool IsConnected => _connectivityChecker.IsConnected;
        public Observable<bool> OnConnectivityChanged => _connectivityChecker.OnConnectivityChanged;
        public bool CanExecute => _circuitBreaker.CanExecute();
        public CircuitState CircuitState => _circuitBreaker.State;
        public Observable<CircuitState> OnCircuitStateChanged => _onCircuitStateChanged;

        public NetworkService(
            IConnectivityChecker connectivityChecker,
            CircuitBreakerPolicy circuitBreaker = null)
        {
            _connectivityChecker = connectivityChecker ?? throw new ArgumentNullException(nameof(connectivityChecker));
            _circuitBreaker = circuitBreaker ?? CircuitBreakerPolicy.Default;
            _connectivityChecker.StartMonitoring();
        }

        public void RecordSuccess()
        {
            _circuitBreaker.RecordSuccess();
            NotifyCircuitStateChange();
        }

        public void RecordFailure()
        {
            _circuitBreaker.RecordFailure();
            NotifyCircuitStateChange();
        }

        public void ResetCircuitBreaker()
        {
            _circuitBreaker.Reset();
            NotifyCircuitStateChange();
        }

        private void NotifyCircuitStateChange()
        {
            var currentState = _circuitBreaker.State;
            if (currentState != _lastCircuitState)
            {
                _lastCircuitState = currentState;
                _onCircuitStateChanged.OnNext(currentState);
                Debug.Log($"[NetworkService] Circuit state changed: {currentState}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _onCircuitStateChanged.Dispose();
            _connectivityChecker.StopMonitoring();
        }
    }
}
