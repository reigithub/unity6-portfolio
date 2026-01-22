using System;
using Cysharp.Threading.Tasks;
using Game.Shared.LockOn;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Shared.Services
{
    /// <summary>
    /// ロックオンサービス実装
    /// 対象をロックオンして武器がその対象を優先的に狙う機能
    /// UIオーバーレイで常に手前に表示
    /// </summary>
    public class LockOnService : ILockOnService, IDisposable
    {
        private const string IndicatorAssetName = "LockOnIndicator";
        private const float HitRadius = 1.5f; // クリック判定の半径（大きいほど当たりやすい）
        private const float MaxRayDistance = 100f;
        private const float FrontNarrowAngle = 0.707f; // 前方90度（左右45度）= cos(45°)
        private const float FrontWideAngle = 0f;       // 前方180度（左右90度）= cos(90°)

        private readonly IAddressableAssetService _assetService;
        private readonly ReactiveProperty<Transform> _currentTarget = new();
        private readonly Collider[] _hitBuffer = new Collider[50];
        private Camera _camera;
        private int _layer;
        private Transform _owner;
        private float _searchRange = 50f;

        private GameObject _indicatorPrefab;
        private GameObject _indicatorInstance;
        private LockOnIndicator _indicatorComponent;
        private Canvas _overlayCanvas;

        public LockOnService(IAddressableAssetService assetService)
        {
            _assetService = assetService;
        }

        public void Initialize(Camera camera, int layer)
        {
            _camera = camera;
            _layer = layer;
        }

        public bool HasTarget()
        {
            var target = _currentTarget.Value;
            return target != null && target.gameObject.activeInHierarchy;
        }

        public bool TryGetTarget(out Transform target, bool autoTarget = true)
        {
            // ロックオン優先
            if (TryGetTargetInternal(out target)) return true;

            if (!autoTarget) return false;

            // ロックオンがなければ自動ターゲット選択を試みる
            UpdateAutoTarget();

            // 自動選択後に再度チェック
            return TryGetTargetInternal(out target);
        }

        private bool TryGetTargetInternal(out Transform target)
        {
            if (HasTarget())
            {
                target = _currentTarget.CurrentValue;
                return true;
            }

            target = null;
            return false;
        }

        public void SetTarget(Vector2 point)
        {
            if (_camera == null)
                return;

            var ray = _camera.ScreenPointToRay(point);
            int layerMask = 1 << _layer;

            // SphereCastで広い判定範囲を持たせる（クリックしやすくする）
            if (Physics.SphereCast(ray, HitRadius, out var hit, MaxRayDistance, layerMask))
            {
                if (hit.collider != null)
                {
                    SetTargetInternal(hit.collider.transform);
                }
            }
            else
            {
                ClearTarget();
            }
        }

        private void SetTargetInternal(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                ClearTarget();
                return;
            }

            // 同じターゲットなら何もしない
            if (_currentTarget.Value == target)
            {
                return;
            }

            // 前のターゲットをクリア
            ClearTargetInternal();

            // 新しいターゲットを設定
            _currentTarget.Value = target;

            // インジケーターを表示
            ShowIndicatorAsync(target).Forget();

            Debug.Log($"[LockOnService] Locked on to: {target.name}");
        }

        public void ClearTarget()
        {
            if (_currentTarget.Value != null)
            {
                Debug.Log($"[LockOnService] Lock-on cleared");
            }

            ClearTargetInternal();
            _currentTarget.Value = null;
        }

        private void ClearTargetInternal()
        {
            // インジケーターを非表示
            HideIndicator();
        }

        public void SetAutoTarget(Transform owner)
        {
            _owner = owner;
        }

        public void UpdateAutoTarget()
        {
            // ターゲットがまだ有効なら何もしない
            if (HasTarget()) return;

            // 自動選択のパラメータが設定されていない場合は何もしない
            if (_owner == null || _camera == null) return;

            // カメラ方向優先で敵を検索
            var newTarget = FindBestTarget();
            if (newTarget != null)
            {
                SetTargetInternal(newTarget);
            }
        }

        private void EnsureOverlayCanvas()
        {
            if (_overlayCanvas != null) return;

            // Screen Space - Overlayキャンバスを作成（常に最前面に表示）
            var canvasObj = new GameObject("LockOnCanvas");
            _overlayCanvas = canvasObj.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 0; // 他のUIより手前

            // CanvasScalerを追加（スケーリング対応）
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // GraphicRaycasterは不要（クリック検出しない）
            UnityEngine.Object.DontDestroyOnLoad(canvasObj);
        }

        private async UniTask ShowIndicatorAsync(Transform target)
        {
            // プレハブがまだロードされていなければロード
            if (_indicatorPrefab == null)
            {
                try
                {
                    _indicatorPrefab = await _assetService.LoadAssetAsync<GameObject>(IndicatorAssetName);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LockOnService] Failed to load indicator prefab: {e.Message}");
                    return;
                }
            }

            // 既存のインジケーターを削除
            HideIndicator();

            // ターゲットがまだ有効か確認
            if (target == null || !target.gameObject.activeInHierarchy || _currentTarget.Value != target)
            {
                return;
            }

            // オーバーレイキャンバスを確保
            EnsureOverlayCanvas();

            // インジケーターをCanvas上に生成
            _indicatorInstance = UnityEngine.Object.Instantiate(_indicatorPrefab, _overlayCanvas.transform);

            // LockOnIndicatorコンポーネントを取得してターゲットを設定
            _indicatorComponent = _indicatorInstance.GetComponent<LockOnIndicator>();
            if (_indicatorComponent != null)
            {
                _indicatorComponent.SetTarget(target);
                _indicatorComponent.SetCamera(_camera);
            }
        }

        private void HideIndicator()
        {
            if (_indicatorInstance != null)
            {
                UnityEngine.Object.Destroy(_indicatorInstance);
                _indicatorInstance = null;
                _indicatorComponent = null;
            }
        }

        public async UniTask PreloadAsync()
        {
            // プレハブを事前ロード
            if (_indicatorPrefab == null)
            {
                try
                {
                    _indicatorPrefab = await _assetService.LoadAssetAsync<GameObject>(IndicatorAssetName);
                    Debug.Log("[LockOnService] Indicator prefab preloaded");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LockOnService] Failed to preload indicator prefab: {e.Message}");
                }
            }

            // キャンバスを事前作成
            EnsureOverlayCanvas();
        }

        /// <summary>
        /// カメラ方向を優先しつつ、最適なターゲットを検索
        /// 優先度:
        /// 1. 前方90度以内（左右45度）で最も近い敵
        /// 2. 前方180度以内（左右90度）で最も近い敵
        /// 3. それ以外（後方）で最も近い敵
        /// </summary>
        private Transform FindBestTarget()
        {
            int layerMask = 1 << _layer;
            int hitCount = Physics.OverlapSphereNonAlloc(_owner.position, _searchRange, _hitBuffer, layerMask);

            if (hitCount == 0) return null;

            // 優先度1: 前方90度以内
            Transform bestNarrow = null;
            float nearestDistNarrow = float.MaxValue;

            // 優先度2: 前方180度以内
            Transform bestWide = null;
            float nearestDistWide = float.MaxValue;

            // 優先度3: それ以外（後方）
            Transform bestBack = null;
            float nearestDistBack = float.MaxValue;

            Vector3 cameraForward = _camera.transform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            for (int i = 0; i < hitCount; i++)
            {
                var collider = _hitBuffer[i];
                if (collider == null) continue;

                // 対象が有効かチェック（死亡チェックはコライダーのアクティブ状態で判断）
                if (!collider.gameObject.activeInHierarchy) continue;

                Vector3 toTarget = collider.transform.position - _owner.position;
                float distance = toTarget.magnitude;

                // 距離が0の場合はスキップ
                if (distance < 0.1f) continue;

                // カメラ方向チェック
                Vector3 toTargetNormalized = toTarget.normalized;
                toTargetNormalized.y = 0f;
                toTargetNormalized.Normalize();

                float cameraAlignment = Vector3.Dot(cameraForward, toTargetNormalized);

                // 優先度に応じて分類
                if (cameraAlignment >= FrontNarrowAngle)
                {
                    // 前方90度以内
                    if (distance < nearestDistNarrow)
                    {
                        nearestDistNarrow = distance;
                        bestNarrow = collider.transform;
                    }
                }
                else if (cameraAlignment >= FrontWideAngle)
                {
                    // 前方180度以内（90度〜180度）
                    if (distance < nearestDistWide)
                    {
                        nearestDistWide = distance;
                        bestWide = collider.transform;
                    }
                }
                else
                {
                    // 後方
                    if (distance < nearestDistBack)
                    {
                        nearestDistBack = distance;
                        bestBack = collider.transform;
                    }
                }
            }

            // 優先度順に返す
            if (bestNarrow != null) return bestNarrow;
            if (bestWide != null) return bestWide;
            return bestBack;
        }

        public void Dispose()
        {
            ClearTarget();
            _currentTarget.Dispose();

            if (_overlayCanvas != null)
            {
                UnityEngine.Object.Destroy(_overlayCanvas.gameObject);
                _overlayCanvas = null;
            }
        }
    }
}