using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Dto;
using Game.Shared.Combat;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using UnityEngine;

namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// クライアントモード時、バッチ ClientRpc からプロキシ敵オブジェクトを管理。
    /// サーバーからの EnemyMasterId でAddressableプレハブをロードし、正式モデルで表示する。
    /// </summary>
    public class SurvivorEnemyView : MonoBehaviour
    {
        // Animator hashes（SurvivorEnemyPresenter と同一）
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int DeathHash = Animator.StringToHash("Death");

        private const float InterpolationSpeed = 8f;
        private const float CorrectionDecayRate = 10f;
        private const float MaxCorrectionDistance = 3f;

        private readonly Dictionary<int, EnemyProxyData> _proxies = new();
        private readonly Dictionary<int, GameObject> _prefabs = new();
        private IDisposable _subscription;
        private IMasterDataService _masterDataService;
        private IAddressableAssetService _assetService;

        /// <summary>ネットワーク同期の位置補間状態</summary>
        private struct EnemyProxyInterpolation
        {
            public Vector3 LastSyncPosition;
            public Vector3 Velocity;
            public float TimeSinceSync;
            public Vector3 CorrectionOffset;

            public void OnSyncReceived(Vector3 serverPos, Vector3 serverVel, float maxCorrectionDist)
            {
                var predicted = LastSyncPosition + Velocity * TimeSinceSync + CorrectionOffset;
                CorrectionOffset = predicted - serverPos;
                if (CorrectionOffset.sqrMagnitude > maxCorrectionDist * maxCorrectionDist)
                    CorrectionOffset = Vector3.zero;
                LastSyncPosition = serverPos;
                Velocity = serverVel;
                TimeSinceSync = 0f;
            }

            public Vector3 GetPosition(float deltaTime, float correctionDecayRate)
            {
                TimeSinceSync += deltaTime;
                var predicted = LastSyncPosition + Velocity * TimeSinceSync;
                CorrectionOffset = Vector3.Lerp(CorrectionOffset, Vector3.zero, correctionDecayRate * deltaTime);
                return predicted + CorrectionOffset;
            }
        }

        private class EnemyProxyData
        {
            public GameObject GameObject;
            public Animator Animator;
            public int EnemyMasterId;
            public bool IsDead;
            public float DeathAnimDuration;
            public EnemyProxyInterpolation Interpolation;
        }

        public async UniTask InitializeAsync(
            ISubscriber<SurvivorSignals.Enemy.BatchUpdated> subscriber,
            IMasterDataService masterDataService,
            IAddressableAssetService assetService)
        {
            _masterDataService = masterDataService;
            _assetService = assetService;

            // 全敵プレハブをプリロード
            var allEnemies = masterDataService.MemoryDatabase.SurvivorEnemyMasterTable.All;
            foreach (var enemy in allEnemies)
            {
                if (!_prefabs.ContainsKey(enemy.Id))
                {
                    var prefab = await assetService.LoadAssetAsync<GameObject>(enemy.AssetName);
                    _prefabs[enemy.Id] = prefab;
                }
            }

            _subscription = subscriber.Subscribe(signal => OnReceived(signal.Enemies));
            Debug.Log($"[SurvivorEnemyView] Initialized: prefabs={_prefabs.Count}");
        }

        private void OnReceived(SurvivorNetworkEnemyStateSnapshot[] enemies)
        {
            foreach (var e in enemies)
            {
                switch (e.SyncType)
                {
                    case EnemySyncType.Spawn:
                        SpawnProxy(e);
                        break;
                    case EnemySyncType.PositionUpdate:
                        UpdateProxy(e);
                        break;
                    case EnemySyncType.Attack:
                        UpdateProxy(e);
                        HandleAttack(e);
                        break;
                    case EnemySyncType.Death:
                        HandleDeath(e);
                        break;
                }
            }
        }

        private void SpawnProxy(SurvivorNetworkEnemyStateSnapshot e)
        {
            // 既存プロキシがある場合は破棄（ネットワークID再利用時の安全策）
            if (_proxies.TryGetValue(e.NetworkId, out var existing))
            {
                if (existing.GameObject != null) Destroy(existing.GameObject);
                _proxies.Remove(e.NetworkId);
            }

            GameObject instance;
            if (_prefabs.TryGetValue(e.EnemyMasterId, out var prefab) && prefab != null)
            {
                instance = Instantiate(prefab, transform);

                // サーバー専用コンポーネントを除去（クライアントではAI/物理不要）
                var controller = instance.GetComponent<SurvivorEnemyController>();
                if (controller != null) Destroy(controller);

                var presenter = instance.GetComponent<SurvivorEnemyPresenter>();
                if (presenter != null) Destroy(presenter);

                var navAgent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (navAgent != null) navAgent.enabled = false;
            }
            else
            {
                // フォールバック: プレハブ未ロード時
                instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Debug.LogWarning($"[SurvivorEnemyView] Prefab not found for enemy {e.EnemyMasterId}, using fallback");
            }

            instance.name = $"EnemyProxy_{e.NetworkId}";
            var pos = new Vector3(e.PositionX, e.PositionY, e.PositionZ);
            instance.transform.position = pos;

            // Enemyレイヤー設定（子オブジェクト含む — LockOn/SphereCast検出用）
            SetLayerRecursively(instance, LayerConstants.Enemy);

            // 全Colliderをトリガーに変更（物理衝突なし、検出のみ）
            foreach (var col in instance.GetComponentsInChildren<Collider>())
            {
                col.isTrigger = true;
            }

            // ICombatTarget実装を追加（ヒット報告用NetworkId + LockOn用CenterPosition）
            var proxyTarget = instance.AddComponent<EnemyProxyTarget>();
            proxyTarget.OwnerView = this;
            proxyTarget.NetworkId = e.NetworkId;

            _proxies[e.NetworkId] = new EnemyProxyData
            {
                GameObject = instance,
                Animator = instance.GetComponentInChildren<Animator>(),
                EnemyMasterId = e.EnemyMasterId,
                IsDead = false,
                DeathAnimDuration = GetDeathAnimDuration(e.EnemyMasterId),
                Interpolation = new EnemyProxyInterpolation
                {
                    LastSyncPosition = pos,
                    Velocity = Vector3.zero,
                    TimeSinceSync = 0f,
                    CorrectionOffset = Vector3.zero
                }
            };
        }

        private void UpdateProxy(SurvivorNetworkEnemyStateSnapshot e)
        {
            if (!_proxies.TryGetValue(e.NetworkId, out var data) || data.IsDead) return;

            var serverPos = new Vector3(e.PositionX, e.PositionY, e.PositionZ);
            var serverVel = new Vector3(e.VelocityX, e.VelocityY, e.VelocityZ);
            data.Interpolation.OnSyncReceived(serverPos, serverVel, MaxCorrectionDistance);
        }

        private void HandleAttack(SurvivorNetworkEnemyStateSnapshot e)
        {
            if (!_proxies.TryGetValue(e.NetworkId, out var data)) return;
            if (data.IsDead) return;

            if (data.Animator != null)
            {
                data.Animator.SetFloat(SpeedHash, 0f);
                data.Animator.SetTrigger(AttackHash);
            }
        }

        private void HandleDeath(SurvivorNetworkEnemyStateSnapshot e)
        {
            if (!_proxies.TryGetValue(e.NetworkId, out var data)) return;
            if (data.IsDead) return;

            data.IsDead = true;

            // 死亡アニメーション
            if (data.Animator != null)
            {
                data.Animator.SetFloat(SpeedHash, 0f);
                data.Animator.SetTrigger(DeathHash);
            }

            // コライダー無効化（死亡後の命中防止）
            foreach (var col in data.GameObject.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            // 死亡アニメーション後に破棄（SpawnProxy 時にキャッシュ済み）
            DestroyProxyDelayed(e.NetworkId, data.DeathAnimDuration).Forget();
        }

        private float GetDeathAnimDuration(int enemyMasterId)
        {
            var table = _masterDataService?.MemoryDatabase?.SurvivorEnemyMasterTable;
            if (table != null && table.TryFindById(enemyMasterId, out var master))
            {
                return master.DeathAnimDuration.ToSeconds();
            }
            return 2f;
        }

        private async UniTaskVoid DestroyProxyDelayed(int networkId, float delay)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), ignoreTimeScale: true);
            DespawnProxy(networkId);
        }

        private void DespawnProxy(int id)
        {
            if (_proxies.TryGetValue(id, out var data))
            {
                if (data.GameObject != null) Destroy(data.GameObject);
                _proxies.Remove(id);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            foreach (var kvp in _proxies)
            {
                var data = kvp.Value;
                if (data.GameObject == null || data.IsDead) continue;

                // 1. 補間済み位置を取得して反映
                data.GameObject.transform.position = data.Interpolation.GetPosition(dt, CorrectionDecayRate);

                // 2. 回転: 速度方向を向く
                Vector3 moveDir = data.Interpolation.Velocity;
                moveDir.y = 0f;
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    data.GameObject.transform.rotation = Quaternion.Slerp(
                        data.GameObject.transform.rotation,
                        Quaternion.LookRotation(moveDir),
                        dt * InterpolationSpeed);
                }

                // 5. アニメーション: 速度ベースで歩行/待機
                if (data.Animator != null)
                {
                    data.Animator.SetFloat(SpeedHash, data.Interpolation.Velocity.magnitude > 0.1f ? 1f : 0f);
                }
            }
        }

        /// <summary>プロキシの死亡状態を返す（EnemyProxyTarget.IsDead から参照）</summary>
        public bool IsProxyDead(int networkId)
        {
            return !_proxies.TryGetValue(networkId, out var data) || data.IsDead;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
            foreach (var data in _proxies.Values)
            {
                if (data.GameObject != null) Destroy(data.GameObject);
            }
            _proxies.Clear();

            // プレハブリリース
            foreach (var prefab in _prefabs.Values)
            {
                _assetService?.ReleaseAsset(prefab);
            }
            _prefabs.Clear();
        }
    }

    /// <summary>
    /// クライアント敵プロキシ用ターゲットコンポーネント。
    /// LockOnServiceがOverlapSphereで検出し、CenterPositionを取得する。
    /// ICombatTarget実装: TakeDamage/ApplyKnockbackはno-op（ダメージはRPC経由でサーバーが処理）。
    /// </summary>
    public class EnemyProxyTarget : MonoBehaviour, ICombatTarget
    {
        public SurvivorEnemyView OwnerView { get; set; }
        public int NetworkId { get; set; }
        public Vector3 CenterPosition => transform.position + Vector3.up;
        public bool IsDead => OwnerView != null && OwnerView.IsProxyDead(NetworkId);
        public void TakeDamage(int damage) { }
        public void ApplyKnockback(Vector3 knockback) { }
    }
}
