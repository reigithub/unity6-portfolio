using Fusion.Addons.KCC;
using UnityEngine;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// KCCData.Speed で KinematicSpeed を上書きする Processor。
    /// EnvironmentProcessor（Priority 1000）の後に実行される。
    /// </summary>
    public sealed class SpeedProcessor : KCCProcessor, ISetKinematicSpeed
    {
        public override float GetPriority(KCC kcc) => EnvironmentProcessor.DefaultPriority - 1;

        public void Execute(ISetKinematicSpeed stage, KCC kcc, KCCData data)
        {
            if (data.Speed > 0f)
            {
                data.KinematicSpeed = data.Speed;
            }
        }
    }
}
