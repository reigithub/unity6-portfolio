using System.Collections.Generic;
using UnityEngine;

namespace Game.Horror.WeaponEffect
{
    /// <summary>
    /// 登録されている武器効果（武器から発生し、切り離され、一定時間存在する効果ボリューム）の集合。
    /// <see cref="HorrorWeaponEffectSpawner"/> が所有し、書き手は各効果自身
    /// （<see cref="HorrorSmokeField"/> が生成で登録し、破棄で解除する）、読み手は敵知覚
    /// （HorrorEnemyPerception。<see cref="GetSightMultiplier"/> で実効視界距離を導出する）。
    /// 登録は効果オブジェクトの存在期間と一致する（生成から破棄まで）。
    /// </summary>
    public class HorrorWeaponEffectRegistry
    {
        private struct WeaponEffect
        {
            public int Id;
            public Vector3 Center;
            public float Radius;
            public float SightMultiplier;
        }

        private readonly List<WeaponEffect> _effects = new();

        /// <summary>登録中のエントリ数（テスト・デバッグ用）</summary>
        public int Count => _effects.Count;

        /// <summary>
        /// 効果ボリュームを登録する。同一 Id の再登録は置き換え（重複登録を作らない）。
        /// </summary>
        /// <param name="id">効果インスタンスの一意 Id（GetInstanceID 等）</param>
        /// <param name="center">効果球の中心（ワールド座標）</param>
        /// <param name="radius">効果球の半径（m）</param>
        /// <param name="sightMultiplier">この効果が視界に与える倍率（1 = 影響なし）</param>
        public void Register(int id, Vector3 center, float radius, float sightMultiplier)
        {
            Unregister(id);
            _effects.Add(new WeaponEffect { Id = id, Center = center, Radius = radius, SightMultiplier = sightMultiplier });
        }

        /// <summary>効果ボリュームの登録を解除する。未登録 Id は何もしない（冪等）。</summary>
        public void Unregister(int id)
        {
            for (var i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].Id == id)
                {
                    _effects.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// 視線に対する視界倍率を取得する。ターゲットを内包する、または視線線分（目→ターゲット）と交差する
        /// エントリの倍率の最小値を返す。該当がなければ 1（影響なし）。
        /// </summary>
        /// <param name="eyePos">観測者の目の位置</param>
        /// <param name="targetPos">ターゲットの位置</param>
        public float GetSightMultiplier(Vector3 eyePos, Vector3 targetPos)
        {
            var multiplier = 1f;
            foreach (var entry in _effects)
            {
                if (IsPointInSphere(targetPos, entry.Center, entry.Radius)
                    || SegmentIntersectsSphere(eyePos, targetPos, entry.Center, entry.Radius))
                {
                    multiplier = Mathf.Min(multiplier, entry.SightMultiplier);
                }
            }

            return multiplier;
        }

        /// <summary>点が球に内包されるか（境界含む）を判定する。</summary>
        internal static bool IsPointInSphere(Vector3 point, Vector3 center, float radius)
            => (point - center).sqrMagnitude <= radius * radius;

        /// <summary>
        /// 線分と球が交差するか（境界含む）を判定する。線分上の最近接点と球心の距離で判定し、
        /// 退化（from == to）は点判定に一致する。
        /// </summary>
        internal static bool SegmentIntersectsSphere(Vector3 from, Vector3 to, Vector3 center, float radius)
        {
            var segment = to - from;
            var lengthSq = segment.sqrMagnitude;
            var t = lengthSq > 0f ? Mathf.Clamp01(Vector3.Dot(center - from, segment) / lengthSq) : 0f;
            var closest = from + segment * t;
            return (center - closest).sqrMagnitude <= radius * radius;
        }
    }
}
