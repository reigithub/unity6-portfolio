using Fusion;
using UnityEngine;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// Fusion 2 プレイヤー入力構造体。
    /// SurvivorFusionRunner.OnInput() で収集 → SurvivorFusionPlayer.FixedUpdateNetwork() で消費。
    /// </summary>
    public struct SurvivorPlayerNetworkInput : INetworkInput
    {
        public Vector2 Move;
        public NetworkBool IsSprinting;
        public float CameraRotationY;
    }
}
