using System.Text;
using Game.Library.Shared.RequestSigning;
using Game.Shared.Network.Survivor;
using NUnit.Framework;

namespace Game.Tests.Shared.Network
{
    /// <summary>
    /// SurvivorNetworkAuthenticator の認証判定ロジックをテストする。
    /// Mirror の NetworkAuthenticator 基盤に依存せず、認証判定の純粋ロジックを検証する。
    ///
    /// 認証判定マトリクス:
    /// | SharedSecret | Token     | 結果     |
    /// |-------------|-----------|----------|
    /// | null        | empty     | SP 承認  |
    /// | null        | non-empty | 拒否     |
    /// | set         | valid     | MP 承認  |
    /// | set         | invalid   | 拒否     |
    /// | set         | expired   | 拒否     |
    /// | set         | empty     | 拒否     |
    /// </summary>
    [TestFixture]
    public class AuthenticationDecisionTests
    {
        private static readonly byte[] TestSecretKey = Encoding.UTF8.GetBytes("test-secret-for-auth-decision");

        #region Helpers

        /// <summary>
        /// SurvivorNetworkAuthenticator.OnAuthRequest の判定ロジックを再現する。
        /// Mirror 依存を排除し、純粋な判定アルゴリズムのみをテスト。
        /// </summary>
        private static AuthDecision EvaluateAuthDecision(byte[] sharedSecret, string token)
        {
            // SP モード: token 空 + sharedSecret null → 無条件承認
            if (string.IsNullOrEmpty(token) && sharedSecret == null)
            {
                return AuthDecision.SpApproved;
            }

            // SharedSecret 未設定だがトークンが送られた → 拒否
            if (sharedSecret == null)
            {
                return AuthDecision.Rejected;
            }

            // HMAC 検証
            var parsed = SessionTokenHelper.ParseAndVerify(token, sharedSecret);
            if (parsed == null)
            {
                return AuthDecision.Rejected;
            }

            return AuthDecision.MpApproved;
        }

        private enum AuthDecision
        {
            SpApproved,
            MpApproved,
            Rejected,
        }

        #endregion

        #region SP Mode (SharedSecret == null)

        [Test]
        public void Decision_IsSpApproved_WhenSecretNullAndTokenEmpty()
        {
            var result = EvaluateAuthDecision(sharedSecret: null, token: "");

            Assert.That(result, Is.EqualTo(AuthDecision.SpApproved));
        }

        [Test]
        public void Decision_IsSpApproved_WhenSecretNullAndTokenNull()
        {
            var result = EvaluateAuthDecision(sharedSecret: null, token: null);

            Assert.That(result, Is.EqualTo(AuthDecision.SpApproved));
        }

        [Test]
        public void Decision_IsRejected_WhenSecretNullButTokenProvided()
        {
            var result = EvaluateAuthDecision(sharedSecret: null, token: "some-token");

            Assert.That(result, Is.EqualTo(AuthDecision.Rejected));
        }

        #endregion

        #region MP Mode (SharedSecret set)

        [Test]
        public void Decision_IsMpApproved_WhenTokenIsValid()
        {
            var token = SessionTokenHelper.CreateToken(TestSecretKey, "user1", "match1");

            var result = EvaluateAuthDecision(sharedSecret: TestSecretKey, token: token);

            Assert.That(result, Is.EqualTo(AuthDecision.MpApproved));
        }

        [Test]
        public void Decision_IsRejected_WhenTokenIsInvalid()
        {
            var result = EvaluateAuthDecision(sharedSecret: TestSecretKey, token: "invalid.token");

            Assert.That(result, Is.EqualTo(AuthDecision.Rejected));
        }

        [Test]
        public void Decision_IsRejected_WhenTokenIsEmpty()
        {
            var result = EvaluateAuthDecision(sharedSecret: TestSecretKey, token: "");

            Assert.That(result, Is.EqualTo(AuthDecision.Rejected));
        }

        [Test]
        public void Decision_IsRejected_WhenTokenSignedWithWrongKey()
        {
            var wrongKey = Encoding.UTF8.GetBytes("wrong-key");
            var token = SessionTokenHelper.CreateToken(wrongKey, "user1", "match1");

            var result = EvaluateAuthDecision(sharedSecret: TestSecretKey, token: token);

            Assert.That(result, Is.EqualTo(AuthDecision.Rejected));
        }

        [Test]
        public void Decision_IsRejected_WhenTokenIsExpired()
        {
            // 10分前のタイムスタンプで手動トークン生成 (DefaultExpiry=5分)
            var expiredTimestamp = System.DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
            var payload = $"user1|match1|{expiredTimestamp}";
            var payloadB64 = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');
            var signature = HmacRequestSigner.ComputeSignature(TestSecretKey, payload);
            var token = $"{payloadB64}.{signature}";

            var result = EvaluateAuthDecision(sharedSecret: TestSecretKey, token: token);

            Assert.That(result, Is.EqualTo(AuthDecision.Rejected));
        }

        #endregion

        #region Payload Decode + Auth Combined

        [Test]
        public void FullFlow_SpMode_DecodesPayloadAndApproves()
        {
            // クライアント: ペイロードエンコード（トークンなし）
            var data = SurvivorNetworkConnectionPayload.Encode(3, "");
            var (stageId, token) = SurvivorNetworkConnectionPayload.Decode(data);

            // サーバー: 認証判定
            var result = EvaluateAuthDecision(sharedSecret: null, token: token);

            Assert.That(stageId, Is.EqualTo(3));
            Assert.That(result, Is.EqualTo(AuthDecision.SpApproved));
        }

        [Test]
        public void FullFlow_MpMode_DecodesPayloadAndApproves()
        {
            // クライアント: HMAC トークン付きペイロードエンコード
            var sessionToken = SessionTokenHelper.CreateToken(TestSecretKey, "user1", "match1");
            var data = SurvivorNetworkConnectionPayload.Encode(5, sessionToken);
            var (stageId, token) = SurvivorNetworkConnectionPayload.Decode(data);

            // サーバー: 認証判定
            var result = EvaluateAuthDecision(sharedSecret: TestSecretKey, token: token);

            Assert.That(stageId, Is.EqualTo(5));
            Assert.That(result, Is.EqualTo(AuthDecision.MpApproved));
        }

        [Test]
        public void FullFlow_MpMode_DecodesPayloadAndRejects_WhenWrongKey()
        {
            // クライアント: 不正なキーで署名
            var wrongKey = Encoding.UTF8.GetBytes("attacker-key");
            var sessionToken = SessionTokenHelper.CreateToken(wrongKey, "user1", "match1");
            var data = SurvivorNetworkConnectionPayload.Encode(5, sessionToken);
            var (_, token) = SurvivorNetworkConnectionPayload.Decode(data);

            // サーバー: 認証判定
            var result = EvaluateAuthDecision(sharedSecret: TestSecretKey, token: token);

            Assert.That(result, Is.EqualTo(AuthDecision.Rejected));
        }

        #endregion
    }
}
