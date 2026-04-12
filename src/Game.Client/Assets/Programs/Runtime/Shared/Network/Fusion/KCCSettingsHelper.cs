using Fusion.Addons.KCC;
using Game.Shared.Constants;
using UnityEngine;

namespace Game.Shared.Network.Fusion
{
    /// <summary>
    /// KCC の物理・同期設定を一元管理する static ヘルパー。
    /// Fusion プレイヤーの Spawned() と PlayerController の ConfigureKCC() から共用する。
    /// </summary>
    public static class KCCSettingsHelper
    {
        /// <summary>
        /// KCC にデフォルトの物理・同期設定を適用する。
        /// CollisionLayerMask、AuthorityBehavior、AntiJitter、補正速度を一括設定する。
        /// </summary>
        /// <param name="kcc">設定を適用する KCC コンポーネント</param>
        public static void ApplyDefaults(KCC kcc)
        {
            kcc.Settings.CollisionLayerMask = Physics.DefaultRaycastLayers & ~LayerMaskConstants.Enemy;
            kcc.Settings.InputAuthorityBehavior = EKCCAuthorityBehavior.PredictFixed_InterpolateRender;
            kcc.Settings.StateAuthorityBehavior = EKCCAuthorityBehavior.PredictFixed_InterpolateRender;
            kcc.Settings.AntiJitterDistance = new Vector2(0.025f, 0.01f);
            kcc.Settings.PredictionCorrectionSpeed = 15f;
        }
    }
}
