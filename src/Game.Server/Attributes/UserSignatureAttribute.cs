namespace Game.Server.Attributes;

/// <summary>
/// ユーザー派生キーによる HMAC 署名 (<c>DeriveUserKey(userId)</c> + JWT userId) を要求する。
///
/// 認証済みユーザーが body を送信する mutation endpoint に付与する。
/// JWT userId が取得できない場合 (未認証) は middleware が 401 を返す。
///
/// 以下の属性とは互いに排他的であり、同一 action に複数指定してはならない:
/// <list type="bullet">
/// <item><see cref="SkipRequestSigningAttribute"/> (署名を skip する)</item>
/// <item><see cref="UnityServerSignatureAttribute"/> (DS 用の別経路)</item>
/// </list>
///
/// Class-level に付与すると同 Controller の全 action に継承される。
/// 例外的にスキップしたい action には individual に <see cref="SkipRequestSigningAttribute"/> を
/// 上書き指定できる (middleware の判定順序で Skip が優先される)。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class UserSignatureAttribute : Attribute
{
}
