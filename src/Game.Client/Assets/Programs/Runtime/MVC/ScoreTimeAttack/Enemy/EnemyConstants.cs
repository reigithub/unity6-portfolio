namespace Game.ScoreTimeAttack.Enemy
{
    /// <summary>
    /// 敵AI関連の定数
    /// </summary>
    public static class EnemyConstants
    {
        /// <summary>
        /// ナビゲーション関連
        /// </summary>
        public static class Navigation
        {
            /// <summary>目的地更新間隔（秒）- 毎フレーム更新による震えを防止</summary>
            public const float DestinationUpdateInterval = 0.2f;

            /// <summary>目的地更新閾値 - この距離以上プレイヤーが移動したら即座に更新</summary>
            public const float DestinationUpdateThreshold = 0.5f;

            /// <summary>回転補間速度</summary>
            public const float RotationSmoothSpeed = 10f;

            /// <summary>速度判定閾値（sqrMagnitude）</summary>
            public const float VelocityThreshold = 0.01f;
        }

        /// <summary>
        /// 視覚検知関連
        /// </summary>
        public static class Vision
        {
            /// <summary>前方視野角の閾値（度）- この角度より小さい場合は前方</summary>
            public const float ForwardAngleMin = 45f;

            /// <summary>前方視野角の閾値（度）- この角度より大きい場合は前方</summary>
            public const float ForwardAngleMax = 315f;

            /// <summary>角度計算用のオフセット</summary>
            public const float AngleOffset = 180f;

            /// <summary>視点の高さオフセット</summary>
            public const float EyeHeightOffset = 0.5f;
        }

        /// <summary>
        /// 検知範囲関連
        /// </summary>
        public static class Detection
        {
            /// <summary>OverlapSphere用の半径倍率</summary>
            public const float RadiusMultiplier = 2f;
        }

        /// <summary>
        /// パトロール関連
        /// </summary>
        public static class Patrol
        {
            /// <summary>デフォルトの残り距離閾値</summary>
            public const float DefaultRemainingDistance = 0.5f;

            /// <summary>回転間隔の最小値（秒）</summary>
            public const float RotationIntervalMin = 3f;

            /// <summary>回転間隔の最大値（秒）</summary>
            public const float RotationIntervalMax = 8f;

            /// <summary>残り距離閾値の最小値</summary>
            public const float RemainingDistanceMin = 0.3f;

            /// <summary>残り距離閾値の最大値</summary>
            public const float RemainingDistanceMax = 0.8f;

            /// <summary>ランダム移動範囲</summary>
            public const float RandomMoveRange = 10f;

            /// <summary>パトロール時の回転速度</summary>
            public const float PatrolRotationSpeed = 10f;

            /// <summary>ランダム回転角度の最大値</summary>
            public const float RandomRotationAngleMax = 180f;
        }
    }
}
