using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.Interaction
{
    /// <summary>
    /// 自身（プレイヤー）の周囲一定距離内の <see cref="IInteractable"/> を検出し、
    /// 最も近い 1 つだけをハイライトする検出器。プレイヤーにアタッチして使う。
    /// 検出は <see cref="Physics.OverlapSphereNonAlloc"/>（デフォルト物理シーン）で行い、
    /// ネットワーク物理シーンには依存しない。
    /// </summary>
    public class InteractionDetector : MonoBehaviour
    {
        [Tooltip("検出半径（m）")]
        [SerializeField] private float _detectRadius = 2.5f;

        [Tooltip("検出対象のレイヤー")]
        [SerializeField] private LayerMask _interactableMask = ~0;

        [Tooltip("検出スキャンの間隔（秒）。毎フレームではなく間引く")]
        [SerializeField] private float _scanInterval = 0.1f;

        private readonly Collider[] _hitBuffer = new Collider[16];
        private readonly List<IInteractable> _candidates = new();
        private IInteractable _current;
        private float _nextScanTime;

        /// <summary>
        /// 現在の最近接ターゲットを取得する。存在しなければ false。
        /// </summary>
        public bool TryGetCurrent(out IInteractable target)
        {
            target = IsAlive(_current) ? _current : null;
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
            var origin = transform.position;
            int hitCount = Physics.OverlapSphereNonAlloc(
                origin, _detectRadius, _hitBuffer, _interactableMask, QueryTriggerInteraction.Collide);

            _candidates.Clear();
            for (int i = 0; i < hitCount; i++)
            {
                var hit = _hitBuffer[i];
                if (hit == null || !hit.gameObject.activeInHierarchy) continue;

                var interactable = hit.GetComponentInParent<IInteractable>();
                if (interactable != null && !_candidates.Contains(interactable))
                {
                    _candidates.Add(interactable);
                }
            }

            UpdateCurrent(SelectNearest(origin, _candidates));
        }

        // 最近接ターゲットが変わったときのみハイライトを差分更新する
        private void UpdateCurrent(IInteractable nearest)
        {
            if (ReferenceEquals(_current, nearest)) return;

            if (IsAlive(_current)) _current.SetHighlighted(false);
            _current = nearest;
            if (IsAlive(_current)) _current.SetHighlighted(true);
        }

        /// <summary>
        /// 候補から <paramref name="origin"/> に最も近い 1 つを返す。候補が空なら null。
        /// 距離の二乗で比較する純粋関数（テスト対象）。
        /// </summary>
        public static IInteractable SelectNearest(Vector3 origin, IReadOnlyList<IInteractable> candidates)
        {
            IInteractable nearest = null;
            float nearestSqr = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate == null) continue;

                float sqr = (candidate.CenterPosition - origin).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        // IInteractable 実装が破棄済み Unity オブジェクトでないかを安全に判定する
        private static bool IsAlive(IInteractable interactable)
        {
            if (interactable is Object unityObject) return unityObject != null;
            return interactable != null;
        }
    }
}
