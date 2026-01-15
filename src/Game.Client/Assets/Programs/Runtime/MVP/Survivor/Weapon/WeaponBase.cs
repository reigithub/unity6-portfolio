using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData.MemoryTables;
using R3;
using UnityEngine;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// 武器基底クラス
    /// マスターデータから初期化可能
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] protected string _attackSoundKey = "SE_Attack";

        // マスターデータから設定される値
        protected int _weaponId;
        protected int _level = 1;
        protected int _maxLevel = 8;
        protected int _damage = 10;
        protected float _cooldown = 1f;
        protected float _range = 5f;
        protected int _projectileCount = 1;
        protected int _projectileSpeed = 10;
        protected int _pierce = 0;

        // State
        protected float _attackTimer;
        protected Transform _owner;
        protected bool _isEnabled = true;
        protected float _damageMultiplier = 1f;

        // Properties
        public int WeaponId => _weaponId;
        public int Level => _level;
        public int Damage => Mathf.RoundToInt(_damage * _damageMultiplier);
        public float AttackInterval => _cooldown;
        public float Range => _range;
        public int ProjectileCount => _projectileCount;

        // Events
        protected readonly Subject<int> _onAttack = new();
        public Observable<int> OnAttack => _onAttack;

        /// <summary>
        /// マスターデータから初期化
        /// </summary>
        public virtual UniTask Initialize(
            SurvivorWeaponMaster weaponMaster,
            SurvivorWeaponLevelMaster levelMaster,
            Transform owner,
            float damageMultiplier = 1f)
        {
            _weaponId = weaponMaster.Id;
            _maxLevel = weaponMaster.MaxLevel;
            _owner = owner;
            _damageMultiplier = damageMultiplier;
            _attackTimer = 0f;

            ApplyLevelMaster(levelMaster);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// レベルマスターのステータスを適用
        /// </summary>
        protected void ApplyLevelMaster(SurvivorWeaponLevelMaster levelMaster)
        {
            _level = levelMaster.Level;
            _damage = levelMaster.Damage;
            _cooldown = levelMaster.Cooldown / 1000f; // ミリ秒→秒
            _range = levelMaster.Range;
            _projectileCount = levelMaster.ProjectileCount;
            _projectileSpeed = levelMaster.ProjectileSpeed;
            _pierce = levelMaster.Pierce;
        }

        /// <summary>
        /// レベルアップ
        /// </summary>
        public virtual void LevelUp(SurvivorWeaponLevelMaster levelMaster)
        {
            if (_level >= _maxLevel) return;

            ApplyLevelMaster(levelMaster);
            Debug.Log($"[WeaponBase] Weapon {_weaponId} leveled up to {_level}");
        }

        public virtual void SetDamageMultiplier(float multiplier)
        {
            _damageMultiplier = multiplier;
        }

        public virtual void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
        }

        protected virtual void Update()
        {
            if (!_isEnabled || _owner == null) return;

            _attackTimer -= Time.deltaTime;

            if (_attackTimer <= 0f)
            {
                if (TryAttack())
                {
                    _attackTimer = _cooldown;
                    _onAttack.OnNext(Damage);
                }
            }
        }

        /// <summary>
        /// 攻撃を試みる（派生クラスで実装）
        /// </summary>
        protected abstract bool TryAttack();

        /// <summary>
        /// 武器情報の取得
        /// </summary>
        public virtual WeaponInfo GetInfo()
        {
            return new WeaponInfo
            {
                WeaponId = _weaponId,
                Name = GetType().Name,
                Level = _level,
                MaxLevel = _maxLevel,
                Damage = Damage,
                AttackInterval = _cooldown,
                Range = _range,
                ProjectileCount = _projectileCount
            };
        }

        protected virtual void OnDestroy()
        {
            _onAttack.Dispose();
        }
    }

    /// <summary>
    /// 武器情報
    /// </summary>
    public class WeaponInfo
    {
        public int WeaponId { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int MaxLevel { get; set; }
        public int Damage { get; set; }
        public float AttackInterval { get; set; }
        public float Range { get; set; }
        public int ProjectileCount { get; set; }
    }
}