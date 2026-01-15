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
    /// 経験値オーブスポーナー
    /// 敵が倒された時に経験値オーブを生成
    /// </summary>
    public class ExperienceOrbSpawner : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string _orbAssetAddress = "ExperienceOrb";
        [SerializeField] private int _poolSize = 100;

        // DI
        [Inject] private IAddressableAssetService _assetService;

        // Pool
        private readonly Queue<ExperienceOrb> _pool = new();
        private readonly List<ExperienceOrb> _activeOrbs = new();
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

            Debug.Log($"[ExperienceOrbSpawner] Initialized with pool size: {_poolSize}");
        }

        private ExperienceOrb CreateOrb()
        {
            var instance = Instantiate(_orbPrefab, transform);
            var orb = instance.GetComponent<ExperienceOrb>();

            if (orb == null)
            {
                orb = instance.AddComponent<ExperienceOrb>();
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
                .Subscribe(enemy =>
                {
                    SpawnOrb(enemy.transform.position, enemy.ExperienceValue);
                })
                .AddTo(this);
        }

        private ExperienceOrb GetFromPool()
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

        private void ReturnToPool(ExperienceOrb orb)
        {
            _activeOrbs.Remove(orb);
            _pool.Enqueue(orb);
        }

        private void OnOrbCollected(ExperienceOrb orb, int experience)
        {
            _onExperienceCollected.OnNext(experience);
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
