using System;
using Game.Shared.Item;
using Game.Shared.Network.Survivor;
using UnityEngine;

namespace Game.MVP.Survivor.Item
{
    /// <summary>
    /// クライアントプロキシ用 ICollectible 実装。
    /// PlayerController の既存吸引ロジック（OverlapSphere → StartAttraction）で動作する。
    /// 浮遊アニメーションと吸引移動を自己管理する。
    /// Collect は no-op（実際の回収はサーバーが管理、Despawn ClientRpc で削除）。
    /// </summary>
    public class ItemProxyCollectible : MonoBehaviour, ICollectible
    {
        private const float FloatAmplitude = 0.2f;
        private const float FloatSpeed = 2f;

        private Transform _attractTarget;
        private float _attractSpeed;
        private float _scale;
        private Vector3 _initialPosition;
        private float _floatTimer;
        private SurvivorFusionGameState _gameState;

        /// <summary>アイテムID</summary>
        public int ItemId { get; private set; }

        /// <summary>収集済みフラグ</summary>
        public bool IsCollected { get; private set; }

        /// <summary>吸引中フラグ</summary>
        public bool IsAttracting => _attractTarget != null;

        /// <summary>収集時コールバック（SurvivorItemView が RPC 送信用に設定）</summary>
        public event Action<int> OnCollected;

        /// <summary>
        /// アイテムプロキシを初期化する。
        /// </summary>
        /// <param name="scale">アイテムのスケール値（浮遊振幅の計算に使用）</param>
        /// <param name="itemId">アイテムID</param>
        /// <param name="gameState">ポーズ判定用ゲーム状態</param>
        public void Initialize(float scale, int itemId, SurvivorFusionGameState gameState)
        {
            _scale = scale;
            ItemId = itemId;
            _gameState = gameState;
            _initialPosition = transform.position;
            _floatTimer = 0f;
        }

        /// <summary>
        /// プレイヤーから呼ばれる：吸引開始。
        /// </summary>
        /// <param name="target">吸引先のTransform（プレイヤー）</param>
        /// <param name="speed">吸引速度</param>
        public void StartAttraction(Transform target, float speed)
        {
            if (_attractTarget != null) return;
            _attractTarget = target;
            _attractSpeed = speed;
        }

        /// <summary>
        /// アイテム収集。
        /// </summary>
        public void Collect()
        {
            if (IsCollected) return;
            IsCollected = true;
            OnCollected?.Invoke(ItemId);
        }

        /// <summary>
        /// プールに戻す際のリセット。
        /// </summary>
        public void Reset()
        {
            _attractTarget = null;
            _attractSpeed = 0f;
            IsCollected = false;
            _floatTimer = 0f;
            _initialPosition = transform.position;
        }

        private void Update()
        {
            if (IsCollected) return;
            if (_gameState != null && _gameState.IsEffectivelyPaused) return;

            if (_attractTarget != null)
            {
                // 吸引移動（収集判定は SurvivorPlayerController が担当）
                var diff = _attractTarget.position - transform.position;
                transform.position += diff.normalized * _attractSpeed * Time.deltaTime;
            }
            else
            {
                // 浮遊アニメーション
                _floatTimer += Time.deltaTime * FloatSpeed;
                float yOffset = Mathf.Sin(_floatTimer) * FloatAmplitude * _scale;
                transform.position = _initialPosition + Vector3.up * yOffset;
            }
        }
    }
}
