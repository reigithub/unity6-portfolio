using System;
using Game.Shared.Item;
using Game.Shared.Network.Survivor;
using UnityEngine;

namespace Game.MVP.Survivor.Item
{
    /// <summary>
    /// アイテムタイプ
    /// SurvivorItemMaster.ItemTypeに対応
    /// </summary>
    public enum SurvivorItemType
    {
        None = 0,
        Experience = 1, // 経験値
        Recovery = 2,   // 回復
        Magnet = 3,     // 磁石（アイテム吸引）
        Bomb = 4,       // 爆弾（範囲攻撃）
        Currency = 5,   // 通貨
        Speed = 6,      // スピードアップ
        Time = 7,       // 時間（クールダウン短縮等）
        Shield = 8,     // シールド（無敵）
        Battery = 9,    // バッテリー（スキル回復）
        Key = 10,       // 鍵
        Special = 11    // 特殊
    }

    /// <summary>
    /// Survivorアイテム
    /// マスタデータからItemTypeに応じた効果を持つ汎用アイテム
    /// プレイヤーからStartAttractionを呼ばれることで吸引開始
    /// 表示ロジック（浮遊アニメーション）はクライアント側の ItemProxyCollectible が担当する
    /// </summary>
    public class SurvivorItem : MonoBehaviour, ICollectible
    {
        [Header("Item Settings")]
        [SerializeField] private int _itemId;

        [SerializeField] private SurvivorItemType _itemType = SurvivorItemType.Experience;
        [SerializeField] private int _effectValue;
        [SerializeField] private int _effectRange;
        [SerializeField] private int _effectDuration;
        [SerializeField] private int _rarity = 1;
        [SerializeField] private float _scale = 1f;

        // 吸引状態
        private Transform _attractTarget;
        private float _attractSpeed;
        private bool _isBeingAttracted;

        // 収集状態
        private bool _isCollected;

        // ポーズ参照（生成元スポーナーが設定）
        private SurvivorFusionGameState _gameState;

        // Events
        public event Action<SurvivorItem> OnCollected;

        // Properties
        public int ItemId => _itemId;
        public SurvivorItemType ItemType => _itemType;
        public int EffectValue => _effectValue;
        public int EffectRange => _effectRange;
        public int EffectDuration => _effectDuration;
        public int Rarity => _rarity;
        public float Scale => _scale;
        public bool IsCollected => _isCollected;

        /// <summary>
        /// マスタデータから初期化
        /// </summary>
        public void Initialize(int itemId, SurvivorItemType itemType, int effectValue, int effectRange, int effectDuration, int rarity, float scale, SurvivorFusionGameState gameState)
        {
            _itemId = itemId;
            _itemType = itemType;
            _effectValue = effectValue;
            _effectRange = effectRange;
            _effectDuration = effectDuration;
            _rarity = rarity;
            _scale = scale > 0f ? scale : 1f;
            _gameState = gameState;

            ApplyTransform();
        }

        private void ApplyTransform()
        {
            transform.position = new Vector3(transform.position.x, 0.5f, transform.position.z);
            transform.localScale = Vector3.one * _scale;
        }

        private void Update()
        {
            if (_isCollected) return;
            if (_gameState != null && _gameState.IsEffectivelyPaused) return;

            if (_isBeingAttracted && _attractTarget != null)
            {
                // 吸引中：ターゲットに向かって移動（ゲームロジック）
                Vector3 direction = (_attractTarget.position - transform.position).normalized;
                transform.position += direction * _attractSpeed * Time.deltaTime;
            }
        }

        /// <summary>
        /// プレイヤーから呼ばれる：吸引開始
        /// </summary>
        /// <param name="target">吸引先のTransform（プレイヤー）</param>
        /// <param name="speed">吸引速度（PlayerLevelMasterから）</param>
        public void StartAttraction(Transform target, float speed)
        {
            if (_isBeingAttracted) return;

            _attractTarget = target;
            _attractSpeed = speed;
            _isBeingAttracted = true;
        }

        /// <summary>
        /// アイテム収集
        /// </summary>
        public void Collect()
        {
            if (_isCollected) return;

            _isCollected = true;
            OnCollected?.Invoke(this);

            gameObject.SetActive(false);
        }

        /// <summary>
        /// クライアントプロキシ用にサーバー専用コンポーネントを破棄する。
        /// SurvivorItemView から呼ばれる。
        /// </summary>
        public void StripForProxy()
        {
            Destroy(this);
        }

        /// <summary>
        /// プールに戻す際のリセット
        /// </summary>
        public void Reset()
        {
            _isCollected = false;
            _isBeingAttracted = false;
            _attractTarget = null;
            _attractSpeed = 0f;
        }

        /// <summary>
        /// 位置設定
        /// </summary>
        public void SetPosition(Vector3 position)
        {
            transform.position = position;
        }
    }
}
