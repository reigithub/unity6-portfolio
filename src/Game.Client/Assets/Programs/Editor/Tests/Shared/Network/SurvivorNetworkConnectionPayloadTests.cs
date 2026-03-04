using System.Text;
using Game.Shared.Network.Survivor;
using NUnit.Framework;

namespace Game.Tests.Shared.Network
{
    /// <summary>
    /// SurvivorNetworkConnectionPayload のテスト。
    /// MemoryPack によるエンコード/デコードのラウンドトリップ、
    /// null/empty ハンドリング、レガシー UTF-8 フォールバックを検証する。
    /// </summary>
    [TestFixture]
    public class SurvivorNetworkConnectionPayloadTests
    {
        #region Encode / Decode Roundtrip

        [Test]
        public void Decode_ReturnsCorrectValues_WhenEncodedWithStageIdAndToken()
        {
            // Arrange
            var encoded = SurvivorNetworkConnectionPayload.Encode(5, "test-session-token");

            // Act
            var (stageId, sessionToken) = SurvivorNetworkConnectionPayload.Decode(encoded);

            // Assert
            Assert.That(stageId, Is.EqualTo(5));
            Assert.That(sessionToken, Is.EqualTo("test-session-token"));
        }

        [Test]
        public void Decode_ReturnsEmptyToken_WhenEncodedWithoutToken()
        {
            // Arrange
            var encoded = SurvivorNetworkConnectionPayload.Encode(3);

            // Act
            var (stageId, sessionToken) = SurvivorNetworkConnectionPayload.Decode(encoded);

            // Assert
            Assert.That(stageId, Is.EqualTo(3));
            Assert.That(sessionToken, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Decode_ReturnsEmptyToken_WhenEncodedWithNullToken()
        {
            // Arrange
            var encoded = SurvivorNetworkConnectionPayload.Encode(1, null);

            // Act
            var (stageId, sessionToken) = SurvivorNetworkConnectionPayload.Decode(encoded);

            // Assert
            Assert.That(stageId, Is.EqualTo(1));
            Assert.That(sessionToken, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Decode_PreservesLargeStageId()
        {
            // Arrange
            var encoded = SurvivorNetworkConnectionPayload.Encode(99999, "token");

            // Act
            var (stageId, _) = SurvivorNetworkConnectionPayload.Decode(encoded);

            // Assert
            Assert.That(stageId, Is.EqualTo(99999));
        }

        [Test]
        public void Decode_PreservesHmacToken()
        {
            // Arrange: 実際の HMAC トークン形式 (Base64Url.hex)
            var hmacToken = "dXNlcjF8bWF0Y2gxfDE3MDk1MjMyMDA.abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
            var encoded = SurvivorNetworkConnectionPayload.Encode(1, hmacToken);

            // Act
            var (_, sessionToken) = SurvivorNetworkConnectionPayload.Decode(encoded);

            // Assert
            Assert.That(sessionToken, Is.EqualTo(hmacToken));
        }

        #endregion

        #region Null / Empty Input

        [Test]
        public void Decode_ReturnsDefaults_WhenDataIsNull()
        {
            // Act
            var (stageId, sessionToken) = SurvivorNetworkConnectionPayload.Decode(null);

            // Assert
            Assert.That(stageId, Is.EqualTo(1));
            Assert.That(sessionToken, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Decode_ReturnsDefaults_WhenDataIsEmpty()
        {
            // Act
            var (stageId, sessionToken) = SurvivorNetworkConnectionPayload.Decode(new byte[0]);

            // Assert
            Assert.That(stageId, Is.EqualTo(1));
            Assert.That(sessionToken, Is.EqualTo(string.Empty));
        }

        #endregion

        #region Legacy UTF-8 Fallback

        [Test]
        public void Decode_FallsBackToUtf8_WhenDataIsNotMemoryPack()
        {
            // Arrange: MemoryPack でない生の UTF-8 文字列
            var legacyData = Encoding.UTF8.GetBytes("legacy-token-value");

            // Act
            var (stageId, sessionToken) = SurvivorNetworkConnectionPayload.Decode(legacyData);

            // Assert: レガシーフォールバック → stageId=1, token=UTF-8文字列
            Assert.That(stageId, Is.EqualTo(1));
            Assert.That(sessionToken, Is.EqualTo("legacy-token-value"));
        }

        #endregion

        #region Encode Output

        [Test]
        public void Encode_ReturnsNonEmptyByteArray()
        {
            var encoded = SurvivorNetworkConnectionPayload.Encode(1, "token");

            Assert.That(encoded, Is.Not.Null);
            Assert.That(encoded.Length, Is.GreaterThan(0));
        }

        [Test]
        public void Encode_ReturnsDifferentBytes_ForDifferentStageIds()
        {
            var encoded1 = SurvivorNetworkConnectionPayload.Encode(1, "token");
            var encoded2 = SurvivorNetworkConnectionPayload.Encode(2, "token");

            Assert.That(encoded1, Is.Not.EqualTo(encoded2));
        }

        #endregion
    }
}
