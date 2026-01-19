using System;
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
    /// </summary>
    public class SurvivorItem : MonoBehaviour
    {
        [Header("Item Settings")]
        [SerializeField] private int _itemId;

        [SerializeField] private SurvivorItemType _itemType = SurvivorItemType.Experience;
        [SerializeField] private int _effectValue;
        [SerializeField] private int _effectRange;
        [SerializeField] private int _effectDuration;
        [SerializeField] private int _rarity = 1;
        [SerializeField] private float _scale = 1f;

        [Header("Pickup Settings")]
        [SerializeField] private float _attractDistance = 3f;

        [SerializeField] private float _attractSpeed = 10f;
        [SerializeField] private float _collectDistance = 0.5f;

        [Header("Visual")]
        [SerializeField] private float _floatAmplitude = 0.2f;

        [SerializeField] private float _floatSpeed = 2f;

        // State
        private Transform _target;
        private Vector3 _initialPosition;
        private float _floatTimer;
        private bool _isCollected;
        private bool _isBeingAttracted;

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
        public void Initialize(int itemId, SurvivorItemType itemType, int effectValue, int effectRange, int effectDuration, int rarity, float scale = 1f)
        {
            _itemId = itemId;
            _itemType = itemType;
            _effectValue = effectValue;
            _effectRange = effectRange;
            _effectDuration = effectDuration;
            _rarity = rarity;
            _scale = scale > 0f ? scale : 1f;
            _floatAmplitude *= _scale;

            // レアリティに応じて吸引距離を調整（高レアリティは広い）
            _attractDistance = 2f + rarity * 1f;

            ApplyTransform();
        }

        private void ApplyTransform()
        {
            transform.position = new Vector3(transform.position.x, 0.5f, transform.position.z);
            transform.localScale = Vector3.one * _scale;
        }

        /// <summary>
        /// 経験値アイテム用の簡易初期化（後方互換性）
        /// </summary>
        public void InitializeAsExperience(int experienceValue)
        {
            _itemType = SurvivorItemType.Experience;
            _effectValue = experienceValue;
        }

        private void Start()
        {
            _initialPosition = transform.position;
        }

        private void Update()
        {
            if (_isCollected) return;

            // プレイヤーを探す
            if (_target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    _target = player.transform;
                }
            }

            if (_target != null)
            {
                float distance = Vector3.Distance(transform.position, _target.position);

                // 吸引範囲内または強制吸引中なら近づく
                if (distance <= _attractDistance || _isBeingAttracted)
                {
                    _isBeingAttracted = true;
                    Vector3 direction = (_target.position - transform.position).normalized;
                    transform.position += direction * _attractSpeed * Time.deltaTime;

                    // 収集判定
                    if (distance <= _collectDistance)
                    {
                        Collect();
                    }
                }
                else
                {
                    // 浮遊アニメーション
                    _floatTimer += Time.deltaTime * _floatSpeed;
                    float yOffset = Mathf.Sin(_floatTimer) * _floatAmplitude;
                    transform.position = _initialPosition + Vector3.up * yOffset;
                }
            }
        }

        /// <summary>
        /// マグネット効果などで強制的に吸引開始
        /// </summary>
        public void StartAttraction()
        {
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
        /// プールに戻す際のリセット
        /// </summary>
        public void Reset()
        {
            _isCollected = false;
            _isBeingAttracted = false;
            _target = null;
            _floatTimer = 0f;
        }

        /// <summary>
        /// 位置設定
        /// </summary>
        public void SetPosition(Vector3 position)
        {
            transform.position = position;
            _initialPosition = position;
        }

        // ===== 後方互換性用プロパティ =====

        /// <summary>
        /// 経験値（後方互換性用）
        /// </summary>
        public int ExperienceValue
        {
            get => _itemType == SurvivorItemType.Experience ? _effectValue : 0;
            set
            {
                if (_itemType == SurvivorItemType.Experience)
                {
                    _effectValue = value;
                }
            }
        }
    }
}