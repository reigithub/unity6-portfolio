using System.Collections.Generic;
using System.Linq;
using Game.Client.MasterData;
using Game.Shared.Combat;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Network.Fusion;
using Game.Shared.Services;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// サーバー用武器マネージャー（純粋C#クラス）。
    /// プレハブ/プール/VFX を一切持たず、マスターデータ駆動でダメージ計算を行う。
    /// SurvivorNetworkStageScene から DI で注入される。
    /// </summary>
    public class SurvivorNetworkWeaponManager
    {
        [Inject] private readonly IMasterDataService _masterDataService;
        [Inject] private readonly IFusionRunnerService _runnerService;
        private MemoryDatabase MemoryDatabase => _masterDataService.MemoryDatabase;

        private readonly Dictionary<int, NetworkWeaponSlot> _weapons = new();
        private float _damageMultiplier = 1f;

        private const float PierceDetectionRadius = 0.5f;

        // SphereCast バッファ（SphereCastAll の allocating 版代替）
        private static readonly RaycastHit[] s_pierceHitBuffer = new RaycastHit[32];

        // 距離でソートするコンパレータ
        private static readonly RaycastHitDistanceComparer s_pierceHitComparer = new RaycastHitDistanceComparer();

        // グローバルヒットレート制限
        private const int MaxHitsPerSecond = 30;
        private readonly Queue<float> _globalHitWindow = new();

        /// <summary>
        /// 初期化。初期武器を追加する。
        /// </summary>
        public void Initialize(int startingWeaponId, float damageMultiplier)
        {
            _damageMultiplier = damageMultiplier;

            if (startingWeaponId > 0)
            {
                AddWeapon(startingWeaponId);
            }
        }

        /// <summary>
        /// 武器を追加。既に持っている場合はアップグレード。
        /// </summary>
        public bool AddWeapon(int weaponId)
        {
            if (_weapons.TryGetValue(weaponId, out var existing))
            {
                return UpgradeWeapon(weaponId);
            }

            if (!MemoryDatabase.SurvivorWeaponMasterTable.TryFindById(weaponId, out var weaponMaster))
            {
                Debug.LogError($"[SurvivorNetworkWeaponManager] Weapon master not found: {weaponId}");
                return false;
            }

            var levelMasters = MemoryDatabase.SurvivorWeaponLevelMasterTable.FindByWeaponId(weaponId);
            if (levelMasters.Count == 0)
            {
                Debug.LogError($"[SurvivorNetworkWeaponManager] No level masters for weapon: {weaponId}");
                return false;
            }

            var slot = new NetworkWeaponSlot
            {
                WeaponId = weaponId,
                Name = weaponMaster.Name,
                IconAssetName = weaponMaster.IconAssetName,
                MaxLevel = levelMasters.Max(l => l.Level),
                DamageMultiplier = _damageMultiplier,
            };

            ApplyLevel(slot, levelMasters, 1);
            _weapons[weaponId] = slot;

            Debug.Log($"[SurvivorNetworkWeaponManager] Added weapon: {weaponMaster.Name} Lv.1");
            return true;
        }

        /// <summary>
        /// 武器をアップグレード。
        /// </summary>
        public bool UpgradeWeapon(int weaponId)
        {
            if (!_weapons.TryGetValue(weaponId, out var slot)) return false;

            int nextLevel = slot.Level + 1;
            if (nextLevel > slot.MaxLevel)
            {
                Debug.LogWarning($"[SurvivorNetworkWeaponManager] Already max level: weaponId={weaponId}");
                return false;
            }

            var levelMasters = MemoryDatabase.SurvivorWeaponLevelMasterTable.FindByWeaponId(weaponId);
            if (!ApplyLevel(slot, levelMasters, nextLevel))
            {
                return false;
            }

            Debug.Log($"[SurvivorNetworkWeaponManager] Upgraded weapon: {weaponId} to Lv.{slot.Level}");
            return true;
        }

        /// <summary>
        /// 武器を入れ替え。
        /// </summary>
        public bool ReplaceWeapon(int removeWeaponId, int newWeaponId)
        {
            if (!_weapons.TryGetValue(removeWeaponId, out var removeSlot))
            {
                Debug.LogError($"[SurvivorNetworkWeaponManager] Weapon to remove not found: {removeWeaponId}");
                return false;
            }

            _weapons.Remove(removeWeaponId);
            Debug.Log($"[SurvivorNetworkWeaponManager] Removed weapon: {removeSlot.Name}");

            return AddWeapon(newWeaponId);
        }

        /// <summary>
        /// WeaponId で武器スロットを検索。
        /// </summary>
        public bool TryGetWeaponById(int weaponId, out NetworkWeaponSlot slot)
        {
            return _weapons.TryGetValue(weaponId, out slot);
        }

        /// <summary>
        /// グローバルバースト制限と武器別発射レートを検証する。
        /// サーバー側で不正なヒット頻度を検出してチート対策を行う。
        /// </summary>
        /// <param name="weaponId">検証対象の武器 ID</param>
        /// <param name="currentTime">現在のゲーム時間（秒）</param>
        /// <returns>ヒットを受け入れる場合 true</returns>
        public bool ValidateHitRate(int weaponId, float currentTime)
        {
            // グローバルバースト検出: 過去1秒以内のヒット総数を制限
            while (_globalHitWindow.Count > 0 && _globalHitWindow.Peek() < currentTime - 1f)
                _globalHitWindow.Dequeue();
            if (_globalHitWindow.Count >= MaxHitsPerSecond)
            {
                Debug.LogWarning($"[WeaponRateLimit] Global burst limit exceeded: {_globalHitWindow.Count}/{MaxHitsPerSecond}");
                return false;
            }

            _globalHitWindow.Enqueue(currentTime);

            // 武器別レート検証
            if (!TryGetWeaponById(weaponId, out var slot)) return false;
            return slot.ValidateFireRate(currentTime);
        }

        /// <summary>
        /// ダメージ倍率を更新。
        /// </summary>
        public void UpdateDamageMultiplier(float multiplier)
        {
            _damageMultiplier = multiplier;
            foreach (var slot in _weapons.Values)
            {
                slot.DamageMultiplier = multiplier;
            }
        }

        public bool HasEmptySlot => _weapons.Count < 6;

        /// <summary>
        /// サーバー権威ヒット処理。
        /// ProcRate判定 → ダメージ計算 → プライマリダメージ適用 → 貫通処理。
        /// </summary>
        public void ProcessHitAuthority(ICombatTarget target, int weaponId, Vector3 playerPos)
        {
            if (!TryGetWeaponById(weaponId, out var slot)) return;
            if (!CalculateHit(slot, out var damage)) return;

            // プライマリターゲット
            ApplyDamageWithKnockback(target, damage, slot.Knockback, playerPos);

            // 貫通処理
            if (slot.Pierce > 0)
            {
                var targetPos = target.CenterPosition;
                var direction = (targetPos - playerPos).normalized;
                var origin = targetPos + direction * 0.1f;
                ProcessPierce(slot, origin, direction, target, playerPos, damage, _runnerService);
            }
        }

        /// <summary>
        /// レベルアップ時の選択肢を取得。
        /// </summary>
        public List<SurvivorWeaponUpgradeOption> GetUpgradeOptions(int count = 3)
        {
            var options = new List<SurvivorWeaponUpgradeOption>();

            // 既存武器のアップグレード（最大レベル未満のみ）
            foreach (var slot in _weapons.Values)
            {
                if (slot.Level >= slot.MaxLevel) continue;

                if (!MemoryDatabase.SurvivorWeaponLevelMasterTable.TryFindByWeaponIdAndLevel(
                        (slot.WeaponId, slot.Level + 1), out var nextLevelMaster))
                    continue;

                MemoryDatabase.SurvivorWeaponMasterTable.TryFindById(slot.WeaponId, out var weaponMaster);

                options.Add(new SurvivorWeaponUpgradeOption
                {
                    WeaponId = slot.WeaponId,
                    WeaponName = slot.Name,
                    IsNewWeapon = false,
                    CurrentLevel = slot.Level,
                    Description = weaponMaster?.Description,
                    UpgradeEffect = nextLevelMaster.Description,
                    IconAssetName = weaponMaster?.IconAssetName
                });
            }

            // 新規武器
            var allWeapons = MemoryDatabase.SurvivorWeaponMasterTable.All;
            foreach (var weaponMaster in allWeapons)
            {
                if (_weapons.ContainsKey(weaponMaster.Id)) continue;

                options.Add(new SurvivorWeaponUpgradeOption
                {
                    WeaponId = weaponMaster.Id,
                    WeaponName = weaponMaster.Name,
                    IsNewWeapon = true,
                    CurrentLevel = 0,
                    Description = weaponMaster.Description,
                    UpgradeEffect = null,
                    IconAssetName = weaponMaster.IconAssetName
                });
            }

            // 決定論的 RNG でランダムに選択（UnityEngine.Random はクライアント/サーバーで非同期のため System.Random を使用）
            int firstLevel = 0;
            foreach (var s in _weapons.Values) { firstLevel = s.Level; break; }
            int seed = firstLevel * 31 + _weapons.Count * 97 + (int)(Time.time * 100);
            var rng = new System.Random(seed);
            var result = new List<SurvivorWeaponUpgradeOption>();
            while (result.Count < count && options.Count > 0)
            {
                int index = rng.Next(0, options.Count);
                result.Add(options[index]);
                options.RemoveAt(index);
            }

            return result;
        }

        #region Private Helpers

        private static bool ApplyLevel(NetworkWeaponSlot slot, IReadOnlyList<SurvivorWeaponLevelMaster> levelMasters, int level)
        {
            var levelMaster = levelMasters?.FirstOrDefault(l => l.Level == level);
            if (levelMaster == null)
            {
                Debug.LogWarning($"[SurvivorNetworkWeaponManager] Level master not found: weaponId={slot.WeaponId}, level={level}");
                return false;
            }

            slot.Level = level;
            slot.Damage = levelMaster.Damage;
            slot.ProcRate = levelMaster.ProcRate;
            slot.CritChance = levelMaster.CritHitRate;
            slot.CritMultiplier = levelMaster.CritHitMultiplier;
            slot.Pierce = levelMaster.Penetration;
            slot.Knockback = levelMaster.Knockback.ToUnit();
            slot.Range = levelMaster.Range.ToUnit();
            slot.ProcInterval = levelMaster.ProcInterval;
            slot.EmitCount = levelMaster.EmitCount;

            return true;
        }

        private static bool CalculateHit(NetworkWeaponSlot slot, out int damage)
        {
            damage = 0;

            if (!slot.ProcRate.RollChance()) return false;

            damage = slot.FinalDamage;
            if (slot.CritChance.RollChance())
            {
                damage = Mathf.RoundToInt(damage * slot.CritMultiplier.ToRate());
            }

            return true;
        }

        private static void ApplyDamageWithKnockback(ICombatTarget target, int damage, float knockback, Vector3 playerPos)
        {
            target.TakeDamage(damage);

            if (knockback > 0)
            {
                var dir = (target.CenterPosition - playerPos).normalized;
                target.ApplyKnockback(dir * knockback);
            }
        }

        private static void ProcessPierce(
            NetworkWeaponSlot slot,
            Vector3 origin,
            Vector3 direction,
            ICombatTarget primaryTarget,
            Vector3 playerPos,
            int damage,
            IFusionRunnerService runnerService)
        {
            var physicsScene = runnerService.GetPhysicsSceneOrDefault();
            int hitCount = physicsScene.SphereCast(
                origin, PierceDetectionRadius, direction, s_pierceHitBuffer, slot.Range,
                LayerMaskConstants.Enemy, QueryTriggerInteraction.Collide);

            System.Array.Sort(s_pierceHitBuffer, 0, hitCount, s_pierceHitComparer);

            int pierceRemaining = slot.Pierce;
            for (int i = 0; i < hitCount && pierceRemaining > 0; i++)
            {
                var target = s_pierceHitBuffer[i].collider.GetComponentInParent<ICombatTarget>();
                if (target == null || target == primaryTarget || target.IsDead) continue;

                target.TakeDamage(damage);
                pierceRemaining--;

                if (slot.Knockback > 0)
                {
                    var dir = (s_pierceHitBuffer[i].collider.transform.position - playerPos).normalized;
                    target.ApplyKnockback(dir * slot.Knockback);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// RaycastHit を距離でソートするコンパレータ
    /// </summary>
    internal sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<UnityEngine.RaycastHit>
    {
        public int Compare(UnityEngine.RaycastHit a, UnityEngine.RaycastHit b)
            => a.distance.CompareTo(b.distance);
    }
}
