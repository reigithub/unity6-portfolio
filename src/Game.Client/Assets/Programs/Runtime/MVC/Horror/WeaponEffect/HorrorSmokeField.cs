using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Horror.WeaponEffect
{
    /// <summary>
    /// 煙幕フィールド。視覚（子の GroundFog）を論理半径にスケール同期し、生成から破棄までの間
    /// 武器効果集合（<see cref="HorrorWeaponEffectRegistry"/>）へ登録してゾンビの視界を減衰させる。
    /// 効果時間が尽きたら自身を破棄し、破棄によって煙の消滅と登録解除が同時に起きる
    /// （効果・見た目・オブジェクトの寿命が一致することを、呼び出し順ではなく破棄の一点で保証する）。
    /// </summary>
    public class HorrorSmokeField : MonoBehaviour
    {
        /// <summary>煙が視界に与える倍率（ゲームルール: 煙の中/間ならゾンビの視界距離 1/3）</summary>
        public const float SightMultiplier = 1f / 3f;

        [Tooltip("煙の視覚を担う子の ParticleSystem（GroundFog）")]
        [SerializeField] private ParticleSystem _particle;

        [Tooltip("視覚プレハブが素の状態で表現している煙半径（m）。EffectRadius との比で視覚をスケールする")]
        [SerializeField] private float _authoredVisualRadius = 7.1f;

        private HorrorWeaponEffectRegistry _registry;

        /// <summary>
        /// 煙フィールドを起動する。<see cref="HorrorSmokeGrenadeProjectile"/> の起爆から生成直後に呼ばれる。
        /// </summary>
        /// <param name="registry">登録先の武器効果集合</param>
        /// <param name="radius">効果球の半径（m。マスターの EffectRadius）</param>
        /// <param name="durationSeconds">効果の持続秒数（マスターの EffectDurationSeconds）</param>
        public void Initialize(HorrorWeaponEffectRegistry registry, float radius, float durationSeconds)
        {
            _registry = registry;

            // 視覚を論理半径へ同期する。GroundFog は scalingMode=Local のため ParticleSystem 自身の transform に適用する
            if (_particle != null && _authoredVisualRadius > 0f)
                _particle.transform.localScale = Vector3.one * (radius / _authoredVisualRadius);

            _registry.Register(GetInstanceID(), transform.position, radius, SightMultiplier);

            RunLifetimeAsync(durationSeconds).Forget();
        }

        // 解除の唯一の地点。破棄と同一イベントで起きるため、効果の終了・煙の消滅・登録解除がずれない。
        // Initialize 未実行なら _registry は null、Unregister は未登録 Id に対して冪等
        private void OnDestroy()
        {
            _registry?.Unregister(GetInstanceID());
        }

        // 効果時間を消化したら自身を破棄する。破棄が ParticleSystem ごと消すため、煙の消滅・登録解除
        // （OnDestroy）も同時に起きる。シーン終了による破棄では destroyCancellationToken で中断され、
        // 解除はやはり OnDestroy が行う
        private async UniTaskVoid RunLifetimeAsync(float durationSeconds)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(durationSeconds), cancellationToken: destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Destroy(gameObject);
        }
    }
}
