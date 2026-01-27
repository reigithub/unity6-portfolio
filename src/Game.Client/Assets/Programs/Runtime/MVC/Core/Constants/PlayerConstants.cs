namespace Game.Core.Constants
{
    public static class PlayerConstants
    {
        public const string GameTitleSceneAnimatorStateName = "Salute";
    }

    /// <summary>
    /// プレイヤー物理・移動関連の定数
    /// </summary>
    public static class PlayerPhysicsConstants
    {
        /// <summary>壁との最小距離（スキン幅）</summary>
        public const float SkinWidth = 0.01f;

        /// <summary>この高さ以下の障害物は乗り越え可能</summary>
        public const float StepHeight = 0.3f;

        /// <summary>移動量の最小閾値（sqrMagnitude）</summary>
        public const float MinMovementThreshold = 0.0001f;

        /// <summary>入力判定の閾値（magnitude）</summary>
        public const float InputThreshold = 0.1f;
    }
}