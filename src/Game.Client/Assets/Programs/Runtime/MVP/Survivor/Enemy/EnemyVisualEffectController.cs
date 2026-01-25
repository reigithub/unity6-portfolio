using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shared.Shaders;
using UnityEngine;

namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// 敵のビジュアルエフェクト制御
    /// ヒットフラッシュ、ディゾルブなどのマテリアルエフェクトを管理
    /// </summary>
    public class EnemyVisualEffectController : MonoBehaviour
    {
        [Header("Target Renderers")]
        [SerializeField] private Renderer[] _targetRenderers;

        [Header("Hit Flash Settings")]
        [SerializeField] private Color _hitFlashColor = Color.white;
        [SerializeField] private float _hitFlashDuration = 0.15f;
        [SerializeField] private AnimationCurve _hitFlashCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("Dissolve Settings")]
        [SerializeField] private float _dissolveDuration = 1.2f;
        [SerializeField] private float _dissolveDelay = 0.3f;
        [SerializeField] private AnimationCurve _dissolveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private Gradient _dissolveEdgeGradient;
        [SerializeField] private Vector3 _dissolveDirection = Vector3.up;

        // Property Block for efficient material updates (no material instance creation)
        private MaterialPropertyBlock _propertyBlock;

        // State
        private CancellationTokenSource _hitFlashCts;
        private CancellationTokenSource _dissolveCts;
        private bool _isDissolving;

        /// <summary>ディゾルブ中かどうか</summary>
        public bool IsDissolving => _isDissolving;

        /// <summary>ディゾルブ完了イベント</summary>
        public event Action OnDissolveComplete;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();

            // 自動検出
            if (_targetRenderers == null || _targetRenderers.Length == 0)
            {
                _targetRenderers = GetComponentsInChildren<Renderer>();
            }

            // デフォルトグラデーション設定
            if (_dissolveEdgeGradient == null || _dissolveEdgeGradient.colorKeys.Length == 0)
            {
                _dissolveEdgeGradient = new Gradient();
                _dissolveEdgeGradient.SetKeys(
                    new GradientColorKey[]
                    {
                        new(new Color(1f, 0.6f, 0.2f), 0f),    // Orange
                        new(new Color(1f, 0.2f, 0.1f), 0.5f),  // Red
                        new(new Color(0.3f, 0.1f, 0.1f), 1f)   // Dark Red
                    },
                    new GradientAlphaKey[]
                    {
                        new(1f, 0f),
                        new(1f, 0.7f),
                        new(0f, 1f)
                    }
                );
            }
        }

        private void OnEnable()
        {
            // プールからスポーンした際にエフェクトをリセット
            ResetEffectsImmediate();
        }

        private void OnDestroy()
        {
            _hitFlashCts?.Cancel();
            _hitFlashCts?.Dispose();
            _dissolveCts?.Cancel();
            _dissolveCts?.Dispose();
        }

        /// <summary>
        /// ヒットフラッシュを再生
        /// </summary>
        public void PlayHitFlash()
        {
            // 前のフラッシュをキャンセル
            _hitFlashCts?.Cancel();
            _hitFlashCts?.Dispose();
            _hitFlashCts = new CancellationTokenSource();

            PlayHitFlashAsync(_hitFlashCts.Token).Forget();
        }

        private async UniTaskVoid PlayHitFlashAsync(CancellationToken ct)
        {
            try
            {
                float elapsed = 0f;

                while (elapsed < _hitFlashDuration)
                {
                    ct.ThrowIfCancellationRequested();

                    float normalizedTime = elapsed / _hitFlashDuration;
                    float flashAmount = _hitFlashCurve.Evaluate(normalizedTime);

                    SetFlashAmount(flashAmount);

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    elapsed += Time.deltaTime;
                }

                // Reset flash
                SetFlashAmount(0f);
            }
            catch (OperationCanceledException)
            {
                // キャンセル時もリセット
                SetFlashAmount(0f);
            }
        }

        /// <summary>
        /// 死亡ディゾルブを再生
        /// </summary>
        public async UniTask PlayDeathDissolveAsync(CancellationToken ct = default)
        {
            if (_isDissolving) return;

            _isDissolving = true;

            // 前のディゾルブをキャンセル
            _dissolveCts?.Cancel();
            _dissolveCts?.Dispose();
            _dissolveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var linkedCt = _dissolveCts.Token;

            try
            {
                // ディレイ
                if (_dissolveDelay > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_dissolveDelay), cancellationToken: linkedCt);
                }

                // 方向設定
                SetDissolveDirection(_dissolveDirection);

                float elapsed = 0f;

                while (elapsed < _dissolveDuration)
                {
                    linkedCt.ThrowIfCancellationRequested();

                    float normalizedTime = elapsed / _dissolveDuration;
                    float dissolveAmount = _dissolveCurve.Evaluate(normalizedTime);

                    SetDissolveAmount(dissolveAmount);

                    // エッジカラー更新
                    Color edgeColor = _dissolveEdgeGradient.Evaluate(normalizedTime);
                    SetEdgeColor(edgeColor);

                    await UniTask.Yield(PlayerLoopTiming.Update, linkedCt);
                    elapsed += Time.deltaTime;
                }

                // 完全にディゾルブ
                SetDissolveAmount(1f);

                OnDissolveComplete?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // キャンセル時
            }
            finally
            {
                _isDissolving = false;
            }
        }

        /// <summary>
        /// エフェクトをリセット（プールに戻すとき）
        /// </summary>
        public void ResetEffects()
        {
            _hitFlashCts?.Cancel();
            _dissolveCts?.Cancel();

            _isDissolving = false;

            ResetEffectsImmediate();
        }

        /// <summary>
        /// エフェクトを即座にリセット
        /// </summary>
        private void ResetEffectsImmediate()
        {
            if (_targetRenderers == null) return;
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            foreach (var renderer in _targetRenderers)
            {
                if (renderer == null) continue;

                // 既存のPropertyBlockを取得してから値を上書き（Clear()しない）
                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(ShaderPropertyIds.FlashAmount, 0f);
                _propertyBlock.SetFloat(ShaderPropertyIds.DissolveAmount, 0f);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        #region Material Property Setters

        private void SetFlashAmount(float amount)
        {
            foreach (var renderer in _targetRenderers)
            {
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(ShaderPropertyIds.FlashAmount, amount);
                _propertyBlock.SetColor(ShaderPropertyIds.FlashColor, _hitFlashColor);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void SetDissolveAmount(float amount)
        {
            foreach (var renderer in _targetRenderers)
            {
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(ShaderPropertyIds.DissolveAmount, amount);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void SetEdgeColor(Color color)
        {
            foreach (var renderer in _targetRenderers)
            {
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(ShaderPropertyIds.EdgeColor, color);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void SetDissolveDirection(Vector3 direction)
        {
            Vector4 dir4 = new Vector4(direction.x, direction.y, direction.z, 0f);

            foreach (var renderer in _targetRenderers)
            {
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetVector(ShaderPropertyIds.DissolveDirection, dir4);
                _propertyBlock.SetFloat(ShaderPropertyIds.DirectionalInfluence, 0.5f);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        #endregion

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_targetRenderers == null || _targetRenderers.Length == 0)
            {
                _targetRenderers = GetComponentsInChildren<Renderer>();
            }
        }

        [ContextMenu("Test Hit Flash")]
        private void TestHitFlash()
        {
            if (Application.isPlaying)
            {
                PlayHitFlash();
            }
        }

        [ContextMenu("Test Dissolve")]
        private void TestDissolve()
        {
            if (Application.isPlaying)
            {
                PlayDeathDissolveAsync(destroyCancellationToken).Forget();
            }
        }
#endif
    }
}
