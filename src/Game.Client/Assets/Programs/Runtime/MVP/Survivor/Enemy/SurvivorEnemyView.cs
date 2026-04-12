using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Dto;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using Unity.Profiling;
using UnityEngine;

namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// クライアントモード時、バッチ ClientRpc からプロキシ敵オブジェクトを管理。
    /// サーバーからの EnemyMasterId でAddressableプレハブをロードし、正式モデルで表示する。
    /// </summary>
    public class SurvivorEnemyView : MonoBehaviour
    {
        private const float InterpolationSpeed = 8f;
        private const float CorrectionDecayRate = 10f;
        private const float MaxCorrectionDistance = 3f;

        // LODティア: 距離²と更新間隔（フレーム数）
        private const float NearDistanceSq = 20f * 20f;   // 400
        private const float MidDistanceSq = 40f * 40f;    // 1600
        private const int NearUpdateInterval = 1;           // 毎フレーム
        private const int MidUpdateInterval = 2;            // 2フレームに1回
        private const int FarUpdateInterval = 5;            // 5フレームに1回

        private static readonly ProfilerMarker s_updateMarker = new("ProfilerMarker.EnemyView.Update");
        private static readonly ProfilerMarker s_frustumMarker = new("ProfilerMarker.EnemyView.FrustumCalc");
        private static readonly ProfilerMarker s_spawnProxyMarker = new("ProfilerMarker.EnemyView.SpawnProxy");

        private readonly Dictionary<int, EnemyProxyData> _proxies = new();
        private readonly Dictionary<int, GameObject> _prefabs = new();
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private IDisposable _subscription;
        private IMasterDataService _masterDataService;
        private IAddressableAssetService _assetService;
        private Camera _camera;

        public async UniTask InitializeAsync(
            ISubscriber<SurvivorSignals.Enemy.BatchUpdated> subscriber,
            IMasterDataService masterDataService,
            IAddressableAssetService assetService,
            Camera mainCamera = null)
        {
            _masterDataService = masterDataService;
            _assetService = assetService;
            _camera = mainCamera;

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

            _subscription = subscriber.Subscribe(signal => OnReceived(signal.Enemies, signal.Count));
            Debug.Log($"[SurvivorEnemyView] Initialized: prefabs={_prefabs.Count}");
        }

        private void OnReceived(SurvivorNetworkEnemyStateSnapshot[] enemies, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var e = enemies[i];
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
            using var spawnScope = s_spawnProxyMarker.Auto();

            // 既存プロキシがある場合は破棄（ネットワークID再利用時の安全策）
            if (_proxies.TryGetValue(e.NetworkId, out var existing))
            {
                if (existing.GameObject != null) Destroy(existing.GameObject);
                _proxies.Remove(e.NetworkId);
            }

            GameObject instance;
            var pos = new Vector3(e.PositionX, e.PositionY, e.PositionZ);
            if (_prefabs.TryGetValue(e.EnemyMasterId, out var prefab) && prefab != null)
            {
                // プレハブを一時的に非アクティブ化して Instantiate することで、
                // NavMeshAgent が NavMesh 外の位置（原点）で Awake するエラーを防ぐ
                prefab.SetActive(false);
                instance = Instantiate(prefab, transform);
                prefab.SetActive(true);

                // サーバー専用コンポーネントを除去（クライアントではAI/物理不要）
                // StripForProxy が NavMeshAgent と Controller の両方を Destroy する
                if (instance.TryGetComponent<SurvivorEnemyController>(out var controller))
                    controller.StripForProxy();

                // 正しいサーバー位置に配置してからアクティブ化
                instance.transform.position = pos;
                instance.SetActive(true);
            }
            else
            {
                // フォールバック: プレハブ未ロード時
                instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                instance.transform.position = pos;
                Debug.LogWarning($"[SurvivorEnemyView] Prefab not found for enemy {e.EnemyMasterId}, using fallback");
            }

            instance.name = $"EnemyProxy_{e.NetworkId}";

            // Enemyレイヤー設定（子オブジェクト含む — LockOn/SphereCast検出用）
            SetLayerRecursively(instance, LayerConstants.Enemy);

            // 全Colliderをトリガーに変更してキャッシュ（HandleDeath での再探索を排除）
            var colliders = instance.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.isTrigger = true;
            }

            // ICombatTarget実装を追加（ヒット報告用NetworkId + LockOn用CenterPosition）
            var proxyTarget = instance.AddComponent<EnemyProxyTarget>();
            proxyTarget.OwnerView = this;
            proxyTarget.NetworkId = e.NetworkId;

            // Animator は Root に配置済み
            instance.TryGetComponent<Animator>(out var animator);
            instance.TryGetComponent<EnemyVisualEffectController>(out var vfxController);
            _proxies[e.NetworkId] = new EnemyProxyData
            {
                GameObject = instance,
                Animator = animator,
                Colliders = colliders,
                EnemyMasterId = e.EnemyMasterId,
                IsDead = false,
                DeathAnimDuration = GetDeathAnimDuration(e.EnemyMasterId),
                FrameOffset = e.NetworkId % FarUpdateInterval,
                VfxController = vfxController,
                PreviousHp = e.CurrentHp,
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

            // HP 減少を検知してヒットフラッシュ再生
            if (e.CurrentHp < data.PreviousHp)
            {
                data.PlayHitFlash();
            }
            data.PreviousHp = e.CurrentHp;
        }

        private void HandleAttack(SurvivorNetworkEnemyStateSnapshot e)
        {
            if (!_proxies.TryGetValue(e.NetworkId, out var data)) return;
            if (data.IsDead) return;

            data.PlayAttack();
        }

        private void HandleDeath(SurvivorNetworkEnemyStateSnapshot e)
        {
            if (!_proxies.TryGetValue(e.NetworkId, out var data)) return;
            if (data.IsDead) return;

            data.PlayDeath();

            // ディゾルブ完了まで待ってから破棄（DeathAnimDuration とディゾルブ時間の長い方を使用）
            float delay = Mathf.Max(data.DeathAnimDuration, data.VfxController?.TotalDissolveDuration ?? 0f);
            DestroyProxyDelayed(e.NetworkId, delay).Forget();
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
            using var updateScope = s_updateMarker.Auto();

            float dt = Time.deltaTime;
            int frameCount = Time.frameCount;

            // カメラがあれば視錐台平面をキャッシュ（1回/フレーム）
            Vector3 cameraPos = Vector3.zero;
            if (_camera != null)
            {
                using var frustumScope = s_frustumMarker.Auto();
                GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);
                cameraPos = _camera.transform.position;
            }

            foreach (var kvp in _proxies)
            {
                var data = kvp.Value;
                if (data.GameObject == null || data.IsDead) continue;

                // LODティアを定期的に再分類（FarUpdateInterval毎）
                if (_camera != null && (frameCount + data.FrameOffset) % FarUpdateInterval == 0)
                {
                    data.LodUpdateInterval = ClassifyLod(data, cameraPos);
                }

                // このフレームが更新対象でなければスキップ
                if (data.LodUpdateInterval > 1 &&
                    frameCount % data.LodUpdateInterval != data.FrameOffset % data.LodUpdateInterval)
                {
                    continue;
                }

                UpdateProxyTransform(data, dt);
            }
        }

        private int ClassifyLod(EnemyProxyData data, Vector3 cameraPos)
        {
            var proxyPos = data.GameObject.transform.position;
            float distSq = (proxyPos - cameraPos).sqrMagnitude;

            if (distSq <= NearDistanceSq)
            {
                var bounds = new Bounds(proxyPos, Vector3.one);
                if (GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                    return NearUpdateInterval;
            }

            if (distSq <= MidDistanceSq)
                return MidUpdateInterval;

            return FarUpdateInterval;
        }

        private void UpdateProxyTransform(EnemyProxyData data, float dt)
        {
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

            // 3. アニメーション: 速度ベースで歩行/待機
            data.UpdateAnimatorSpeed(data.Interpolation.Velocity.magnitude);
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
}
