using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// アイテム取得のPlayModeテスト
    /// コリジョン検出、経験値/HP回復をテスト
    /// </summary>
    [TestFixture]
    public class ItemPickupTests
    {
        private GameObject _player;
        private CharacterController _characterController;
        private TestPlayerStats _playerStats;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // プレイヤーを作成
            _player = new GameObject("TestPlayer");
            _player.tag = "Player";
            _player.layer = LayerMask.NameToLayer("Default");

            _characterController = _player.AddComponent<CharacterController>();
            _characterController.height = 2f;
            _characterController.radius = 0.5f;

            _playerStats = _player.AddComponent<TestPlayerStats>();
            _playerStats.CurrentHP = 50;
            _playerStats.MaxHP = 100;
            _playerStats.CurrentExp = 0;

            _player.transform.position = Vector3.zero;

            // 地面を作成
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "TestGround";
            ground.transform.position = new Vector3(0, -0.5f, 0);

            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_player != null) Object.Destroy(_player);

            var ground = GameObject.Find("TestGround");
            if (ground != null) Object.Destroy(ground);

            // テスト中に作成されたアイテムをクリーンアップ
            var items = GameObject.FindObjectsOfType<TestPickupItem>();
            foreach (var item in items)
            {
                Object.Destroy(item.gameObject);
            }

            yield return null;
        }

        /// <summary>
        /// HP回復アイテムの取得でHPが回復することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator HealthItem_Pickup_RestoresHP()
        {
            // Arrange
            var healthItem = CreateHealthItem(new Vector3(0, 0.5f, 2f), healAmount: 30);
            int initialHP = _playerStats.CurrentHP;
            yield return new WaitForFixedUpdate();

            // Act - プレイヤーをアイテムに向かって移動
            yield return MovePlayerTowards(healthItem.transform.position, 3f);

            // Assert
            Assert.AreEqual(initialHP + 30, _playerStats.CurrentHP, "HP should increase by heal amount");
        }

        /// <summary>
        /// 最大HPを超えて回復しないことを確認
        /// </summary>
        [UnityTest]
        public IEnumerator HealthItem_Pickup_DoesNotExceedMaxHP()
        {
            // Arrange
            _playerStats.CurrentHP = 90;
            var healthItem = CreateHealthItem(new Vector3(0, 0.5f, 2f), healAmount: 50);
            yield return new WaitForFixedUpdate();

            // Act
            yield return MovePlayerTowards(healthItem.transform.position, 3f);

            // Assert
            Assert.AreEqual(_playerStats.MaxHP, _playerStats.CurrentHP, "HP should not exceed MaxHP");
        }

        /// <summary>
        /// 経験値アイテムの取得で経験値が増加することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator ExpItem_Pickup_AddsExperience()
        {
            // Arrange
            var expItem = CreateExpItem(new Vector3(0, 0.5f, 2f), expAmount: 100);
            int initialExp = _playerStats.CurrentExp;
            yield return new WaitForFixedUpdate();

            // Act
            yield return MovePlayerTowards(expItem.transform.position, 3f);

            // Assert
            Assert.AreEqual(initialExp + 100, _playerStats.CurrentExp, "Exp should increase by item amount");
        }

        /// <summary>
        /// アイテム取得後にアイテムが消えることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Item_AfterPickup_IsDestroyed()
        {
            // Arrange
            var item = CreateExpItem(new Vector3(0, 0.5f, 2f), expAmount: 10);
            yield return new WaitForFixedUpdate();

            // Act
            yield return MovePlayerTowards(item.transform.position, 3f);
            yield return null; // 1フレーム待機

            // Assert
            Assert.IsTrue(item == null || !item.gameObject.activeInHierarchy,
                "Item should be destroyed or deactivated after pickup");
        }

        /// <summary>
        /// 複数アイテムを連続取得できることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator MultipleItems_CanBePickedUpSequentially()
        {
            // Arrange - アイテムをより近くに配置（CI環境でのフレームレート差を考慮）
            CreateExpItem(new Vector3(0, 0.5f, 0.5f), expAmount: 10);
            CreateExpItem(new Vector3(0, 0.5f, 1.0f), expAmount: 20);
            CreateExpItem(new Vector3(0, 0.5f, 1.5f), expAmount: 30);
            yield return new WaitForFixedUpdate();

            // Act - 速度を上げてタイムアウトも延長（CI環境対応）
            yield return MovePlayerTowards(new Vector3(0, 0, 2f), 10f, 10f);

            // Assert
            Assert.AreEqual(60, _playerStats.CurrentExp, "Should collect all exp items");
        }

        /// <summary>
        /// アイテムの吸引（マグネット）効果をテスト
        /// </summary>
        [UnityTest]
        public IEnumerator MagnetItem_AttractsNearbyItems()
        {
            // Arrange
            var item = CreateExpItem(new Vector3(3f, 0.5f, 0), expAmount: 50);
            var magnetItem = item.GetComponent<TestPickupItem>();
            magnetItem.IsMagnetic = true;
            magnetItem.MagnetRange = 5f;
            magnetItem.MagnetSpeed = 10f;

            yield return new WaitForFixedUpdate();

            // Act - 数フレーム待機してマグネット効果を確認
            var initialDistance = Vector3.Distance(_player.transform.position, item.transform.position);

            for (int i = 0; i < 30; i++)
            {
                magnetItem.UpdateMagnet(_player.transform);
                yield return null;
            }

            var finalDistance = Vector3.Distance(_player.transform.position, item.transform.position);

            // Assert
            Assert.Less(finalDistance, initialDistance, "Item should move closer to player due to magnet effect");
        }

        /// <summary>
        /// HP満タン時にHP回復アイテムの効果がないことを確認
        /// </summary>
        [UnityTest]
        public IEnumerator HealthItem_AtFullHP_NoEffect()
        {
            // Arrange
            _playerStats.CurrentHP = _playerStats.MaxHP;
            var healthItem = CreateHealthItem(new Vector3(0, 0.5f, 2f), healAmount: 30);
            int initialHP = _playerStats.CurrentHP;
            yield return new WaitForFixedUpdate();

            // Act
            yield return MovePlayerTowards(healthItem.transform.position, 3f);

            // Assert
            Assert.AreEqual(initialHP, _playerStats.CurrentHP, "HP should not change when already at max");
        }

        #region Helper Methods

        private GameObject CreateHealthItem(Vector3 position, int healAmount)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            item.name = "HealthItem";
            item.transform.position = position;
            item.transform.localScale = Vector3.one * 0.5f;

            var collider = item.GetComponent<SphereCollider>();
            collider.isTrigger = true;

            var pickup = item.AddComponent<TestPickupItem>();
            pickup.ItemType = TestPickupItem.PickupType.Health;
            pickup.Value = healAmount;
            pickup.PlayerStats = _playerStats;

            return item;
        }

        private GameObject CreateExpItem(Vector3 position, int expAmount)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            item.name = "ExpItem";
            item.transform.position = position;
            item.transform.localScale = Vector3.one * 0.3f;

            var collider = item.GetComponent<SphereCollider>();
            collider.isTrigger = true;

            var pickup = item.AddComponent<TestPickupItem>();
            pickup.ItemType = TestPickupItem.PickupType.Experience;
            pickup.Value = expAmount;
            pickup.PlayerStats = _playerStats;

            return item;
        }

        private IEnumerator MovePlayerTowards(Vector3 target, float speed, float timeout = 5f)
        {
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                var direction = (target - _player.transform.position).normalized;
                direction.y = 0;

                if (direction.magnitude < 0.1f) break;

                _characterController.Move(direction * speed * Time.deltaTime);
                _characterController.Move(Vector3.down * 9.8f * Time.deltaTime);

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        #endregion

        #region Test Classes

        private class TestPlayerStats : MonoBehaviour
        {
            public int CurrentHP { get; set; }
            public int MaxHP { get; set; }
            public int CurrentExp { get; set; }
        }

        private class TestPickupItem : MonoBehaviour
        {
            public enum PickupType { Health, Experience }

            public PickupType ItemType { get; set; }
            public int Value { get; set; }
            public TestPlayerStats PlayerStats { get; set; }
            public bool IsMagnetic { get; set; }
            public float MagnetRange { get; set; }
            public float MagnetSpeed { get; set; }

            private void OnTriggerEnter(Collider other)
            {
                if (other.CompareTag("Player") && PlayerStats != null)
                {
                    ApplyEffect();
                    Destroy(gameObject);
                }
            }

            private void ApplyEffect()
            {
                switch (ItemType)
                {
                    case PickupType.Health:
                        PlayerStats.CurrentHP = Mathf.Min(PlayerStats.CurrentHP + Value, PlayerStats.MaxHP);
                        break;
                    case PickupType.Experience:
                        PlayerStats.CurrentExp += Value;
                        break;
                }
            }

            public void UpdateMagnet(Transform player)
            {
                if (!IsMagnetic) return;

                float distance = Vector3.Distance(transform.position, player.position);
                if (distance <= MagnetRange)
                {
                    var direction = (player.position - transform.position).normalized;
                    transform.position += direction * MagnetSpeed * Time.deltaTime;
                }
            }
        }

        #endregion
    }
}
