using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// プレイヤー移動のPlayModeテスト
    /// 物理演算・CharacterControllerを含む移動処理をテスト
    /// </summary>
    [TestFixture]
    public class PlayerMovementTests
    {
        private GameObject _testPlayer;
        private CharacterController _characterController;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // テスト用プレイヤーオブジェクトを作成
            _testPlayer = new GameObject("TestPlayer");
            _characterController = _testPlayer.AddComponent<CharacterController>();

            // 基本設定
            _characterController.height = 2f;
            _characterController.radius = 0.5f;
            _characterController.center = new Vector3(0, 1f, 0);

            // 地面を作成
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "TestGround";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(10f, 1f, 10f);

            // プレイヤーを地面の上に配置
            _testPlayer.transform.position = new Vector3(0, 0.1f, 0);

            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // テストオブジェクトをクリーンアップ
            if (_testPlayer != null)
            {
                Object.Destroy(_testPlayer);
            }

            var ground = GameObject.Find("TestGround");
            if (ground != null)
            {
                Object.Destroy(ground);
            }

            yield return null;
        }

        /// <summary>
        /// CharacterControllerで前方移動ができることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator CharacterController_MoveForward_ChangesPosition()
        {
            // Arrange
            var startPosition = _testPlayer.transform.position;
            var moveDirection = Vector3.forward;
            float moveSpeed = 5f;

            // Act - 複数フレームで移動
            for (int i = 0; i < 10; i++)
            {
                _characterController.Move(moveDirection * moveSpeed * Time.deltaTime);
                yield return null;
            }

            // Assert
            var endPosition = _testPlayer.transform.position;
            Assert.Greater(endPosition.z, startPosition.z, "Player should move forward (positive Z)");
        }

        /// <summary>
        /// 横移動ができることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator CharacterController_MoveRight_ChangesPosition()
        {
            // Arrange
            var startPosition = _testPlayer.transform.position;
            var moveDirection = Vector3.right;
            float moveSpeed = 5f;

            // Act
            for (int i = 0; i < 10; i++)
            {
                _characterController.Move(moveDirection * moveSpeed * Time.deltaTime);
                yield return null;
            }

            // Assert
            var endPosition = _testPlayer.transform.position;
            Assert.Greater(endPosition.x, startPosition.x, "Player should move right (positive X)");
        }

        /// <summary>
        /// 斜め移動ができることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator CharacterController_MoveDiagonal_ChangesPosition()
        {
            // Arrange
            var startPosition = _testPlayer.transform.position;
            var moveDirection = new Vector3(1f, 0f, 1f).normalized;
            float moveSpeed = 5f;

            // Act
            for (int i = 0; i < 10; i++)
            {
                _characterController.Move(moveDirection * moveSpeed * Time.deltaTime);
                yield return null;
            }

            // Assert
            var endPosition = _testPlayer.transform.position;
            Assert.Greater(endPosition.x, startPosition.x, "Player should move in X direction");
            Assert.Greater(endPosition.z, startPosition.z, "Player should move in Z direction");
        }

        /// <summary>
        /// isGroundedが正しく検出されることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator CharacterController_OnGround_IsGroundedTrue()
        {
            // Arrange - 地面に接地させる
            _testPlayer.transform.position = new Vector3(0, 0.1f, 0);

            // Act - 重力を適用して接地
            for (int i = 0; i < 30; i++)
            {
                _characterController.Move(Vector3.down * 9.8f * Time.deltaTime);
                yield return new WaitForFixedUpdate();
            }

            // Assert
            Assert.IsTrue(_characterController.isGrounded, "Player should be grounded");
        }

        /// <summary>
        /// 空中にいる場合isGroundedがfalseになることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator CharacterController_InAir_IsGroundedFalse()
        {
            // Arrange - 高い位置に配置
            _testPlayer.transform.position = new Vector3(0, 10f, 0);
            yield return new WaitForFixedUpdate();

            // Assert
            Assert.IsFalse(_characterController.isGrounded, "Player should not be grounded when in air");
        }

        /// <summary>
        /// 壁との衝突が正しく検出されることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator CharacterController_WallCollision_StopsMovement()
        {
            // Arrange - 壁を作成
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "TestWall";
            wall.transform.position = new Vector3(0, 1f, 2f);
            wall.transform.localScale = new Vector3(10f, 3f, 0.5f);

            _testPlayer.transform.position = new Vector3(0, 0.1f, 0);
            yield return new WaitForFixedUpdate();

            // 接地させる
            for (int i = 0; i < 10; i++)
            {
                _characterController.Move(Vector3.down * 9.8f * Time.deltaTime);
                yield return new WaitForFixedUpdate();
            }

            // Act - 壁に向かって移動
            float totalMovement = 0f;
            for (int i = 0; i < 100; i++)
            {
                var flags = _characterController.Move(Vector3.forward * 10f * Time.deltaTime);
                totalMovement += 10f * Time.deltaTime;
                yield return null;
            }

            // Assert - 壁で止まっている（壁の位置より前）
            var finalPosition = _testPlayer.transform.position;
            Assert.Less(finalPosition.z, 2f, "Player should be stopped by wall");

            // Cleanup
            Object.Destroy(wall);
        }

        /// <summary>
        /// 回転が正しく適用されることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Player_Rotation_ChangesForward()
        {
            // Arrange
            var originalForward = _testPlayer.transform.forward;

            // Act - 90度回転
            _testPlayer.transform.Rotate(0, 90f, 0);
            yield return null;

            // Assert
            var newForward = _testPlayer.transform.forward;
            Assert.AreNotEqual(originalForward, newForward, "Forward direction should change after rotation");

            // 約90度回転したことを確認（右を向く）
            Assert.AreEqual(1f, newForward.x, 0.01f, "Should be facing right (X = 1)");
            Assert.AreEqual(0f, newForward.z, 0.01f, "Should be facing right (Z = 0)");
        }

        /// <summary>
        /// スロープ上で移動できることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator CharacterController_OnSlope_CanMove()
        {
            // Arrange - スロープを作成（45度以下）
            var slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slope.name = "TestSlope";
            slope.transform.position = new Vector3(0, 0.5f, 3f);
            slope.transform.localScale = new Vector3(5f, 1f, 5f);
            slope.transform.rotation = Quaternion.Euler(-30f, 0, 0); // 30度の傾斜

            _testPlayer.transform.position = new Vector3(0, 0.1f, 0);
            _characterController.slopeLimit = 45f;

            yield return new WaitForFixedUpdate();

            // Act - スロープに向かって移動
            var startPosition = _testPlayer.transform.position;
            for (int i = 0; i < 60; i++)
            {
                _characterController.Move(Vector3.forward * 3f * Time.deltaTime);
                _characterController.Move(Vector3.down * 9.8f * Time.deltaTime);
                yield return null;
            }

            // Assert
            var endPosition = _testPlayer.transform.position;
            Assert.Greater(endPosition.z, startPosition.z, "Player should move forward");

            // Cleanup
            Object.Destroy(slope);
        }

        /// <summary>
        /// 移動速度が正しく反映されることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Movement_Speed_IsProportional()
        {
            // Arrange
            var startPosition = _testPlayer.transform.position;
            float slowSpeed = 2f;
            float fastSpeed = 10f;

            // Act - 遅い速度で移動
            _testPlayer.transform.position = startPosition;
            for (int i = 0; i < 10; i++)
            {
                _characterController.Move(Vector3.forward * slowSpeed * Time.deltaTime);
                yield return null;
            }
            var slowEndPosition = _testPlayer.transform.position;
            float slowDistance = Vector3.Distance(startPosition, slowEndPosition);

            // Act - 速い速度で移動
            _testPlayer.transform.position = startPosition;
            for (int i = 0; i < 10; i++)
            {
                _characterController.Move(Vector3.forward * fastSpeed * Time.deltaTime);
                yield return null;
            }
            var fastEndPosition = _testPlayer.transform.position;
            float fastDistance = Vector3.Distance(startPosition, fastEndPosition);

            // Assert
            Assert.Greater(fastDistance, slowDistance, "Faster speed should result in greater distance");
        }
    }
}
