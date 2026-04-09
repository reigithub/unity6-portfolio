namespace Game.Shared.Network.Fusion
{
    /// <summary>
    /// Fusion Dedicated Server の接続認証プロバイダ。
    /// OnConnectRequest で ConnectionToken を検証する。
    /// </summary>
    public interface IUnityServerAuthProvider
    {
        /// <summary>
        /// ConnectionToken を検証し、接続を許可するかどうかを返す。
        /// </summary>
        /// <param name="token">クライアントから送られた ConnectionToken バイト列</param>
        /// <returns>認証成功なら true、失敗なら false</returns>
        bool ValidateConnectionToken(byte[] token);
    }
}
