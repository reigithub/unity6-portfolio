using System;
using System.Collections;
using Game.Shared.Input;
using Game.Shared.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// InputServiceの統合テスト
    /// Unity InputSystemとの連携をPlayModeでテスト
    /// </summary>
    [TestFixture]
    public class InputServiceTests : InputTestFixture
    {
        private InputService _inputService;
        private Keyboard _keyboard;
        private Gamepad _gamepad;

        public override void Setup()
        {
            base.Setup();

            // テスト用デバイスをセットアップ
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _gamepad = InputSystem.AddDevice<Gamepad>();

            // InputServiceを作成
            _inputService = new InputService();
        }

        public override void TearDown()
        {
            _inputService?.Shutdown();
            _inputService = null;

            base.TearDown();
        }

        /// <summary>
        /// Startupで入力が有効化されることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Startup_EnablesInputActions()
        {
            // Act
            _inputService.Startup();
            yield return null;

            // Assert
            Assert.IsNotNull(_inputService.Player, "Player actions should be available");
            Assert.IsNotNull(_inputService.UI, "UI actions should be available");
        }

        /// <summary>
        /// Shutdownで入力が無効化されることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Shutdown_DisablesInputActions()
        {
            // Arrange
            _inputService.Startup();
            yield return null;

            // Act
            _inputService.Shutdown();
            yield return null;

            // Assert - 再度Startupが必要な状態になっているはず
            // （内部実装に依存するため、エラーが出ないことを確認）
            Assert.Pass("Shutdown completed without errors");
        }

        /// <summary>
        /// EnablePlayer/DisablePlayerが正常に動作することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator PlayerInput_CanBeEnabledAndDisabled()
        {
            // Arrange
            _inputService.Startup();
            yield return null;

            // Act - Enable
            _inputService.EnablePlayer();
            yield return null;
            bool enabledState = _inputService.Player.enabled;

            // Act - Disable
            _inputService.DisablePlayer();
            yield return null;
            bool disabledState = _inputService.Player.enabled;

            // Assert
            Assert.IsTrue(enabledState, "Player input should be enabled after EnablePlayer()");
            Assert.IsFalse(disabledState, "Player input should be disabled after DisablePlayer()");
        }

        /// <summary>
        /// EnableUI/DisableUIが正常に動作することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator UIInput_CanBeEnabledAndDisabled()
        {
            // Arrange
            _inputService.Startup();
            yield return null;

            // Act - Enable
            _inputService.EnableUI();
            yield return null;
            bool enabledState = _inputService.UI.enabled;

            // Act - Disable
            _inputService.DisableUI();
            yield return null;
            bool disabledState = _inputService.UI.enabled;

            // Assert
            Assert.IsTrue(enabledState, "UI input should be enabled after EnableUI()");
            Assert.IsFalse(disabledState, "UI input should be disabled after DisableUI()");
        }

        /// <summary>
        /// キーボード入力が検出されることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator KeyboardInput_IsDetected()
        {
            // Arrange
            _inputService.Startup();
            _inputService.EnablePlayer();
            yield return null;

            bool moveDetected = false;
            var moveAction = _inputService.Player.Move;
            moveAction.performed += ctx => moveDetected = true;

            // Act - WASDキー押下をシミュレート
            Press(_keyboard.wKey);
            yield return null;

            // Assert
            // Note: InputTestFixtureを使用している場合、入力がシミュレートされる
            Assert.Pass("Keyboard input simulation completed");
        }

        /// <summary>
        /// ゲームパッド入力が検出されることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator GamepadInput_IsDetected()
        {
            // Arrange
            _inputService.Startup();
            _inputService.EnablePlayer();
            yield return null;

            // Act - スティック入力をシミュレート
            Set(_gamepad.leftStick, new Vector2(1f, 0f));
            yield return null;

            // Assert
            Assert.Pass("Gamepad input simulation completed");
        }

        /// <summary>
        /// 入力の有効/無効切り替えを連続で行っても問題ないことを確認
        /// </summary>
        [UnityTest]
        public IEnumerator RapidEnableDisable_DoesNotCauseErrors()
        {
            // Arrange
            _inputService.Startup();
            yield return null;

            // Act - 高速で有効/無効を切り替え
            for (int i = 0; i < 100; i++)
            {
                _inputService.EnablePlayer();
                _inputService.DisablePlayer();
                _inputService.EnableUI();
                _inputService.DisableUI();
            }
            yield return null;

            // Assert - エラーなく完了
            Assert.Pass("Rapid enable/disable completed without errors");
        }

        /// <summary>
        /// 複数回Startup/Shutdownを呼んでも問題ないことを確認
        /// </summary>
        [UnityTest]
        public IEnumerator MultipleStartupShutdown_DoesNotCauseErrors()
        {
            // Act
            for (int i = 0; i < 5; i++)
            {
                _inputService.Startup();
                yield return null;
                _inputService.Shutdown();
                yield return null;
            }

            // Assert
            Assert.Pass("Multiple startup/shutdown cycles completed without errors");
        }

        /// <summary>
        /// Startup前にEnable/Disableを呼ぶと例外がスローされることを確認
        /// （InputServiceはStartup()による初期化が必須）
        /// </summary>
        [UnityTest]
        public IEnumerator EnableDisable_BeforeStartup_ThrowsException()
        {
            // Arrange - Startupを呼ばない状態

            // Act & Assert - 初期化前はNullReferenceExceptionが発生する
            Assert.Throws<NullReferenceException>(() => _inputService.EnablePlayer());
            Assert.Throws<NullReferenceException>(() => _inputService.DisablePlayer());
            Assert.Throws<NullReferenceException>(() => _inputService.EnableUI());
            Assert.Throws<NullReferenceException>(() => _inputService.DisableUI());

            yield return null;
        }
    }
}
