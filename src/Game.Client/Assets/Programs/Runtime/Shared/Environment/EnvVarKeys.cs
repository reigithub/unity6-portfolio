namespace Game.Shared.Environment
{
    public static class EnvVarKeys
    {
        /// <summary>
        /// Dedicated Server 認証用 HMAC シークレットの .env キー名。
        /// </summary>
        public const string UnityServerAuthSecretKey = "UNITY_SERVER_AUTH_SESSION_SECRET";

        /// <summary>
        /// DS が Game.Server に自己登録・ハートビートを送る宛先 URL の環境変数キー名。
        /// </summary>
        public const string GameServerUrl = "GAME_SERVER_URL";

        /// <summary>
        /// DS が Fusion に公開する外部 IP アドレスの環境変数キー名。
        /// </summary>
        public const string PublicAddress = "PUBLIC_ADDRESS";

        /// <summary>
        /// Unity Dedicated Server の Fusion UDP ポート番号の環境変数キー名。
        /// </summary>
        public const string UnityServerPort = "UNITY_SERVER_PORT";

        /// <summary>
        /// Unity Dedicated Server のヘルスチェック TCP ポート番号の環境変数キー名。
        /// </summary>
        public const string UnityServerHealthPort = "UNITY_SERVER_HEALTH_PORT";
    }
}
