using System;
using UnityEngine;

namespace Game.Shared.Services.Network.Policies
{
    /// <summary>
    /// サーキットブレーカーの状態
    /// </summary>
    public enum CircuitState
    {
        /// <summary>正常動作中（リクエスト許可）</summary>
        Closed,
        /// <summary>遮断中（リクエスト即座に拒否）</summary>
        Open,
        /// <summary>試行中（1リクエストのみ許可して様子見）</summary>
        HalfOpen
    }

    /// <summary>
    /// サーキットブレーカーポリシー
    /// 連続失敗時にリクエストを一時停止してサーバー負荷を軽減
    /// </summary>
    public class CircuitBreakerPolicy
    {
        private readonly object _lock = new();
        private CircuitState _state = CircuitState.Closed;
        private int _consecutiveFailures;
        private DateTime? _openedAt;
        private DateTime _lastFailureAt;

        /// <summary>
        /// 連続失敗でOpenになる閾値
        /// </summary>
        public int FailureThreshold { get; set; } = 5;

        /// <summary>
        /// Open状態の継続時間
        /// </summary>
        public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 失敗カウントのリセット間隔（この時間内に失敗が続かなければリセット）
        /// </summary>
        public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// 現在の状態
        /// </summary>
        public CircuitState State
        {
            get
            {
                lock (_lock)
                {
                    UpdateState();
                    return _state;
                }
            }
        }

        /// <summary>
        /// 連続失敗回数
        /// </summary>
        public int ConsecutiveFailures
        {
            get
            {
                lock (_lock)
                {
                    return _consecutiveFailures;
                }
            }
        }

        /// <summary>
        /// Open状態になった時刻
        /// </summary>
        public DateTime? OpenedAt
        {
            get
            {
                lock (_lock)
                {
                    return _openedAt;
                }
            }
        }

        /// <summary>
        /// デフォルトのサーキットブレーカーポリシー
        /// </summary>
        public static CircuitBreakerPolicy Default => new();

        /// <summary>
        /// 敏感なサーキットブレーカーポリシー（早めにOpenになる）
        /// </summary>
        public static CircuitBreakerPolicy Sensitive => new()
        {
            FailureThreshold = 3,
            OpenDuration = TimeSpan.FromSeconds(60)
        };

        /// <summary>
        /// 寛容なサーキットブレーカーポリシー（なかなかOpenにならない）
        /// </summary>
        public static CircuitBreakerPolicy Tolerant => new()
        {
            FailureThreshold = 10,
            OpenDuration = TimeSpan.FromSeconds(15)
        };

        /// <summary>
        /// 無効化されたサーキットブレーカー（常にClosed）
        /// </summary>
        public static CircuitBreakerPolicy Disabled => new()
        {
            FailureThreshold = int.MaxValue
        };

        /// <summary>
        /// リクエストを実行可能か判定
        /// </summary>
        /// <returns>実行可能な場合はtrue</returns>
        public bool CanExecute()
        {
            lock (_lock)
            {
                UpdateState();

                return _state switch
                {
                    CircuitState.Closed => true,
                    CircuitState.HalfOpen => true,
                    CircuitState.Open => false,
                    _ => false
                };
            }
        }

        /// <summary>
        /// 成功を記録（サーキットをCloseに戻す）
        /// </summary>
        public void RecordSuccess()
        {
            lock (_lock)
            {
                if (_state == CircuitState.HalfOpen)
                {
                    Debug.Log("[CircuitBreaker] Success in HalfOpen state, transitioning to Closed");
                }

                _state = CircuitState.Closed;
                _consecutiveFailures = 0;
                _openedAt = null;
            }
        }

        /// <summary>
        /// 失敗を記録（閾値に達したらOpenに遷移）
        /// </summary>
        public void RecordFailure()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;

                // サンプリング期間を超えていたらリセット
                if (now - _lastFailureAt > SamplingDuration)
                {
                    _consecutiveFailures = 0;
                }

                _consecutiveFailures++;
                _lastFailureAt = now;

                // HalfOpen状態での失敗は即座にOpenに戻す
                if (_state == CircuitState.HalfOpen)
                {
                    TransitionToOpen();
                    Debug.Log("[CircuitBreaker] Failure in HalfOpen state, transitioning back to Open");
                    return;
                }

                // 閾値に達したらOpenに遷移
                if (_consecutiveFailures >= FailureThreshold && _state == CircuitState.Closed)
                {
                    TransitionToOpen();
                    Debug.Log($"[CircuitBreaker] Failure threshold ({FailureThreshold}) reached, transitioning to Open");
                }
            }
        }

        /// <summary>
        /// 手動でサーキットをリセット
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _state = CircuitState.Closed;
                _consecutiveFailures = 0;
                _openedAt = null;
                Debug.Log("[CircuitBreaker] Manually reset to Closed state");
            }
        }

        /// <summary>
        /// Open状態の残り時間を取得
        /// </summary>
        /// <returns>残り時間（Openでない場合はTimeSpan.Zero）</returns>
        public TimeSpan GetRemainingOpenTime()
        {
            lock (_lock)
            {
                if (_state != CircuitState.Open || !_openedAt.HasValue)
                {
                    return TimeSpan.Zero;
                }

                var elapsed = DateTime.UtcNow - _openedAt.Value;
                var remaining = OpenDuration - elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        private void UpdateState()
        {
            if (_state == CircuitState.Open && _openedAt.HasValue)
            {
                var elapsed = DateTime.UtcNow - _openedAt.Value;
                if (elapsed >= OpenDuration)
                {
                    _state = CircuitState.HalfOpen;
                    Debug.Log("[CircuitBreaker] Open duration elapsed, transitioning to HalfOpen");
                }
            }
        }

        private void TransitionToOpen()
        {
            _state = CircuitState.Open;
            _openedAt = DateTime.UtcNow;
        }
    }
}
