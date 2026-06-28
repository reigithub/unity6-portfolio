using System.Collections.Generic;
using Game.Shared.Enums;
using UnityEngine;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// プレイヤー周囲のインタラクト対象を検出し、各対象の提示状態を駆動する検出器。
    /// 検出範囲(<see cref="_discoverRadius"/>)内・カメラ視界内・非遮蔽の対象を「発見可能(Discoverable)」とし、
    /// そのうちインタラクト距離(<see cref="_interactRadius"/>)内で画面中心（レティクル）に最も近い 1 つだけを「実行可能(Actionable)」とする。
    /// 距離判定はプレイヤー位置、視界・遮蔽・狙いはカメラ基準（一人称で視点が頭前方にあるため）。
    /// 対象は点でなく <see cref="IInteractable.WorldBounds"/>(AABB) で扱い、狙いは画面中心 ray への交差/角度で測る。
    /// 物理 SphereCast を使わないため、対象へ密着しても（cast 開始位置のめり込みで）検出が落ちることがない。
    /// </summary>
    public class InteractionDetector : MonoBehaviour
    {
        [Tooltip("視界・遮蔽判定とビルボード視点の基準カメラ")]
        [SerializeField] private Camera _camera;

        [Tooltip("発見可能とみなす最大距離（m, プレイヤー基準）")]
        [SerializeField] private float _discoverRadius = 3f;

        [Tooltip("インタラクト可能とみなす最大距離（m, プレイヤー基準・対象表面まで）。_discoverRadius 以下にする")]
        [SerializeField] private float _interactRadius = 1.5f;

        [Tooltip("検出スキャンの間隔（秒）。毎フレームではなく間引く")]
        [SerializeField] private float _scanInterval = 0.1f;

        [Tooltip("候補収集の対象レイヤー（Interactable）")]
        [SerializeField] private LayerMask _interactableMask = ~0;

        [Tooltip("遮蔽判定の対象レイヤー（壁・床・構造物）。対象自身のレイヤー(Interactable)は含めないこと")]
        [SerializeField] private LayerMask _occluderMask = ~0;

        [Tooltip("実行可能とみなすエイムアシスト半角（度）。画面中心からこの角度以内の対象のみ Actionable 候補。レティクル直撃は 0 度")]
        [SerializeField] private float _aimConeAngle = 12f;

        [Tooltip("現在の Actionable を維持しやすくするヒステリシス角度（度）。僅差での対象切替・点滅を抑える")]
        [SerializeField] private float _actionableStickiness = 5f;

        // 遮蔽レイを対象表面の手前で止め、対象自身の collider への自己ヒットを避けるための余白
        private const float OcclusionMargin = 0.05f;

        // 物理クエリ・候補集計用の再利用バッファ（毎スキャンで Clear し GC を避ける）
        private readonly Collider[] _hitBuffer = new Collider[16];
        private readonly HashSet<IInteractable> _seen = new();
        private readonly List<IInteractable> _visible = new();
        private readonly Plane[] _frustumPlanes = new Plane[6];

        // 提示状態の差分追跡（前回 / 今回）。Scan 末尾で swap して再利用する
        private Dictionary<IInteractable, InteractionState> _previousStates = new();
        private Dictionary<IInteractable, InteractionState> _currentStates = new();

        private IInteractable _actionable;
        private float _nextScanTime;

#if UNITY_EDITOR
        // ---- デバッグ Gizmo トグル ----
        [Header("Debug Gizmos")]
        [SerializeField] private bool _drawGizmos = false;
        [SerializeField] private bool _drawDiscoverRadius = true;
        [SerializeField] private bool _drawInteractRadius = true;
        [SerializeField] private bool _drawReticleRay = true;
        [SerializeField] private bool _drawOcclusionRays = true;
        [SerializeField] private bool _drawCameraFrustum = true;
        [SerializeField] private bool _drawCandidates = true;

        // ---- Gizmo 色定数 ----
        private static readonly Color GizmoColorDiscoverRadius = Color.cyan;
        private static readonly Color GizmoColorInteractRadius = Color.yellow;
        private static readonly Color GizmoColorReticleRay = Color.white;
        private static readonly Color GizmoColorOcclusionRayClear = Color.green;
        private static readonly Color GizmoColorOcclusionRayBlocked = Color.red;
        private static readonly Color GizmoColorFrustum = Color.gray;
        private static readonly Color GizmoColorCandidateDiscoverable = Color.cyan;
        private static readonly Color GizmoColorCandidateActionable = Color.green;
        private static readonly Color GizmoColorCandidateOutOfView = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color GizmoColorCandidateOccluded = Color.red;

        // ---- 候補分類 ----
        private enum GizmoCandidateKind { OutOfView, Occluded, Discoverable, Actionable }

        private readonly struct GizmoCandidate
        {
            public readonly IInteractable Target;
            public readonly Bounds Bounds;
            public readonly Vector3 AimPoint;
            public readonly float AimScore;
            public readonly GizmoCandidateKind Kind;

            public GizmoCandidate(IInteractable target, Bounds bounds, Vector3 aimPoint, float aimScore, GizmoCandidateKind kind)
            {
                Target = target;
                Bounds = bounds;
                AimPoint = aimPoint;
                AimScore = aimScore;
                Kind = kind;
            }
        }

        // ---- スナップショット（最後のスキャン結果を OnDrawGizmos から参照する） ----
        private Vector3 _gizmoCamPos;
        private Ray _gizmoReticleRay;
        private readonly List<GizmoCandidate> _gizmoCandidates = new();

        // 遮蔽レイのスナップショット（camPos → aimPoint、ヒット有無）
        private readonly struct GizmoOcclusionRay
        {
            public readonly Vector3 From;
            public readonly Vector3 To;
            public readonly bool Blocked;

            public GizmoOcclusionRay(Vector3 from, Vector3 to, bool blocked)
            {
                From = from;
                To = to;
                Blocked = blocked;
            }
        }

        private readonly List<GizmoOcclusionRay> _gizmoOcclusionRays = new();
#endif

        /// <summary>
        /// 現在の実行可能（Actionable）対象を取得する。存在しなければ false。Interact 入力の実行先。
        /// </summary>
        public bool TryGetTarget(out IInteractable target)
        {
            target = IsAlive(_actionable) ? _actionable : null;
            return target != null;
        }

        private void Update()
        {
            if (Time.time < _nextScanTime) return;
            _nextScanTime = Time.time + _scanInterval;
            Scan();
        }

        private void Scan()
        {
#if UNITY_EDITOR
            _gizmoCandidates.Clear();
            _gizmoOcclusionRays.Clear();
            if (_camera != null)
            {
                _gizmoCamPos = _camera.transform.position;
                _gizmoReticleRay = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            }
#endif

            _visible.Clear();
            _seen.Clear();
            _currentStates.Clear();

            // ヒステリシス用に直前の Actionable を退避してからクリアする
            var previousActionable = _actionable;
            _actionable = null;

            if (_camera != null)
            {
                EvaluateCandidates(previousActionable);

                for (int i = 0; i < _visible.Count; i++)
                {
                    var target = _visible[i];
                    _currentStates[target] = ReferenceEquals(target, _actionable)
                        ? InteractionState.Actionable
                        : InteractionState.Discoverable;
                }
            }

            ApplyStates();
        }

        /// <summary>
        /// 範囲内の候補を1本のパイプラインで評価する。
        /// 「カメラ視界内（bounds の一部でも frustum 内）かつ非遮蔽」を Discoverable として <see cref="_visible"/> に集め、
        /// そのうち「対象表面までの距離が <see cref="_interactRadius"/> 内 かつ 画面中心からの角度が <see cref="_aimConeAngle"/> 内」で
        /// 最も画面中心に近い 1 つを <see cref="_actionable"/> に選ぶ。Actionable ⊆ Discoverable が常に保たれる。
        /// </summary>
        private void EvaluateCandidates(IInteractable previousActionable)
        {
            var playerPos = transform.position;
            var camTransform = _camera.transform;
            var camPos = camTransform.position;
            var camForward = camTransform.forward;
            var centerRay = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);

            int hitCount = Physics.OverlapSphereNonAlloc(playerPos, _discoverRadius, _hitBuffer, _interactableMask, QueryTriggerInteraction.Collide);

            float bestScore = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _hitBuffer[i];
                if (hit == null || !hit.gameObject.activeInHierarchy) continue;

                var interactable = hit.GetComponentInParent<IInteractable>();
                if (interactable == null || !_seen.Add(interactable)) continue; // 複数コライダーの重複を排除

                var bounds = interactable.WorldBounds;

                // カメラ視界（frustum）内か：bounds の一部でも入っていれば可（中心が画面外でも脱落しない）
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                {
#if UNITY_EDITOR
                    _gizmoCandidates.Add(new GizmoCandidate(interactable, bounds, bounds.center, float.NaN, GizmoCandidateKind.OutOfView));
#endif
                    continue;
                }

                // 狙いスコア（0=レティクル直撃、度）と、遮蔽判定に使う aimPoint を同時に得る
                float aimScore = CalculateAimScore(bounds, centerRay, camPos, camForward, out var aimPoint);

                // 遮蔽（壁越し）を除外：カメラ → aimPoint の間に遮蔽物があれば不可視
                var toAim = aimPoint - camPos;
                float aimDist = toAim.magnitude;
                if (aimDist > OcclusionMargin &&
                    Physics.Raycast(camPos, toAim, aimDist - OcclusionMargin, _occluderMask, QueryTriggerInteraction.Ignore))
                {
#if UNITY_EDITOR
                    _gizmoCandidates.Add(new GizmoCandidate(interactable, bounds, aimPoint, aimScore, GizmoCandidateKind.Occluded));
                    _gizmoOcclusionRays.Add(new GizmoOcclusionRay(camPos, aimPoint, blocked: true));
#endif
                    continue;
                }

                // ここまで Discoverable
                _visible.Add(interactable);

#if UNITY_EDITOR
                _gizmoCandidates.Add(new GizmoCandidate(interactable, bounds, aimPoint, aimScore, GizmoCandidateKind.Discoverable));
                if (aimDist > OcclusionMargin)
                {
                    _gizmoOcclusionRays.Add(new GizmoOcclusionRay(camPos, aimPoint, blocked: false));
                }
#endif

                // Actionable 候補：対象表面までの距離がインタラクト距離内、かつエイムコーン内
                float surfaceDist = (playerPos - bounds.ClosestPoint(playerPos)).magnitude;
                if (surfaceDist <= _interactRadius && aimScore <= _aimConeAngle)
                {
                    // 現 Actionable はヒステリシス分だけ優遇し、僅差での乗り換え・点滅を防ぐ
                    float effectiveScore = ReferenceEquals(interactable, previousActionable)
                        ? aimScore - _actionableStickiness
                        : aimScore;

                    if (effectiveScore < bestScore)
                    {
                        bestScore = effectiveScore;
                        _actionable = interactable;
                    }
                }
            }

#if UNITY_EDITOR
            // _actionable 確定後、該当候補の分類を Actionable へ差し替える
            for (int i = 0; i < _gizmoCandidates.Count; i++)
            {
                if (ReferenceEquals(_gizmoCandidates[i].Target, _actionable))
                {
                    var c = _gizmoCandidates[i];
                    _gizmoCandidates[i] = new GizmoCandidate(c.Target, c.Bounds, c.AimPoint, c.AimScore, GizmoCandidateKind.Actionable);
                    break;
                }
            }
#endif
        }

        /// <summary>
        /// 画面中心（レティクル）からの「狙いの良さ」を角度で返す。0 が最良（レティクル直撃）で、
        /// 値が大きいほど画面中心から外れる。レティクル ray が bounds を貫けば 0、外れたら
        /// カメラ前方と bounds 中心方向のなす角（度）。画面投影を使わないため、対象がカメラ平面より
        /// 後ろ（深度 z&lt;0）へ回り込む近距離でも反転・破綻しない。
        /// <paramref name="aimPoint"/> は遮蔽判定に使う狙い点（交差時は交差点、非交差時は bounds 上の最近接点）。
        /// </summary>
        public static float CalculateAimScore(Bounds bounds, Ray centerRay, Vector3 cameraPosition, Vector3 cameraForward, out Vector3 aimPoint)
        {
            if (bounds.IntersectRay(centerRay, out float distance))
            {
                aimPoint = centerRay.GetPoint(distance);
                return 0f;
            }

            aimPoint = bounds.ClosestPoint(cameraPosition);
            return Vector3.Angle(cameraForward, bounds.center - cameraPosition);
        }

        // 前回との差分のみ通知する。今回不在の対象は Hidden に戻し、状態変化のみ反映する。
        private void ApplyStates()
        {
            foreach (var (interactable, _) in _previousStates)
            {
                if (!_currentStates.ContainsKey(interactable) && IsAlive(interactable))
                {
                    interactable.SetInteractionState(InteractionState.Hidden, _camera);
                }
            }

            foreach (var (interactable, state) in _currentStates)
            {
                if (!_previousStates.TryGetValue(interactable, out var previous) || previous != state)
                {
                    interactable.SetInteractionState(state, _camera);
                }
            }

            (_previousStates, _currentStates) = (_currentStates, _previousStates);
        }

        // 無効化時、提示中の対象を Hidden に戻して取り残しを防ぐ
        private void OnDisable()
        {
            foreach (var (interactable, _) in _previousStates)
            {
                if (IsAlive(interactable)) interactable.SetInteractionState(InteractionState.Hidden, _camera);
            }

            _previousStates.Clear();
            _actionable = null;
            _nextScanTime = 0f;
        }

        // IInteractable 実装が破棄済み Unity オブジェクトでないかを安全に判定する
        private static bool IsAlive(IInteractable interactable)
        {
            if (interactable is Object unityObject) return unityObject != null;
            return interactable != null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_drawGizmos) return;

            // 範囲スフィアは Play 外でも transform.position から描画できる
            if (_drawDiscoverRadius)
            {
                Gizmos.color = GizmoColorDiscoverRadius;
                Gizmos.DrawWireSphere(transform.position, _discoverRadius);
            }

            if (_drawInteractRadius)
            {
                Gizmos.color = GizmoColorInteractRadius;
                Gizmos.DrawWireSphere(transform.position, _interactRadius);
            }

            // カメラ・スナップショット依存の描画は Play 中かつ参照が有効なときのみ
            if (!Application.isPlaying || _camera == null) return;

            if (_drawCameraFrustum)
            {
                Gizmos.color = GizmoColorFrustum;
                Gizmos.matrix = _camera.transform.localToWorldMatrix;
                Gizmos.DrawFrustum(
                    Vector3.zero,
                    _camera.fieldOfView,
                    _camera.farClipPlane,
                    _camera.nearClipPlane,
                    _camera.aspect);
                Gizmos.matrix = Matrix4x4.identity;
            }

            if (_drawReticleRay)
            {
                Gizmos.color = GizmoColorReticleRay;
                Gizmos.DrawLine(_gizmoReticleRay.origin, _gizmoReticleRay.origin + _gizmoReticleRay.direction * _interactRadius);
            }

            if (_drawOcclusionRays)
            {
                foreach (var ray in _gizmoOcclusionRays)
                {
                    Gizmos.color = ray.Blocked ? GizmoColorOcclusionRayBlocked : GizmoColorOcclusionRayClear;
                    Gizmos.DrawLine(ray.From, ray.To);
                }
            }

            if (_drawCandidates)
            {
                foreach (var candidate in _gizmoCandidates)
                {
                    Gizmos.color = candidate.Kind switch
                    {
                        GizmoCandidateKind.Actionable => GizmoColorCandidateActionable,
                        GizmoCandidateKind.Discoverable => GizmoColorCandidateDiscoverable,
                        GizmoCandidateKind.Occluded => GizmoColorCandidateOccluded,
                        _ => GizmoColorCandidateOutOfView,
                    };
                    // 検出に使った AABB と、狙い点を可視化する
                    Gizmos.DrawWireCube(candidate.Bounds.center, candidate.Bounds.size);
                    Gizmos.DrawWireSphere(candidate.AimPoint, 0.08f);
                }
            }
        }
#endif
    }
}
