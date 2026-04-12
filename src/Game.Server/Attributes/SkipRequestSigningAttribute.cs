namespace Game.Server.Attributes;

/// <summary>
/// この endpoint への HMAC 署名検証をスキップする。
/// 以下のいずれかのカテゴリに該当する endpoint に付与する:
///
/// <list type="number">
/// <item>
/// <term>Anonymous 認証確立</term>
/// <description>
/// パスワード/デバイス fingerprint 等で認証トークンを発行する。
/// 例: <c>/api/auth/login</c>, <c>/api/auth/guest</c>, <c>/api/auth/email/login</c>
/// </description>
/// </item>
/// <item>
/// <term>Anonymous 認証継続</term>
/// <description>
/// 長寿命 refresh token で短寿命 access token を再発行する
/// (refresh token 自体が認証情報として body に含まれる)。
/// 例: <c>/api/auth/refresh</c>
/// </description>
/// </item>
/// <item>
/// <term>Anonymous 認証準備</term>
/// <description>
/// メール検証・パスワードリセットトークン処理等の未認証経路。
/// 例: <c>/api/auth/email/verify</c>, <c>/api/auth/email/forgot-password</c>,
/// <c>/api/auth/email/reset-password</c>
/// </description>
/// </item>
/// <item>
/// <term>JWT のみ要求 (署名不要)</term>
/// <description>
/// JWT 認証は必要だが HMAC 署名は不要な特例。<c>[Authorize]</c> と併用する。
/// 例: <c>/api/unity-server/issue-token</c> (JWT 必須 + 署名不要)
/// </description>
/// </item>
/// </list>
///
/// JWT 必須かどうかは別途 <c>[Authorize]</c> で制御する。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class SkipRequestSigningAttribute : Attribute
{
}
