using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Horror.WeaponEffect
{
    /// <summary>
    /// 煙幕グレネードの投擲物。放物線・バウンドで飛び、最初の衝突から信管（マスターの FuseSeconds）を開始し、
    /// 起爆時に煙フィールド（<see cref="HorrorSmokeField"/>）を展開して自身を破棄する。
    /// 起爆は無音（騒音シグナル・SE とも発行しない仕様）。
    /// </summary>
    public class HorrorSmokeGrenadeProjectile : HorrorProjectile
    {
        [Tooltip("起爆時に展開する煙フィールドのプレハブ")]
        [SerializeField] private HorrorSmokeField _smokeFieldPrefab;

        private bool _fuseStarted;

        private void OnCollisionEnter(Collision collision)
        {
            // 最初の衝突（バウンド開始点）でのみ信管を開始する。以降の転がり・バウンドでは再計時しない
            if (_fuseStarted) return;
            _fuseStarted = true;

            DetonateAfterFuseAsync().Forget();
        }

        // 信管消化後、起爆時点の位置に煙フィールドを展開して自身を破棄する。
        // シーン終了（スポナー Dispose / シーンアンロード）による破棄では destroyCancellationToken で正常に中断される
        private async UniTaskVoid DetonateAfterFuseAsync()
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(Master.FuseSeconds), cancellationToken: destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var fog = Spawner.SpawnEffect(_smokeFieldPrefab, transform.position);
            fog.Initialize(Spawner.Registry, Master.EffectRadius, Master.EffectDurationSeconds);

            Destroy(gameObject);
        }
    }
}
