using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.MVP.Survivor.Enemy;
using Game.Shared.Services;
using R3;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Item
{
    /// <summary>
    /// 経験値オーブスポーナー（後方互換性用）
    /// 新規実装ではSurvivorItemSpawnerを使用してください
    /// </summary>
    [Obsolete("Use SurvivorItemSpawner instead. This class is kept for backward compatibility.")]
    public class SurvivorExperienceOrbSpawner : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string _orbAssetAddress = "SurvivorExperienceOrb";

        [SerializeField] private int _poolSize = 100;

        // DI
        [Inject] private IAddressableAssetService _assetService;

        // Pool - SurvivorItemを使用（SurvivorExperienceOrbはSurvivorItemを継承）
        private readonly Queue<SurvivorItem> _pool = new();
        private readonly List<SurvivorItem> _activeOrbs = new();
        private GameObject _orbPrefab;

        // Events
        private readonly Subject<int> _onExperienceCollected = new();
        public Observable<int> OnExperienceCollected => _onExperienceCollected;

        public async UniTask InitializeAsync()
        {
            // IAddressableAssetService経由でアセット読み込み
            _orbPrefab = await _assetService.LoadAssetAsync<GameObject>(_orbAssetAddress);

            // プール初期化
            for (int i = 0; i < _poolSize; i++)
            {
                var orb = CreateOrb();
                orb.gameObject.SetActive(false);
                _pool.Enqueue(orb);
            }

            Debug.Log($"[SurvivorItemSpawner] Initialized with pool size: {_poolSize}");
        }

        private SurvivorItem CreateOrb()
        {
            var instance = Instantiate(_orbPrefab, transform);
            var orb = instance.GetComponent<SurvivorItem>();

            if (orb == null)
            {
                orb = instance.AddComponent<SurvivorItem>();
                orb.InitializeAsExperience(5);
            }

            orb.OnCollected += OnOrbCollected;

            return orb;
        }

        /// <summary>
        /// 敵が倒された位置にオーブをスポーン
        /// </summary>
        public void SpawnOrb(Vector3 position, int experienceValue)
        {
            var orb = GetFromPool();
            if (orb == null)
            {
                orb = CreateOrb();
            }

            orb.Reset();
            orb.SetPosition(position);
            orb.ExperienceValue = experienceValue;
            orb.gameObject.SetActive(true);

            _activeOrbs.Add(orb);
        }

        /// <summary>
        /// 敵の死亡イベントに接続
        /// </summary>
        public void ConnectToEnemySpawner(SurvivorEnemySpawner enemySpawner)
        {
            enemySpawner.OnEnemyKilled
                .Subscribe(enemy => { SpawnOrb(enemy.transform.position, enemy.ExperienceValue); })
                .AddTo(this);
        }

        private SurvivorItem GetFromPool()
        {
            while (_pool.Count > 0)
            {
                var orb = _pool.Dequeue();
                if (orb != null)
                {
                    return orb;
                }
            }

            return null;
        }

        private void ReturnToPool(SurvivorItem orb)
        {
            _activeOrbs.Remove(orb);
            _pool.Enqueue(orb);
        }

        private void OnOrbCollected(SurvivorItem orb)
        {
            _onExperienceCollected.OnNext(orb.ExperienceValue);
            ReturnToPool(orb);
        }

        /// <summary>
        /// 全てのオーブをクリア
        /// </summary>
        public void ClearAllOrbs()
        {
            foreach (var orb in _activeOrbs.ToArray())
            {
                orb.gameObject.SetActive(false);
                ReturnToPool(orb);
            }

            _activeOrbs.Clear();
        }

        private void OnDestroy()
        {
            _onExperienceCollected.Dispose();
        }
    }
}