using System.Collections.Generic;
using Game.Shared.Enums;
using UnityEngine;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// プレイヤー周囲のインタラクト対象を検出し、各対象の提示状態を駆動する検出器。
    /// 検出範囲(<see cref="_discoverRadius"/>)内・カメラ視界内・非遮蔽の対象を「発見可能(Discoverable)」とし、
    /// そのうちインタラクト距離(<see cref="_interactRadius"/>)内で画面中心（レティクル）に最も近い 1 つだけを「実行可能(Actionable)」とする。
    /// エイムコーン内に候補が無い場合のみ、視界外インタラクト許可対象
    /// （<see cref="IInteractable.AllowOutOfView"/>、拾得系のみ）のうち
    /// プレイヤー前方半面（水平 180 度）かつインタラクト距離内の非遮蔽対象から
    /// 表面距離最小の 1 つをフォールバックで Actionable にする
    /// （足元の近接アイテムを画面端クランプのプロンプトで取得可能にするため。背後・据え置き装置は対象外）。
    /// 距離判定はプレイヤー位置、視界・遮蔽・狙いはカメラ基準（一人称で視点が頭前方にあるため）。
    /// 遮蔽物は構造物・地形に加え他の Interactable も含み、対象自身のコライダーのみ参照一致で除外する。
    /// 対象は点でなく <see cref="IInteractable.WorldBounds"/>(AABB) で扱い、狙いは画面中心 ray への交差/角度で測る。
    /// 物理 SphereCast を使わないため、対象へ密着しても（cast 開始位置のめり込みで）検出が落ちることがない。
    /// </summary>
    public class InteractionDetector : MonoBehaviour
    {
        [Tooltip("視界・遮蔽判定とスクリーン座標変換の基準カメラ")]
        [SerializeField] private Camera _camera;

        [Tooltip("発見可能とみなす最大距離（m, プレイヤー基準）")]
        [SerializeField] private float _discoverRadius = 3f;

        [Tooltip("インタラクト可能とみなす最大距離（m, プレイヤー基準・対象表面まで）。_discoverRadius 以下にする")]
        [SerializeField] private float _interactRadius = 1.5f;

        [Tooltip("検出スキャンの間隔（秒）。毎フレームではなく間引く")]
        [SerializeField] private float _scanInterval = 0.1f;

        [Tooltip("候補収集の対象レイヤー（Interactable）")]
        [SerializeField] private LayerMask _interactableMask = ~0;

        [Tooltip("遮蔽判定の対象レイヤー（壁・床・構造物）。Interactable レイヤーは実行時に常時合成され、対象自身のコライダーは参照一致で除外される")]
        [SerializeField] private LayerMask _occluderMask = ~0;

        [Tooltip("実行可能とみなすエイムアシスト半角（度）。画面中心からこの角度以内の対象のみ Actionable 候補。レティクル直撃は 0 度")]
        [SerializeField] private float _aimConeAngle = 12f;

        [Tooltip("現在の Actionable を維持しやすくするヒステリシス角度（度）。僅差での対象切替・点滅を抑える")]
        [SerializeField] private float _actionableStickiness = 5f;

        [Tooltip("視界外フォールバックで現在の Actionable を維持しやすくするヒステリシス距離（m）。僅差での対象切替・点滅を抑える")]
        [SerializeField] private float _fallbackStickiness = 0.3f;

        [Tooltip("視界外フォールバックの前方判定で現在の Actionable を維持しやすくするヒステリシス角度（度）。真横境界での表示点滅を抑える")]
        [SerializeField] private float _fallbackDirectionStickiness = 10f;

        // 遮蔽レイを対象表面の手前で止める余白。自己ヒット除外の正本は参照一致（IsOccluded）で、
        // これは AABB 境界ちょうどの際どいヒットを避ける補助
        private const float OcclusionMargin = 0.05f;

        // 物理クエリ・候補集計用の再利用バッファ（毎スキャンで Clear し GC を避ける）
        private readonly Collider[] _hitBuffer = new Collider[16];
        private readonly RaycastHit[] _occlusionHitBuffer = new RaycastHit[16];
        private readonly HashSet<IInteractable> _seen = new();
        private readonly List<IInteractable> _visible = new();
        private readonly Plane[] _frustumPlanes = new Plane[6];

        // 視界外フォールバックの候補（視錐台外だがインタラクト距離内）。視界内 Actionable 不在時のみ選定に使う
        private readonly List<FallbackCandidate> _fallbackCandidates = new();

        // 視錐台判定で脱落した対象のうち、近接距離内のものを選定まで保持する
        private readonly struct FallbackCandidate
        {
            public readonly IInteractable Target;
            public readonly Bounds Bounds;
            public readonly float SurfaceDistance;

            public FallbackCandidate(IInteractable target, Bounds bounds, float surfaceDistance)
            {
                Target = target;
                Bounds = bounds;
                SurfaceDistance = surfaceDistance;
            }
        }

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
        private static readonly Color GizmoColorCandidateFallbackActionable = new Color(1f, 0.5f, 0f);
        private static readonly Color GizmoColorCandidateOutOfView = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color GizmoColorCandidateOccluded = Color.red;

        // ---- 候補分類 ----
        private enum GizmoCandidateKind { OutOfView, Occluded, Discoverable, Actionable, FallbackActionable }

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
            _fallbackCandidates.Clear();

            // ヒステリシス用に直前の Actionable を退避してからクリアする
            var previousActionable = _actionable;
            _actionable = null;

            if (_camera != null)
            {
                EvaluateCandidates(previousActionable);

                for (int i = 0; i < _visible.Count; i++)
                {
                    _currentStates[_visible[i]] = InteractionState.Discoverable;
                }

                // 視界内選定なら Discoverable からの上書き、視界外フォールバックなら追加となる
                //（フォールバック対象は _visible に入らないため Discoverable にはならない）
                if (_actionable != null)
                {
                    _currentStates[_actionable] = InteractionState.Actionable;
                }
            }

            ApplyStates();
        }

        /// <summary>
        /// 範囲内の候補を1本のパイプラインで評価する。
        /// 「カメラ視界内（bounds の一部でも frustum 内）かつ非遮蔽」を Discoverable として <see cref="_visible"/> に集め、
        /// そのうち「対象表面までの距離が <see cref="_interactRadius"/> 内 かつ 画面中心からの角度が <see cref="_aimConeAngle"/> 内」で
        /// 最も画面中心に近い 1 つを <see cref="_actionable"/> に選ぶ。
        /// エイムコーン内候補が無ければ、視錐台外の視界外インタラクト許可対象
        /// （<see cref="IInteractable.AllowOutOfView"/>）のうちプレイヤー前方半面
        /// （水平 180 度、<see cref="IsInForwardHemisphere"/>）かつインタラクト距離内の候補
        /// （<see cref="_fallbackCandidates"/>）から表面距離最小の非遮蔽対象をフォールバック選定する。Actionable は視界内選定なら Discoverable 集合の元、
        /// フォールバック選定なら視界外で Discoverable 集合と交わらない（いずれも常に高々 1 つ）。
        /// </summary>
        private void EvaluateCandidates(IInteractable previousActionable)
        {
            var playerPos = transform.position;
            var playerForward = transform.forward; // プレイヤー本体は yaw のみ回転するため常に水平の体の向き
            var camTransform = _camera.transform;
            var camPos = camTransform.position;
            var camForward = camTransform.forward;
            var centerRay = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            // 遮蔽マスク: シリアライズ値は変えず Interactable を常時合成し、対象同士の遮蔽も成立させる
            int occlusionMask = _occluderMask | _interactableMask;

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
                    // 視界外でも「視界外インタラクト許可対象（拾得系） かつ インタラクト距離内 かつ プレイヤー前方半面（水平 180 度）」
                    // なら Actionable フォールバック候補として保持する（採用時の表示は View 側の画面端クランプが担う）。
                    // 現 Actionable には前方判定に角度マージンを与え、真横境界での成立/不成立の点滅を抑える
                    if (interactable.AllowOutOfView)
                    {
                        float fallbackDistance = CalculateSurfaceDistance(bounds, playerPos);
                        float directionTolerance = ReferenceEquals(interactable, previousActionable)
                            ? _fallbackDirectionStickiness
                            : 0f;
                        if (fallbackDistance <= _interactRadius
                            && IsInForwardHemisphere(playerPos, playerForward, bounds.center, directionTolerance))
                        {
                            _fallbackCandidates.Add(new FallbackCandidate(interactable, bounds, fallbackDistance));
                        }
                    }
#if UNITY_EDITOR
                    _gizmoCandidates.Add(new GizmoCandidate(interactable, bounds, bounds.center, float.NaN, GizmoCandidateKind.OutOfView));
#endif
                    continue;
                }

                // 狙いスコア（0=レティクル直撃、度）と、遮蔽判定に使う aimPoint を同時に得る
                float aimScore = CalculateAimScore(bounds, centerRay, camPos, camForward, out var aimPoint);

                // 遮蔽（壁越し）を除外：カメラ → aimPoint の間に対象自身以外の遮蔽物（構造物・他の Interactable）があれば不可視
                if (IsOccluded(camPos, aimPoint, interactable, occlusionMask, _occlusionHitBuffer))
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
                if ((aimPoint - camPos).magnitude > OcclusionMargin)
                {
                    _gizmoOcclusionRays.Add(new GizmoOcclusionRay(camPos, aimPoint, blocked: false));
                }
#endif

                // Actionable 候補：対象表面までの距離がインタラクト距離内、かつエイムコーン内
                float surfaceDist = CalculateSurfaceDistance(bounds, playerPos);
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

            // エイムコーン内の視界内候補が不在のときのみ、視界外の近接対象をフォールバック選定する
            if (_actionable == null && _fallbackCandidates.Count > 0)
            {
                SelectFallbackActionable(previousActionable, camPos, occlusionMask);
            }

#if UNITY_EDITOR
            // _actionable 確定後、該当候補の分類を差し替える（視界内は Actionable、視界外は FallbackActionable）
            for (int i = 0; i < _gizmoCandidates.Count; i++)
            {
                if (!ReferenceEquals(_gizmoCandidates[i].Target, _actionable)) continue;

                var c = _gizmoCandidates[i];
                bool fromOutOfView = c.Kind == GizmoCandidateKind.OutOfView;
                var kind = fromOutOfView ? GizmoCandidateKind.FallbackActionable : GizmoCandidateKind.Actionable;
                var aimPoint = fromOutOfView ? c.Bounds.ClosestPoint(_gizmoCamPos) : c.AimPoint;
                _gizmoCandidates[i] = new GizmoCandidate(c.Target, c.Bounds, aimPoint, c.AimScore, kind);
                break;
            }
#endif
        }

        /// <summary>
        /// 視界外フォールバックの Actionable を選定する。選定基準は表面距離最小
        /// （視界外対象への角度はカメラ回転で毎スキャン揺れるため、プレイヤー移動でしか変わらない距離が安定する）。
        /// 現対象には距離ヒステリシス（<see cref="_fallbackStickiness"/>）を適用して僅差での乗り換え・点滅を防ぎ、
        /// 遮蔽判定（壁越し取得の防止。視界内経路と同条件）は現ベストを更新しうる候補にのみ遅延実行する
        /// （遮蔽された近距離候補があっても次点の非遮蔽候補へ正しく落ちる）。
        /// </summary>
        private void SelectFallbackActionable(IInteractable previousActionable, Vector3 cameraPosition, int occlusionMask)
        {
            float bestDistance = float.MaxValue;

            for (int i = 0; i < _fallbackCandidates.Count; i++)
            {
                var candidate = _fallbackCandidates[i];
                float effectiveDistance = ReferenceEquals(candidate.Target, previousActionable)
                    ? candidate.SurfaceDistance - _fallbackStickiness
                    : candidate.SurfaceDistance;

                if (effectiveDistance >= bestDistance) continue; // 勝てない候補には遮蔽レイを撃たない

                // 視界外はレティクル ray と交差しないため、狙い点はカメラへの最近接点を直接使う
                var aimPoint = candidate.Bounds.ClosestPoint(cameraPosition);
                bool occluded = IsOccluded(cameraPosition, aimPoint, candidate.Target, occlusionMask, _occlusionHitBuffer);

#if UNITY_EDITOR
                if ((aimPoint - cameraPosition).magnitude > OcclusionMargin)
                {
                    _gizmoOcclusionRays.Add(new GizmoOcclusionRay(cameraPosition, aimPoint, occluded));
                }
#endif

                if (occluded) continue;

                bestDistance = effectiveDistance;
                _actionable = candidate.Target;
            }
        }

        /// <summary>
        /// プレイヤー位置から対象 AABB 表面までの距離（m）を返す純関数。プレイヤーが bounds 内部・表面上にいる場合は 0。
        /// 視界内経路の Actionable 距離判定と、視界外フォールバックの近接判定が同じ距離定義を共有する。
        /// </summary>
        internal static float CalculateSurfaceDistance(Bounds bounds, Vector3 playerPosition)
            => (playerPosition - bounds.ClosestPoint(playerPosition)).magnitude;

        /// <summary>
        /// 対象がプレイヤーの前方半面（水平 180 度以内）にあるかを返す純関数。
        /// 判定は水平面（XZ）で行う: 足元の対象は縦方向成分が支配的で、3D の内積では前方の足元でも
        /// 負になりうるため、y 成分を落として体の向きとの角度で判定する。
        /// <paramref name="toleranceDegrees"/> は境界を緩めるマージン（度）。0 で真横（90°）ちょうどまで含み、
        /// 正の値で 90°+マージンまで含む（現 Actionable の維持ヒステリシスに使う）。
        /// どちらかの水平成分がほぼゼロ（対象がほぼ真下、または forward が垂直）の場合は前方扱いとする
        /// （真下は前後の区別が無く、体の向きに依らず手が届くため）。
        /// </summary>
        internal static bool IsInForwardHemisphere(
            Vector3 playerPosition, Vector3 playerForward, Vector3 targetPoint, float toleranceDegrees)
        {
            var toTarget = targetPoint - playerPosition;
            toTarget.y = 0f;
            playerForward.y = 0f;

            if (toTarget.sqrMagnitude < 1e-6f || playerForward.sqrMagnitude < 1e-6f) return true;

            return Vector3.Angle(playerForward, toTarget) <= 90f + toleranceDegrees;
        }

        /// <summary>
        /// 画面中心（レティクル）からの「狙いの良さ」を角度で返す。0 が最良（レティクル直撃）で、
        /// 値が大きいほど画面中心から外れる。レティクル ray が bounds を貫けば 0、外れたら
        /// カメラ前方と bounds 中心方向のなす角（度）。画面投影を使わないため、対象がカメラ平面より
        /// 後ろ（深度 z&lt;0）へ回り込む近距離でも反転・破綻しない。
        /// <paramref name="aimPoint"/> は遮蔽判定に使う狙い点（交差時は交差点、非交差時は bounds 上の最近接点）。
        /// </summary>
        internal static float CalculateAimScore(Bounds bounds, Ray centerRay, Vector3 cameraPosition, Vector3 cameraForward, out Vector3 aimPoint)
        {
            if (bounds.IntersectRay(centerRay, out float distance))
            {
                aimPoint = centerRay.GetPoint(distance);
                return 0f;
            }

            aimPoint = bounds.ClosestPoint(cameraPosition);
            return Vector3.Angle(cameraForward, bounds.center - cameraPosition);
        }

        /// <summary>
        /// カメラ → 狙い点の間に、対象自身以外の遮蔽物（構造物・他の Interactable）があるかを返す。
        /// 自己ヒットはレイヤーでなく同一性（ヒットコライダー親階層の <see cref="IInteractable"/> と
        /// <paramref name="target"/> の参照一致）で除外する。対象のコライダーが対象ルート階層の外にあると
        /// 自己遮蔽になるが、候補収集も同じ親方向探索に依存しており新たな制約ではない。
        /// トリガーは無視するため、拾得用に膨らませたトリガーコライダーは遮蔽物にならない。
        /// </summary>
        /// <param name="cameraPosition">遮蔽レイの始点（カメラ位置）</param>
        /// <param name="aimPoint">遮蔽レイの終点（対象の狙い点）</param>
        /// <param name="target">自己ヒット除外の対象自身</param>
        /// <param name="occluderMask">遮蔽対象レイヤー（Interactable 合成済み）</param>
        /// <param name="hitBuffer">ヒット列挙の再利用バッファ（呼び出し側が所有）</param>
        internal static bool IsOccluded(Vector3 cameraPosition, Vector3 aimPoint, IInteractable target, int occluderMask, RaycastHit[] hitBuffer)
        {
            var toAim = aimPoint - cameraPosition;
            float aimDistance = toAim.magnitude;
            if (aimDistance <= OcclusionMargin) return false;

            int hitCount = Physics.RaycastNonAlloc(
                cameraPosition, toAim, hitBuffer, aimDistance - OcclusionMargin, occluderMask, QueryTriggerInteraction.Ignore);

            // バッファ満杯はヒットの切り捨て（真の遮蔽物の欠落）と区別できないため、安全側の遮蔽扱いにする
            if (hitCount == hitBuffer.Length) return true;

            for (int i = 0; i < hitCount; i++)
            {
                var hitCollider = hitBuffer[i].collider;
                if (hitCollider == null) continue;
                if (ReferenceEquals(hitCollider.GetComponentInParent<IInteractable>(), target)) continue;
                return true;
            }

            return false;
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
                        GizmoCandidateKind.FallbackActionable => GizmoColorCandidateFallbackActionable,
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
