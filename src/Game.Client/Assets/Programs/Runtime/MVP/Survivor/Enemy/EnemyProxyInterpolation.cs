using UnityEngine;

namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// ネットワーク同期の位置補間状態。
    /// サーバーからのスナップショットに基づき、クライアント側で滑らかな位置予測を行う。
    /// </summary>
    public struct EnemyProxyInterpolation
    {
        public Vector3 LastSyncPosition;
        public Vector3 Velocity;
        public float TimeSinceSync;
        public Vector3 CorrectionOffset;

        public void OnSyncReceived(Vector3 serverPos, Vector3 serverVel, float maxCorrectionDist)
        {
            var predicted = LastSyncPosition + Velocity * TimeSinceSync + CorrectionOffset;
            CorrectionOffset = predicted - serverPos;
            if (CorrectionOffset.sqrMagnitude > maxCorrectionDist * maxCorrectionDist)
                CorrectionOffset = Vector3.zero;
            LastSyncPosition = serverPos;
            Velocity = serverVel;
            TimeSinceSync = 0f;
        }

        public Vector3 GetPosition(float deltaTime, float correctionDecayRate)
        {
            TimeSinceSync += deltaTime;
            var predicted = LastSyncPosition + Velocity * TimeSinceSync;
            CorrectionOffset = Vector3.Lerp(CorrectionOffset, Vector3.zero, correctionDecayRate * deltaTime);
            return predicted + CorrectionOffset;
        }
    }
}
