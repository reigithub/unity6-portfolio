using Fusion;
using UnityEngine;

namespace Game.Shared.Network.Fusion
{
    /// <summary>
    /// IFusionRunnerService の拡張メソッド群。
    /// Fusion Runner の Physics Scene および DeltaTime を安全に取得するヘルパー。
    /// </summary>
    public static class FusionRunnerServiceExtensions
    {
        /// <summary>
        /// Fusion Runner の PhysicsScene を取得する。
        /// Runner が無効な場合は Physics.defaultPhysicsScene にフォールバック。
        /// </summary>
        /// <param name="runnerService">FusionRunnerService。</param>
        /// <returns>有効な PhysicsScene。</returns>
        public static PhysicsScene GetPhysicsSceneOrDefault(this IFusionRunnerService runnerService)
        {
            if (runnerService?.IsActive == true && runnerService.Runner != null)
                return runnerService.Runner.GetPhysicsScene();
            return Physics.defaultPhysicsScene;
        }

        /// <summary>
        /// Fusion Runner の DeltaTime を取得する。
        /// Runner が無効な場合は Time.deltaTime にフォールバック。
        /// </summary>
        /// <param name="runnerService">FusionRunnerService。</param>
        /// <returns>ゲームロジック用のデルタタイム。</returns>
        public static float GetDeltaTime(this IFusionRunnerService runnerService)
        {
            if (runnerService?.IsActive == true && runnerService.Runner != null)
                return runnerService.Runner.DeltaTime;
            return Time.deltaTime;
        }
    }
}
