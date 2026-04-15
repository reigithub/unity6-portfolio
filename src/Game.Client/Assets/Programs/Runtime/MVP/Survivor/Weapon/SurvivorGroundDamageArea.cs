using System;
using Game.Shared.Constants;
using Game.Shared.Network.Fusion;
using Game.Shared.Network.Survivor;
using UnityEngine;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// 地面設置型ダメージエリア
    /// 接触した敵に継続ダメージを与える
    /// </summary>
    public class SurvivorGroundDamageArea : MonoBehaviour, IPoolableWeaponItem
    {
        [SerializeField] private SphereCollider _damageCollider;
        [SerializeField] private ParticleSystem _vfx;

        // ポーズ参照（生成元マネージャーが設定）
        private SurvivorFusionGameState _gameState;

        // Runner サービス（PhysicsScene・DeltaTime 取得用）
        private IFusionRunnerService _runnerService;

        // OverlapSphere バッファ（allocating 版の代替）
        private static readonly Collider[] s_overlapBuffer = new Collider[32];

        // 状態
        private int _damage;
        private float _procInterval;
        private float _knockback;
        private float _remainingTime;
        private float _nextProcTime;
        private bool _isActive;

        // コールバック
        public event Action<SurvivorGroundDamageArea> OnExpired;
        public event Action<SurvivorGroundDamageArea, Collider> OnHit;

        public int Damage => _damage;
        public float Knockback => _knockback;

        /// <summary>
        /// ダメージエリアを初期化する
        /// </summary>
        /// <param name="gameState">ゲーム状態（ポーズチェック用）</param>
        /// <param name="runnerService">Fusion Runner サービス（PhysicsScene・DeltaTime 取得用）</param>
        public void Initialize(SurvivorFusionGameState gameState, IFusionRunnerService runnerService = null)
        {
            _gameState = gameState;
            _runnerService = runnerService;
        }

        /// <summary>
        /// ダメージエリアを有効化
        /// </summary>
        public void Activate(int damage, float duration, float procInterval,
                             float knockback, float hitboxRadius)
        {
            _damage = damage;
            _procInterval = procInterval;
            _knockback = knockback;
            _remainingTime = duration;
            _nextProcTime = 0f;
            _isActive = true;

            if (_damageCollider != null)
                _damageCollider.radius = hitboxRadius;

            if (_vfx != null)
            {
                _vfx.Clear();
                _vfx.Play();
            }
        }

        private void Update()
        {
            if (!_isActive) return;
            if (_gameState != null && _gameState.IsEffectivelyPaused) return;

            float deltaTime = _runnerService.GetRenderDeltaTime();
            _remainingTime -= deltaTime;
            _nextProcTime -= deltaTime;

            // ProcInterval毎にダメージ判定
            if (_nextProcTime <= 0f)
            {
                _nextProcTime = _procInterval;

                // 現在接触中の敵にOnHitを発火（PhysicsScene 版でバッファ再利用）
                if (_damageCollider != null)
                {
                    var physicsScene = _runnerService.GetPhysicsSceneOrDefault();
                    int count = physicsScene.OverlapSphere(
                        transform.position,
                        _damageCollider.radius,
                        s_overlapBuffer,
                        LayerMaskConstants.Enemy,
                        QueryTriggerInteraction.Collide);

                    for (int i = 0; i < count; i++)
                    {
                        OnHit?.Invoke(this, s_overlapBuffer[i]);
                    }
                }
            }

            // 持続時間終了
            if (_remainingTime <= 0f)
            {
                _isActive = false;
                if (_vfx != null) _vfx.Stop();
                OnExpired?.Invoke(this);
            }
        }

        /// <summary>
        /// ダメージエリアを非活性化
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            _remainingTime = 0f;
            if (_vfx != null) _vfx.Stop();
        }

        /// <summary>
        /// イベントリスナーをクリア
        /// </summary>
        public void ClearListeners()
        {
            OnExpired = null;
            OnHit = null;
        }
    }
}
