namespace Game.Shared.Constants
{
    public static class AppConstants
    {
        // プレハブを所属させる常駐UnitySceneName
        public const string GameRootScene = "GameRootScene";
    }

    /// <summary>
    /// 時間関連の定数
    /// </summary>
    public static class TimeConstants
    {
        /// <summary>1分あたりの秒数</summary>
        public const int SecondsPerMinute = 60;
    }

    /// <summary>
    /// UIアニメーション関連の定数
    /// </summary>
    public static class UIAnimationConstants
    {
        /// <summary>UI要素の標準フェード時間（秒）</summary>
        public const float StandardFadeDuration = 0.25f;

        /// <summary>シーン遷移のフェードイン時間（秒）</summary>
        public const float SceneTransitionFadeInDuration = 0.5f;

        /// <summary>シーン遷移のフェードアウト時間（秒）</summary>
        public const float SceneTransitionFadeOutDuration = 1f;

        /// <summary>完全不透明</summary>
        public const float AlphaOpaque = 1f;

        /// <summary>完全透明</summary>
        public const float AlphaTransparent = 0f;
    }
}