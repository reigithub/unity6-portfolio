namespace Game.Shared.Environment
{
    public static class EnvVarKeys
    {
        /// <summary>
        /// Dedicated Server 認証用 HMAC シークレットの .env キー名。
        /// </summary>
        public const string UnityServerAuthSecretKey = "UNITY_SERVER_AUTH_SESSION_SECRET";
    }
}
