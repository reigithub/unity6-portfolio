using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Shared.Shaders
{
    /// <summary>
    /// ディゾルブエフェクトのアニメーション制御
    /// 敵死亡時などのディゾルブ演出に使用
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class DissolveController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Renderer _targetRenderer;

        [Header("Animation Settings")]
        [SerializeField] private float _dissolveDuration = 1.5f;
        [SerializeField] private AnimationCurve _dissolveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private float _delayBeforeDissolve = 0f;

        [Header("Edge Animation")]
        [SerializeField] private bool _animateEdgeColor = true;
        [SerializeField] private Gradient _edgeColorGradient;

        [Header("Direction Animation")]
        [SerializeField] private bool _useDirectionalDissolve = false;
        [SerializeField] private Vector3 _dissolveDirection = Vector3.up;

        // Cached property block for efficient material property updates
        private MaterialPropertyBlock _propertyBlock;
        private Material _instancedMaterial;
        private bool _usePropertyBlock = true;

        // State
        private bool _isDissolving;
        private float _currentDissolveAmount;

        /// <summary>ディゾルブ中かどうか</summary>
        public bool IsDissolving => _isDissolving;

        /// <summary>現在のディゾルブ量 (0-1)</summary>
        public float CurrentDissolveAmount => _currentDissolveAmount;

        /// <summary>ディゾルブ完了イベント</summary>
        public event Action OnDissolveComplete;

        private void Awake()
        {
            if (_targetRenderer == null)
            {
                TryGetComponent(out _targetRenderer);
            }

            _propertyBlock = new MaterialPropertyBlock();

            // デフォルトのグラデーション設定
            if (_edgeColorGradient == null || _edgeColorGradient.colorKeys.Length == 0)
            {
                _edgeColorGradient = new Gradient();
                _edgeColorGradient.SetKeys(
                    new GradientColorKey[]
                    {
                        new(new Color(1f, 0.5f, 0f), 0f),  // Orange
                        new(new Color(1f, 0.2f, 0f), 0.5f), // Red-Orange
                        new(new Color(0.5f, 0f, 0f), 1f)    // Dark Red
                    },
                    new GradientAlphaKey[]
                    {
                        new(1f, 0f),
                        new(1f, 0.8f),
                        new(0f, 1f)
                    }
                );
            }
        }

        private void OnDestroy()
        {
            // インスタンス化したマテリアルを破棄
            if (_instancedMaterial != null)
            {
                Destroy(_instancedMaterial);
                _instancedMaterial = null;
            }
        }

        /// <summary>
        /// ディゾルブを即座に設定（アニメーションなし）
        /// </summary>
        /// <param name="amount">ディゾルブ量 (0-1)</param>
        public void SetDissolveAmount(float amount)
        {
            _currentDissolveAmount = Mathf.Clamp01(amount);
            ApplyDissolveAmount(_currentDissolveAmount);
        }

        /// <summary>
        /// ディゾルブをリセット（元の状態に戻す）
        /// </summary>
        public void ResetDissolve()
        {
            _isDissolving = false;
            _currentDissolveAmount = 0f;
            ApplyDissolveAmount(0f);
        }

        /// <summary>
        /// ディゾルブアニメーションを再生
        /// </summary>
        /// <param name="ct">キャンセルトークン</param>
        /// <returns>完了時にtrue、キャンセル時にfalse</returns>
        public async UniTask<bool> PlayDissolveAsync(CancellationToken ct = default)
        {
            if (_isDissolving) return false;
            if (_targetRenderer == null) return false;

            _isDissolving = true;

            try
            {
                // 開始前のディレイ
                if (_delayBeforeDissolve > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_delayBeforeDissolve), cancellationToken: ct);
                }

                // 方向設定
                if (_useDirectionalDissolve)
                {
                    ApplyDissolveDirection(_dissolveDirection);
                }

                float elapsed = 0f;

                while (elapsed < _dissolveDuration)
                {
                    ct.ThrowIfCancellationRequested();

                    elapsed += Time.deltaTime;
                    float normalizedTime = Mathf.Clamp01(elapsed / _dissolveDuration);
                    float curveValue = _dissolveCurve.Evaluate(normalizedTime);

                    _currentDissolveAmount = curveValue;
                    ApplyDissolveAmount(curveValue);

                    // エッジカラーのアニメーション
                    if (_animateEdgeColor && _edgeColorGradient != null)
                    {
                        Color edgeColor = _edgeColorGradient.Evaluate(normalizedTime);
                        ApplyEdgeColor(edgeColor);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                // 最終値を確実に設定
                _currentDissolveAmount = 1f;
                ApplyDissolveAmount(1f);

                OnDissolveComplete?.Invoke();
                return true;
            }
            catch (OperationCanceledException)
            {
                // キャンセル時は現在の状態を維持
                return false;
            }
            finally
            {
                _isDissolving = false;
            }
        }

        /// <summary>
        /// 逆ディゾルブアニメーション（復元）を再生
        /// </summary>
        public async UniTask<bool> PlayReverseDissolveAsync(CancellationToken ct = default)
        {
            if (_isDissolving) return false;
            if (_targetRenderer == null) return false;

            _isDissolving = true;

            try
            {
                float elapsed = 0f;

                while (elapsed < _dissolveDuration)
                {
                    ct.ThrowIfCancellationRequested();

                    elapsed += Time.deltaTime;
                    float normalizedTime = Mathf.Clamp01(elapsed / _dissolveDuration);
                    float curveValue = 1f - _dissolveCurve.Evaluate(normalizedTime);

                    _currentDissolveAmount = curveValue;
                    ApplyDissolveAmount(curveValue);

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                _currentDissolveAmount = 0f;
                ApplyDissolveAmount(0f);

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                _isDissolving = false;
            }
        }

        /// <summary>
        /// ディゾルブ量をマテリアルに適用
        /// </summary>
        private void ApplyDissolveAmount(float amount)
        {
            if (_targetRenderer == null) return;

            if (_usePropertyBlock)
            {
                _targetRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(ShaderPropertyIds.DissolveAmount, amount);
                _targetRenderer.SetPropertyBlock(_propertyBlock);
            }
            else
            {
                EnsureInstancedMaterial();
                _instancedMaterial.SetFloat(ShaderPropertyIds.DissolveAmount, amount);
            }
        }

        /// <summary>
        /// エッジカラーをマテリアルに適用
        /// </summary>
        private void ApplyEdgeColor(Color color)
        {
            if (_targetRenderer == null) return;

            if (_usePropertyBlock)
            {
                _targetRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(ShaderPropertyIds.EdgeColor, color);
                _targetRenderer.SetPropertyBlock(_propertyBlock);
            }
            else
            {
                EnsureInstancedMaterial();
                _instancedMaterial.SetColor(ShaderPropertyIds.EdgeColor, color);
            }
        }

        /// <summary>
        /// ディゾルブ方向をマテリアルに適用
        /// </summary>
        private void ApplyDissolveDirection(Vector3 direction)
        {
            if (_targetRenderer == null) return;

            Vector4 directionVector = new Vector4(direction.x, direction.y, direction.z, 0f);

            if (_usePropertyBlock)
            {
                _targetRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetVector(ShaderPropertyIds.DissolveDirection, directionVector);
                _propertyBlock.SetFloat(ShaderPropertyIds.DirectionalInfluence, 1f);
                _targetRenderer.SetPropertyBlock(_propertyBlock);
            }
            else
            {
                EnsureInstancedMaterial();
                _instancedMaterial.SetVector(ShaderPropertyIds.DissolveDirection, directionVector);
                _instancedMaterial.SetFloat(ShaderPropertyIds.DirectionalInfluence, 1f);
            }
        }

        /// <summary>
        /// インスタンス化されたマテリアルを確保
        /// </summary>
        private void EnsureInstancedMaterial()
        {
            if (_instancedMaterial == null && _targetRenderer != null)
            {
                _instancedMaterial = _targetRenderer.material; // Creates instance
            }
        }

        /// <summary>
        /// PropertyBlock使用モードを切り替え
        /// </summary>
        /// <param name="usePropertyBlock">true: PropertyBlock使用（推奨）, false: マテリアルインスタンス使用</param>
        public void SetPropertyBlockMode(bool usePropertyBlock)
        {
            _usePropertyBlock = usePropertyBlock;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_targetRenderer == null)
            {
                TryGetComponent(out _targetRenderer);
            }
        }

        /// <summary>
        /// エディタでのテスト用：ディゾルブをプレビュー
        /// </summary>
        [ContextMenu("Preview Dissolve (0.5)")]
        private void PreviewDissolve()
        {
            if (_targetRenderer != null)
            {
                _propertyBlock ??= new MaterialPropertyBlock();
                _targetRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(ShaderPropertyIds.DissolveAmount, 0.5f);
                _targetRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        [ContextMenu("Reset Dissolve Preview")]
        private void ResetDissolvePreview()
        {
            if (_targetRenderer != null)
            {
                _propertyBlock ??= new MaterialPropertyBlock();
                _targetRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(ShaderPropertyIds.DissolveAmount, 0f);
                _targetRenderer.SetPropertyBlock(_propertyBlock);
            }
        }
#endif
    }
}
