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

        // 遮蔽レイを対象表面の手前で止め、対象自身の collider への自己ヒットを避けるための余白
        private const float OcclusionMargin = 0.05f;

        private static readonly Vector2 _viewportCenter = new(0.5f, 0.5f);

        // 物理クエリ・候補集計用の再利用バッファ（毎スキャンで Clear し GC を避ける）
        private readonly Collider[] _hitBuffer = new Collider[16];
        private readonly HashSet<IInteractable> _seen = new();
        private readonly List<IInteractable> _visible = new();
        private readonly List<(IInteractable target, Vector2 viewport)> _actionableCandidates = new();

        // 提示状態の差分追跡（前回 / 今回）。Scan 末尾で swap して再利用する
        private Dictionary<IInteractable, InteractionState> _previousStates = new();
        private Dictionary<IInteractable, InteractionState> _currentStates = new();

        private IInteractable _actionable;
        private float _nextScanTime;

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
            _visible.Clear();
            _actionableCandidates.Clear();
            _seen.Clear();
            _currentStates.Clear();
            _actionable = null;

            if (_camera != null)
            {
                CollectVisible();
                _actionable = SelectActionable(_viewportCenter, _actionableCandidates);

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

        // 範囲内の候補から「カメラ視界内かつ非遮蔽」のものを _visible に、
        // さらにインタラクト距離内のものを _actionableCandidates に集める。
        private void CollectVisible()
        {
            var playerPos = transform.position;
            var camPos = _camera.transform.position;
            float interactSqr = _interactRadius * _interactRadius;

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
                if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;

                // 遮蔽（壁越し）を除外：カメラ→中心の間に遮蔽物があれば不可視
                var toCenter = center - camPos;
                float dist = toCenter.magnitude;
                if (dist > OcclusionMargin &&
                    Physics.Raycast(camPos, toCenter, dist - OcclusionMargin, _occluderMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                _visible.Add(interactable);

                if ((center - playerPos).sqrMagnitude <= interactSqr)
                {
                    _actionableCandidates.Add((interactable, new Vector2(viewport.x, viewport.y)));
                }
            }
        }

        /// <summary>
        /// インタラクト距離内の候補から、画面中心 <paramref name="screenCenter"/> に最も近い 1 つを選ぶ純粋関数。
        /// 候補が空なら null。同点は先頭を返す（厳密な &lt; で更新）。視界・遮蔽・距離の絞り込みは呼び出し側の責務。
        /// </summary>
        public static IInteractable SelectActionable(Vector2 screenCenter, IReadOnlyList<(IInteractable target, Vector2 viewport)> candidates)
        {
            IInteractable nearest = null;
            float nearestSqr = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.target == null) continue;

                float sqr = (candidate.viewport - screenCenter).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = candidate.target;
                }
            }

            return nearest;
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
    }
}
