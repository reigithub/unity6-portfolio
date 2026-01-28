using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Shared.Services;
using Unity.Profiling;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// SurvivorVFXスポーナー
    /// ヒットエフェクトなどのVFXをプール管理してスポーン
    /// </summary>
    public class SurvivorVfxSpawner : MonoBehaviour
    {
        // Profiler markers
        private static readonly ProfilerMarker s_spawnEffectMarker = new("ProfilerMarker.Vfx.SpawnEffect");
        private static readonly ProfilerMarker s_getFromPoolMarker = new("ProfilerMarker.Vfx.GetFromPool");

        [Header("Settings")]
        [SerializeField] private int _poolSizePerEffect = 20;

        // DI
        [Inject] private IAddressableAssetService _assetService;

        // Pools (AssetName -> Pool)
        private readonly Dictionary<string, Queue<ParticleSystem>> _pools = new();
        private readonly Dictionary<string, GameObject> _prefabCache = new();
        private readonly HashSet<string> _loadingAssets = new();

        public UniTask InitializeAsync()
        {
            Debug.Log("[SurvivorVfxSpawner] Initialized (lazy loading enabled)");
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// エフェクトをプリロード
        /// </summary>
        public async UniTask PreloadEffectAsync(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return;
            if (_pools.ContainsKey(assetName)) return;
            if (_loadingAssets.Contains(assetName)) return;

            _loadingAssets.Add(assetName);

            try
            {
                var prefab = await LoadPrefabAsync(assetName);
                if (prefab == null) return;

                _prefabCache[assetName] = prefab;
                _pools[assetName] = new Queue<ParticleSystem>();

                for (int i = 0; i < _poolSizePerEffect; i++)
                {
                    var ps = CreateParticleSystem(assetName, prefab);
                    if (ps != null)
                    {
                        ps.gameObject.SetActive(false);
                        _pools[assetName].Enqueue(ps);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SurvivorVfxSpawner] PreloadEffectAsync failed for {assetName}: {ex.Message}");
            }
            finally
            {
                _loadingAssets.Remove(assetName);
            }
        }

        private async UniTask<GameObject> LoadPrefabAsync(string assetName)
        {
            try
            {
                return await _assetService.LoadAssetAsync<GameObject>(assetName);
            }
            catch
            {
                Debug.LogWarning($"[SurvivorVfxSpawner] Failed to load prefab: {assetName}");
                return null;
            }
        }

        private ParticleSystem CreateParticleSystem(string assetName, GameObject prefab)
        {
            var instance = Instantiate(prefab, transform);
            if (!instance.TryGetComponent<ParticleSystem>(out var ps))
            {
                Debug.LogWarning($"[SurvivorVfxSpawner] ParticleSystem not found: {assetName}");
                Destroy(instance);
                return null;
            }

            // 自動再生を無効化
            var main = ps.main;
            main.playOnAwake = false;

            return ps;
        }

        /// <summary>
        /// エフェクトをスポーン
        /// </summary>
        public void SpawnEffect(string assetName, Vector3 position, float scale = 1f)
        {
            using (s_spawnEffectMarker.Auto())
            {
                if (string.IsNullOrEmpty(assetName)) return;

                // プールがなければ動的に作成開始
                if (!_pools.ContainsKey(assetName))
                {
                    PreloadEffectAsync(assetName).Forget();
                    return;
                }

                var ps = GetFromPool(assetName);
                if (ps == null)
                {
                    // プールが空の場合は新規作成
                    if (_prefabCache.TryGetValue(assetName, out var prefab))
                    {
                        ps = CreateParticleSystem(assetName, prefab);
                    }

                    if (ps == null) return;
                }

                // 位置とスケール設定
                ps.transform.position = position;
                ps.transform.localScale = Vector3.one * scale;

                // 再生開始
                ps.gameObject.SetActive(true);
                ps.Clear();
                ps.Play();

                // 再生終了後にプールへ返却
                ReturnToPoolAfterPlayAsync(assetName, ps).Forget();
            }
        }

        private ParticleSystem GetFromPool(string assetName)
        {
            using (s_getFromPoolMarker.Auto())
            {
                if (!_pools.TryGetValue(assetName, out var pool)) return null;

                while (pool.Count > 0)
                {
                    var ps = pool.Dequeue();
                    if (ps != null)
                    {
                        return ps;
                    }
                }

                return null;
            }
        }

        private async UniTaskVoid ReturnToPoolAfterPlayAsync(string assetName, ParticleSystem ps)
        {
            try
            {
                // パーティクルが生存している間待機
                var main = ps.main;
                var waitTime = main.duration + main.startLifetime.constantMax;

                // destroyCancellationTokenでMonoBehaviour破棄時に自動キャンセル
                await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: destroyCancellationToken);

                // シーン遷移などでオブジェクトが破棄されている可能性をチェック
                if (ps == null) return;

                // 念のため停止
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);

                // プールへ返却
                if (_pools.TryGetValue(assetName, out var pool))
                {
                    pool.Enqueue(ps);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常なキャンセル（オブジェクト破棄時など）
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SurvivorVfxSpawner] ReturnToPoolAfterPlayAsync failed for {assetName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 全エフェクトをクリア
        /// </summary>
        public void ClearAll()
        {
            // UniTaskはdestroyCancellationTokenで自動キャンセルされる

            foreach (var kvp in _pools)
            {
                foreach (var ps in kvp.Value)
                {
                    if (ps != null)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        Destroy(ps.gameObject);
                    }
                }
            }
            _pools.Clear();

            // ロードしたプレハブをリリース
            foreach (var prefab in _prefabCache.Values)
            {
                _assetService.ReleaseAsset(prefab);
            }
            _prefabCache.Clear();
        }

        private void OnDestroy()
        {
            ClearAll();
        }
    }
}
