namespace Game.Server.Attributes;

/// <summary>
/// Dedicated Server からの経路を示す。
/// <see cref="Game.Server.Middleware.RequestSigningMiddleware"/> は
/// <c>UnityServerSettings.SecretKey</c> を直接 HMAC key として使用し、
/// <c>DeriveUserKey(userId)</c> は適用しない。JWT は要求しない。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class UnityServerSignatureAttribute : Attribute
{
}
