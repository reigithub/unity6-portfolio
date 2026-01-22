using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.Shared.Extensions;
using Game.Shared.Services;
using R3;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// 武器基底クラス（純粋C#）
    /// マスターデータから初期化可能
    /// </summary>
    public abstract class SurvivorWeaponBase : IDisposable
    {
        [Inject] private readonly ILockOnService _lockOnService;

        // マスターデータ
        protected IReadOnlyList<SurvivorWeaponLevelMaster> _levelMasters;

        #region 基本情報

        protected int _weaponId;
        protected string _name;
        protected string _iconAssetName;
        protected int _level = 1;
        protected int _maxLevel = 10;
        protected string _currentAssetName;

        #endregion

        #region 基本パラメータ

        protected int _damage = 10;
        protected int _cooldown = 0;
        protected int _interval = 1000;
        protected float _range = 10f;
        protected int _emitCount = 1;
        protected int _hitCount = 1;
        protected float _moveSpeed = 10f;
        protected int _duration = 0;
        protected int _emitDelay = 0;
        protected int _hitBoxRate = 10000;

        #endregion

        #region 物理パラメータ

        protected float _knockback = 0f;
        protected float _vacuum = 0f;
        protected int _spinSpeed = 0;
        protected int _limit = 20;

        #endregion

        #region クリティカルパラメータ

        protected int _critChance = 0;
        protected int _critMultiplier = 150;

        #endregion

        #region 弾道パラメータ

        protected int _pierce = 0;
        protected int _bounce = 0;
        protected int _chain = 0;
        protected int _homing = 0;
        protected int _spread = 0;

        #endregion

        #region 状態異常パラメータ

        protected int _procRate = 0;

        #endregion

        #region ヒットエフェクト

        protected SurvivorVfxSpawner _vfxSpawner;
        protected string _hitEffectAssetName;
        protected float _hitEffectScale = 1f;

        #endregion

        // State
        protected float _attackTimer;
        protected Transform _owner;
        protected bool _isEnabled = true;
        protected float _damageMultiplier = 1f;
        protected bool _isDisposed;

        #region Properties

        public int WeaponId => _weaponId;
        public string Name => _name;
        public string IconAssetName => _iconAssetName;
        public int Level => _level;
        public int MaxLevel => _maxLevel;
        public int Damage => Mathf.RoundToInt(_damage * _damageMultiplier);
        public float Cooldown => _cooldown / 1000f;
        public float Interval => _interval / 1000f;
        public float Range => _range;
        public int EmitCount => _emitCount;
        public int HitCount => _hitCount;
        public float MoveSpeed => _moveSpeed;
        public float Duration => _duration / 1000f;
        public float EmitDelay => _emitDelay / 1000f;
        public int HitBoxRate => _hitBoxRate;
        public float Knockback => _knockback;
        public float Vacuum => _vacuum;
        public int SpinSpeed => _spinSpeed;
        public int Limit => _limit;
        public int CritChance => _critChance;
        public int CritMultiplier => _critMultiplier;
        public int Pierce => _pierce;
        public int Bounce => _bounce;
        public int Chain => _chain;
        public int Homing => _homing;
        public int Spread => _spread;
        public int ProcRate => _procRate;
        public bool IsEnabled => _isEnabled;
        public string HitEffectAssetName => _hitEffectAssetName;
        public float HitEffectScale => _hitEffectScale;

        #endregion

        // Events
        protected readonly Subject<int> _onAttack = new();
        public Observable<int> OnAttack => _onAttack;

        /// <summary>
        /// マスターデータから初期化
        /// </summary>
        public virtual UniTask InitializeAsync(
            SurvivorWeaponMaster weaponMaster,
            IReadOnlyList<SurvivorWeaponLevelMaster> levelMasters,
            Transform owner,
            float damageMultiplier,
            SurvivorVfxSpawner vfxSpawner)
        {
            _weaponId = weaponMaster.Id;
            _name = weaponMaster.Name;
            _iconAssetName = weaponMaster.IconAssetName;
            _maxLevel = levelMasters.Max(x => x.Level);
            _levelMasters = levelMasters;
            _owner = owner;
            _damageMultiplier = damageMultiplier;
            _attackTimer = 0f;

            // ヒットエフェクト設定
            _vfxSpawner = vfxSpawner;
            _hitEffectAssetName = weaponMaster.HitEffectAssetName;
            _hitEffectScale = weaponMaster.HitEffectScale > 0
                ? weaponMaster.HitEffectScale / 10000f
                : 1f;

            // レベル1を適用
            ApplyLevel(1);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 指定レベルのマスターを取得
        /// </summary>
        protected SurvivorWeaponLevelMaster GetLevelMaster(int level)
        {
            return _levelMasters?.FirstOrDefault(l => l.Level == level);
        }

        /// <summary>
        /// 指定レベルのステータスを適用
        /// </summary>
        protected bool ApplyLevel(int level)
        {
            var levelMaster = GetLevelMaster(level);
            if (levelMaster == null)
            {
                Debug.LogWarning($"[SurvivorWeaponBase] Level master not found: weaponId={_weaponId}, level={level}");
                return false;
            }

            _level = levelMaster.Level;

            // 基本パラメータ
            _damage = levelMaster.Damage;
            _cooldown = levelMaster.Cooldown;
            _interval = levelMaster.ProcInterval;
            _range = levelMaster.Range.ToUnit();
            _emitCount = levelMaster.EmitCount;
            _hitCount = levelMaster.HitCount;
            _moveSpeed = levelMaster.Speed.ToUnit();
            _duration = levelMaster.Duration;
            _emitDelay = levelMaster.EmitDelay;
            _hitBoxRate = levelMaster.HitBoxRate;

            // 物理パラメータ
            _knockback = levelMaster.Knockback.ToUnit();
            _vacuum = levelMaster.Vacuum.ToUnit();
            _spinSpeed = levelMaster.Spin;
            _limit = levelMaster.EmitLimit;

            // クリティカルパラメータ
            _critChance = levelMaster.CritHitRate;
            _critMultiplier = levelMaster.CritHitMultiplier;

            // 弾道パラメータ
            _pierce = levelMaster.Penetration;
            _bounce = levelMaster.Bounce;
            _chain = levelMaster.Chain;
            _homing = levelMaster.Homing;
            _spread = levelMaster.Spread;

            // 状態異常パラメータ
            _procRate = levelMaster.ProcRate;

            // アセット名の変更チェック
            var newAssetName = levelMaster.AssetName;
            if (!string.IsNullOrEmpty(newAssetName) && newAssetName != _currentAssetName)
            {
                var oldAssetName = _currentAssetName;
                _currentAssetName = newAssetName;
                OnAssetNameChanged(oldAssetName, newAssetName);
            }
            else if (!string.IsNullOrEmpty(newAssetName))
            {
                _currentAssetName = newAssetName;
            }

            return true;
        }

        /// <summary>
        /// アセット名が変更された時に呼ばれる（派生クラスでオーバーライド）
        /// </summary>
        protected virtual void OnAssetNameChanged(string oldAssetName, string newAssetName)
        {
            // 派生クラスで実装
        }

        /// <summary>
        /// レベルアップ
        /// </summary>
        public virtual bool LevelUp()
        {
            int nextLevel = _level + 1;
            if (nextLevel > MaxLevel)
            {
                Debug.LogWarning($"[SurvivorWeaponBase] Already max level: weaponId={_weaponId}, level={_level}");
                return false;
            }

            if (!ApplyLevel(nextLevel))
            {
                return false;
            }

            Debug.Log($"[SurvivorWeaponBase] Weapon {_weaponId} leveled up to {_level}");
            return true;
        }

        public virtual void SetDamageMultiplier(float multiplier)
        {
            _damageMultiplier = multiplier;
        }

        /// <summary>
        /// ターゲットを取得（ロックオン優先、なければ自動選択を試みる）
        /// 派生クラスでFindNearestEnemyをオーバーライドすること
        /// </summary>
        protected virtual bool TryGetTarget(out Transform target)
        {
            // ロックオン優先
            if (_lockOnService.TryGetTarget(out target))
                return true;

            target = null;
            return false;
        }

        public virtual void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
        }

        /// <summary>
        /// 毎フレーム更新（SurvivorWeaponManagerから呼ばれる）
        /// </summary>
        public virtual void UpdateWeapon(float deltaTime)
        {
            if (!_isEnabled || _owner == null) return;

            _attackTimer -= deltaTime;

            if (_attackTimer <= 0f)
            {
                if (TryAttack())
                {
                    _attackTimer = Interval;
                    _onAttack.OnNext(Damage);
                }
            }
        }

        /// <summary>
        /// 攻撃を試みる（派生クラスで実装）
        /// </summary>
        protected abstract bool TryAttack();

        /// <summary>
        /// クリティカル判定（万分率）
        /// </summary>
        protected bool RollCritical()
        {
            if (_critChance <= 0) return false;
            return _critChance.RollChance();
        }

        /// <summary>
        /// クリティカルダメージを計算（万分率）
        /// </summary>
        protected int CalculateCriticalDamage(int baseDamage)
        {
            return Mathf.RoundToInt(baseDamage * _critMultiplier.ToRate());
        }

        /// <summary>
        /// ダメージ発生確率判定（万分率）
        /// ProcRate=10000で100%ダメージ、ProcRate=0で常にfalse
        /// </summary>
        protected bool RollProcRate()
        {
            if (_procRate <= 0) return false;
            if (_procRate >= 10000) return true;
            return _procRate.RollChance();
        }

        /// <summary>
        /// 武器情報の取得
        /// </summary>
        public virtual SurvivorWeaponInfo GetInfo()
        {
            return new SurvivorWeaponInfo
            {
                WeaponId = _weaponId,
                Name = GetType().Name,
                Level = _level,
                MaxLevel = _maxLevel,
                Damage = Damage,
                Interval = Interval,
                SearchRange = _range,
                EmitCount = _emitCount
            };
        }

        public virtual void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _onAttack.Dispose();
        }
    }

    /// <summary>
    /// 武器情報
    /// </summary>
    public class SurvivorWeaponInfo
    {
        public int WeaponId { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int MaxLevel { get; set; }
        public int Damage { get; set; }
        public float Interval { get; set; }
        public float SearchRange { get; set; }
        public int EmitCount { get; set; }
    }
}