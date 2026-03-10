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
        private static readonly int DeathHash = Animator.StringToHash("Death");

        private const float InterpolationSpeed = 8f;

        private readonly Dictionary<int, EnemyProxyData> _proxies = new();
        private readonly Dictionary<int, GameObject> _prefabs = new();
        private IDisposable _subscription;
        private IMasterDataService _masterDataService;
        private IAddressableAssetService _assetService;

        private class EnemyProxyData
        {
            public GameObject GameObject;
            public Animator Animator;
            public Vector3 TargetPosition;
            public int EnemyMasterId;
            public bool IsDead;
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
            proxyTarget.NetworkId = e.NetworkId;

            _proxies[e.NetworkId] = new EnemyProxyData
            {
                GameObject = instance,
                Animator = instance.GetComponentInChildren<Animator>(),
                TargetPosition = pos,
                EnemyMasterId = e.EnemyMasterId,
                IsDead = false
            };
        }

        private void UpdateProxy(SurvivorNetworkEnemyStateSnapshot e)
        {
            if (_proxies.TryGetValue(e.NetworkId, out var data) && !data.IsDead)
            {
                data.TargetPosition = new Vector3(e.PositionX, e.PositionY, e.PositionZ);
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

            // 死亡アニメーション後に破棄
            float deathDuration = GetDeathAnimDuration(data.EnemyMasterId);
            DestroyProxyDelayed(e.NetworkId, deathDuration).Forget();
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
            foreach (var kvp in _proxies)
            {
                var data = kvp.Value;
                if (data.GameObject == null || data.IsDead) continue;

                var currentPos = data.GameObject.transform.position;
                var targetPos = data.TargetPosition;

                // 位置補間（1Hz同期のガタつきを軽減）
                if (Vector3.SqrMagnitude(currentPos - targetPos) > 0.001f)
                {
                    data.GameObject.transform.position = Vector3.Lerp(
                        currentPos, targetPos, Time.deltaTime * InterpolationSpeed);

                    // 移動方向に回転
                    Vector3 moveDir = targetPos - currentPos;
                    moveDir.y = 0f;
                    if (moveDir.sqrMagnitude > 0.01f)
                    {
                        data.GameObject.transform.rotation = Quaternion.Slerp(
                            data.GameObject.transform.rotation,
                            Quaternion.LookRotation(moveDir),
                            Time.deltaTime * InterpolationSpeed);
                    }

                    // 歩行アニメーション
                    if (data.Animator != null)
                    {
                        data.Animator.SetFloat(SpeedHash, 1f);
                    }
                }
                else
                {
                    // 待機アニメーション
                    if (data.Animator != null)
                    {
                        data.Animator.SetFloat(SpeedHash, 0f);
                    }
                }
            }
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
        public int NetworkId { get; set; }
        public Vector3 CenterPosition => transform.position + Vector3.up;
        public bool IsDead => false;
        public void TakeDamage(int damage) { }
        public void ApplyKnockback(Vector3 knockback) { }
    }
}
