using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.Interaction
{
    /// <summary>
    /// プレイヤー周囲のインタラクト対象を検出し、各対象の提示状態を駆動する検出器。
    /// 検出範囲(<see cref="_discoverRadius"/>)内・カメラ視界内・非遮蔽の対象を「発見可能(Discoverable)」とし、
    /// そのうちインタラクト距離(<see cref="_interactRadius"/>)内で画面中心に最も近い 1 つだけを「実行可能(Actionable)」とする。
    /// 距離判定はプレイヤー位置、視界・遮蔽判定はカメラを基準にする（一人称で視点が頭前方にあるため）。
    /// </summary>
    public class InteractionDetector : MonoBehaviour
    {
        [Tooltip("視界・遮蔽判定とビルボード視点の基準カメラ")]
        [SerializeField] private Camera _camera;

        [Tooltip("発見可能とみなす最大距離（m, プレイヤー基準）")]
        [SerializeField] private float _discoverRadius = 6f;

        [Tooltip("インタラクト可能とみなす最大距離（m, プレイヤー基準）。_discoverRadius 以下にする")]
        [SerializeField] private float _interactRadius = 3f;

        [Tooltip("検出スキャンの間隔（秒）。毎フレームではなく間引く")]
        [SerializeField] private float _scanInterval = 0.1f;

        [Tooltip("候補収集の対象レイヤー（Interactable）")]
        [SerializeField] private LayerMask _interactableMask = ~0;

        [Tooltip("遮蔽判定の対象レイヤー（壁・床・構造物）。対象自身のレイヤー(Interactable)は含めないこと")]
        [SerializeField] private LayerMask _occluderMask = ~0;

        [Tooltip("レティクルからのエイムアシスト用 SphereCast 半径(m)。小さいほど厳密、大きいほど掴みやすい")]
        [SerializeField] private float _aimAssistRadius = 0.15f;

        // 遮蔽レイを対象表面の手前で止め、対象自身の collider への自己ヒットを避けるための余白
        private const float OcclusionMargin = 0.05f;

        // レティクル SphereCast の原点をカメラ手前へ後退させる量（対象へのめり込みによる検出漏れ対策）
        private const float AimCastBackstep = 0.2f;

        // 物理クエリ・候補集計用の再利用バッファ（毎スキャンで Clear し GC を避ける）
        private readonly Collider[] _hitBuffer = new Collider[16];
        private readonly HashSet<IInteractable> _seen = new();
        private readonly List<IInteractable> _visible = new();

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
        [SerializeField] private bool _drawAimCast = true;
        [SerializeField] private bool _drawOcclusionRays = true;
        [SerializeField] private bool _drawCameraFrustum = true;
        [SerializeField] private bool _drawCandidates = true;

        // ---- Gizmo 色定数 ----
        private static readonly Color GizmoColorDiscoverRadius = Color.cyan;
        private static readonly Color GizmoColorInteractRadius = Color.yellow;
        private static readonly Color GizmoColorAimCastMiss = Color.white;
        private static readonly Color GizmoColorAimCastHit = Color.green;
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
            public readonly Vector3 Center;
            public readonly GizmoCandidateKind Kind;

            public GizmoCandidate(Vector3 center, GizmoCandidateKind kind)
            {
                Center = center;
                Kind = kind;
            }
        }

        // ---- スナップショット（最後のスキャン結果を OnDrawGizmos から参照する） ----
        private Vector3 _gizmoCamPos;
        private Vector3 _gizmoAimOrigin;
        private Vector3 _gizmoAimDir;
        private float _gizmoAimMaxDist;
        private bool _gizmoAimHasHit;
        private Vector3 _gizmoAimHitPoint;
        private readonly List<GizmoCandidate> _gizmoCandidates = new();

        // 遮蔽レイのスナップショット（origin → center、ヒット有無）
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
        public bool TryGetActionable(out IInteractable target)
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
            }
#endif

            _visible.Clear();
            _seen.Clear();
            _currentStates.Clear();
            _actionable = null;

            if (_camera != null)
            {
                CollectVisible();
                _actionable = SelectActionableByAimCast();

                for (int i = 0; i < _visible.Count; i++)
                {
                    var target = _visible[i];
                    _currentStates[target] = ReferenceEquals(target, _actionable)
                        ? InteractionState.Actionable
                        : InteractionState.Discoverable;

#if UNITY_EDITOR
                    // 最終分類を候補リストへ反映する（CollectVisible で Discoverable として仮記録済みの要素を上書き）
                    var finalKind = ReferenceEquals(target, _actionable)
                        ? GizmoCandidateKind.Actionable
                        : GizmoCandidateKind.Discoverable;
                    var center = target.CenterPosition;
                    // 仮記録（Discoverable）を最終 kind に差し替える
                    bool replaced = false;
                    for (int j = 0; j < _gizmoCandidates.Count; j++)
                    {
                        if (_gizmoCandidates[j].Center == center && _gizmoCandidates[j].Kind == GizmoCandidateKind.Discoverable)
                        {
                            _gizmoCandidates[j] = new GizmoCandidate(center, finalKind);
                            replaced = true;
                            break;
                        }
                    }

                    // _visible への追加後に Gizmo スナップショットが未登録の場合は追加する
                    if (!replaced)
                    {
                        _gizmoCandidates.Add(new GizmoCandidate(center, finalKind));
                    }
#endif
                }
            }

            ApplyStates();
        }

        // 範囲内の候補から「カメラ視界内かつ非遮蔽」のものを _visible に集める（Discoverable 候補）。
        private void CollectVisible()
        {
            var playerPos = transform.position;
            var camPos = _camera.transform.position;

            int hitCount = Physics.OverlapSphereNonAlloc(
                playerPos, _discoverRadius, _hitBuffer, _interactableMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _hitBuffer[i];
                if (hit == null || !hit.gameObject.activeInHierarchy) continue;

                var interactable = hit.GetComponentInParent<IInteractable>();
                if (interactable == null || !_seen.Add(interactable)) continue; // 複数コライダーの重複を排除

                var center = interactable.CenterPosition;

                // カメラ視界（frustum）内か
                var viewport = _camera.WorldToViewportPoint(center);
                if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                {
#if UNITY_EDITOR
                    _gizmoCandidates.Add(new GizmoCandidate(center, GizmoCandidateKind.OutOfView));
#endif
                    continue;
                }

                // 遮蔽（壁越し）を除外：カメラ→中心の間に遮蔽物があれば不可視
                var toCenter = center - camPos;
                float dist = toCenter.magnitude;
                if (dist > OcclusionMargin &&
                    Physics.Raycast(camPos, toCenter, dist - OcclusionMargin, _occluderMask, QueryTriggerInteraction.Ignore))
                {
#if UNITY_EDITOR
                    _gizmoCandidates.Add(new GizmoCandidate(center, GizmoCandidateKind.Occluded));
                    _gizmoOcclusionRays.Add(new GizmoOcclusionRay(camPos, center, blocked: true));
#endif
                    continue;
                }

#if UNITY_EDITOR
                // 非遮蔽レイもスナップショットに記録する
                if (dist > OcclusionMargin)
                {
                    _gizmoOcclusionRays.Add(new GizmoOcclusionRay(camPos, center, blocked: false));
                }

                // _visible.Add より前に Discoverable として仮記録する（Scan の状態決定ループで最終 kind へ差し替える）
                _gizmoCandidates.Add(new GizmoCandidate(center, GizmoCandidateKind.Discoverable));
#endif

                _visible.Add(interactable);
            }
        }

        /// <summary>
        /// レティクル（画面中心）から SphereCast を撃ち、ヒットした単一対象を Actionable として返す。
        /// エイムアシスト半径 <see cref="_aimAssistRadius"/> ぶんの許容を持たせ、レティクルが対象コライダーに
        /// 重なっているときのみ成立する。原点はカメラへのめり込み対策で <see cref="AimCastBackstep"/> 後退させる。
        /// マスクは Interactable のみ。遮蔽は <see cref="_visible"/>（中心点への細いレイ遮蔽を通った集合）への
        /// 包含チェックで担保し、Actionable ⊆ Discoverable を保証する。
        /// </summary>
        private IInteractable SelectActionableByAimCast()
        {
            var ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var origin = ray.origin - ray.direction * AimCastBackstep;
            float maxDist = _interactRadius + AimCastBackstep;

            bool hasHit = Physics.SphereCast(origin, _aimAssistRadius, ray.direction, out var hit, maxDist, _interactableMask, QueryTriggerInteraction.Collide);

#if UNITY_EDITOR
            _gizmoAimOrigin = origin;
            _gizmoAimDir = ray.direction;
            _gizmoAimMaxDist = maxDist;
            _gizmoAimHasHit = hasHit;
            _gizmoAimHitPoint = hasHit ? hit.point : Vector3.zero;
#endif

            if (!hasHit)
            {
                return null;
            }

            var target = hit.collider.GetComponentInParent<IInteractable>();
            if (target == null || !_visible.Contains(target)) return null;

            return target;
        }

        // 前回との差分のみ通知する。今回不在の対象は Hidden に戻し、状態変化のみ反映する。
        private void ApplyStates()
        {
            foreach (var pair in _previousStates)
            {
                if (!_currentStates.ContainsKey(pair.Key) && IsAlive(pair.Key))
                {
                    pair.Key.SetInteractionState(InteractionState.Hidden, _camera);
                }
            }

            foreach (var pair in _currentStates)
            {
                if (!_previousStates.TryGetValue(pair.Key, out var previous) || previous != pair.Value)
                {
                    pair.Key.SetInteractionState(pair.Value, _camera);
                }
            }

            (_previousStates, _currentStates) = (_currentStates, _previousStates);
        }

        // 無効化時、提示中の対象を Hidden に戻して取り残しを防ぐ
        private void OnDisable()
        {
            foreach (var pair in _previousStates)
            {
                if (IsAlive(pair.Key)) pair.Key.SetInteractionState(InteractionState.Hidden, _camera);
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

            if (_drawOcclusionRays)
            {
                foreach (var ray in _gizmoOcclusionRays)
                {
                    Gizmos.color = ray.Blocked ? GizmoColorOcclusionRayBlocked : GizmoColorOcclusionRayClear;
                    Gizmos.DrawLine(ray.From, ray.To);
                }
            }

            if (_drawAimCast)
            {
                Gizmos.color = _gizmoAimHasHit ? GizmoColorAimCastHit : GizmoColorAimCastMiss;
                DrawSphereCast(_gizmoAimOrigin, _gizmoAimDir, _aimAssistRadius, _gizmoAimMaxDist, _gizmoAimHasHit, _gizmoAimHitPoint);
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
                    Gizmos.DrawWireSphere(candidate.Center, 0.15f);
                }
            }
        }

        /// <summary>
        /// SphereCast の軌道を Gizmo で可視化する。始点・終点に球、軸線で軌跡を表現する。
        /// </summary>
        /// <param name="origin">キャスト始点（AimCastBackstep 後退済みの原点）</param>
        /// <param name="dir">キャスト方向</param>
        /// <param name="radius">球の半径</param>
        /// <param name="maxDist">最大距離</param>
        /// <param name="hasHit">ヒットした場合 true</param>
        /// <param name="hitPoint">ヒット点（hasHit が false の場合は未使用）</param>
        private static void DrawSphereCast(Vector3 origin, Vector3 dir, float radius, float maxDist, bool hasHit, Vector3 hitPoint)
        {
            var endpoint = hasHit ? hitPoint : origin + dir * maxDist;
            Gizmos.DrawWireSphere(origin, radius);
            Gizmos.DrawWireSphere(endpoint, radius);
            Gizmos.DrawLine(origin, endpoint);
        }
#endif
    }
}
