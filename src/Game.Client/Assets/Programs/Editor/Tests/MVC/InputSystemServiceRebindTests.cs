using Game.Core.Services;
using Game.Shared.Constants;
using Game.Shared.Input;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Game.Tests.MVC
{
    /// <summary>
    /// リバインドのスキーム解決・重複判定（純関数）の EditMode テスト。
    /// 実際の <see cref="ProjectDefaultInputSystem"/> アセットを用いて binding 構造を検証する。
    /// </summary>
    [TestFixture]
    public class InputSystemServiceRebindTests
    {
        private ProjectDefaultInputSystem _input;
        private InputActionAsset _asset;
        private InputActionMap _player;

        [SetUp]
        public void Setup()
        {
            _input = new ProjectDefaultInputSystem();
            _asset = _input.asset;
            _player = _asset.FindActionMap("Player");
        }

        [TearDown]
        public void TearDown()
        {
            // ProjectDefaultInputSystem.Dispose() は内部で Object.Destroy を呼ぶが
            // EditMode では使用できないため、アセット実体を DestroyImmediate で破棄する。
            if (_asset != null)
                UnityEngine.Object.DestroyImmediate(_asset);
        }

        private InputAction Action(string name) => _player.FindAction(name);

        #region ResolveSchemeBindingIndices

        [Test]
        public void Resolve_Jump_KeyboardMouse_ReturnsSingleKeyboardBinding()
        {
            var action = Action("Jump");
            var indices = InputSystemService.ResolveSchemeBindingIndices(InputConstants.KeyboardAndMouse, action);

            Assert.That(indices.Count, Is.EqualTo(1));
            Assert.That(action.bindings[indices[0]].effectivePath, Is.EqualTo("<Keyboard>/space"));
        }

        [Test]
        public void Resolve_Jump_Gamepad_ReturnsSingleGamepadBinding()
        {
            var action = Action("Jump");
            var indices = InputSystemService.ResolveSchemeBindingIndices(InputConstants.Gamepad, action);

            Assert.That(indices.Count, Is.EqualTo(1));
            Assert.That(action.bindings[indices[0]].effectivePath, Is.EqualTo("<Gamepad>/buttonSouth"));
        }

        [Test]
        public void Resolve_Move_KeyboardMouse_ReturnsFourCompositeParts()
        {
            var action = Action("Move");
            var indices = InputSystemService.ResolveSchemeBindingIndices(InputConstants.KeyboardAndMouse, action);

            // WASD コンポジットの4パート（up/down/left/right）
            Assert.That(indices.Count, Is.EqualTo(4));
            foreach (var index in indices)
                Assert.That(action.bindings[index].isPartOfComposite, Is.True);
        }

        [Test]
        public void Resolve_Move_Gamepad_ReturnsLeftStick()
        {
            var action = Action("Move");
            var indices = InputSystemService.ResolveSchemeBindingIndices(InputConstants.Gamepad, action);

            Assert.That(indices.Count, Is.EqualTo(1));
            Assert.That(action.bindings[indices[0]].effectivePath, Is.EqualTo("<Gamepad>/leftStick"));
        }

        [Test]
        public void Resolve_Reset_KeyboardMouse_ReturnsBindings()
        {
            // groups 整備後、Reset の KBM バインドが解決されることを確認
            var action = Action("Reset");
            var indices = InputSystemService.ResolveSchemeBindingIndices(InputConstants.KeyboardAndMouse, action);

            Assert.That(indices.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Resolve_Reset_Gamepad_ReturnsEmpty()
        {
            // Reset は Gamepad バインドを持たない
            var action = Action("Reset");
            var indices = InputSystemService.ResolveSchemeBindingIndices(InputConstants.Gamepad, action);

            Assert.That(indices.Count, Is.EqualTo(0));
        }

        [Test]
        public void Resolve_Move_KeyboardMouse_PartUp_ReturnsSingleW()
        {
            var action = Action("Move");
            var indices = InputSystemService.ResolveSchemeBindingIndices(InputConstants.KeyboardAndMouse, action, "up");

            Assert.That(indices.Count, Is.EqualTo(1));
            Assert.That(action.bindings[indices[0]].effectivePath, Is.EqualTo("<Keyboard>/w"));
        }

        [Test]
        public void Resolve_Move_KeyboardMouse_PartDown_ReturnsSingleS()
        {
            var action = Action("Move");
            var indices = InputSystemService.ResolveSchemeBindingIndices(InputConstants.KeyboardAndMouse, action, "down");

            Assert.That(indices.Count, Is.EqualTo(1));
            Assert.That(action.bindings[indices[0]].effectivePath, Is.EqualTo("<Keyboard>/s"));
        }

        [Test]
        public void Resolve_SingleAction_WithPartName_ReturnsEmpty()
        {
            // 単体アクション（Jump）に partName を指定しても該当パートは存在しない
            var action = Action("Jump");
            var indices = InputSystemService.ResolveSchemeBindingIndices(InputConstants.KeyboardAndMouse, action, "up");

            Assert.That(indices.Count, Is.EqualTo(0));
        }

        #endregion

        #region WouldConflict

        [Test]
        public void WouldConflict_SameSchemeDuplicate_ReturnsTrue()
        {
            // Jump(KBM) を対象に、Attack の KBM に存在する <Keyboard>/enter を候補にすると衝突
            var jump = Action("Jump");
            var jumpIndex = InputSystemService.ResolveSchemeBindingIndices(InputConstants.KeyboardAndMouse, jump)[0];

            var conflict = InputSystemService.WouldConflict(
                _asset, InputConstants.KeyboardAndMouse, jump, jumpIndex, "<Keyboard>/enter");

            Assert.That(conflict, Is.True);
        }

        [Test]
        public void WouldConflict_DifferentScheme_ReturnsFalse()
        {
            // Gamepad のパスは KBM スキームでは衝突対象にならない
            var jump = Action("Jump");
            var jumpIndex = InputSystemService.ResolveSchemeBindingIndices(InputConstants.KeyboardAndMouse, jump)[0];

            var conflict = InputSystemService.WouldConflict(
                _asset, InputConstants.KeyboardAndMouse, jump, jumpIndex, "<Gamepad>/buttonSouth");

            Assert.That(conflict, Is.False);
        }

        [Test]
        public void WouldConflict_OwnCurrentPath_ReturnsFalse()
        {
            // 自分自身の現在パスは衝突扱いしない
            var jump = Action("Jump");
            var jumpIndex = InputSystemService.ResolveSchemeBindingIndices(InputConstants.KeyboardAndMouse, jump)[0];
            var ownPath = jump.bindings[jumpIndex].effectivePath;

            var conflict = InputSystemService.WouldConflict(
                _asset, InputConstants.KeyboardAndMouse, jump, jumpIndex, ownPath);

            Assert.That(conflict, Is.False);
        }

        [Test]
        public void WouldConflict_UnusedKey_ReturnsFalse()
        {
            var jump = Action("Jump");
            var jumpIndex = InputSystemService.ResolveSchemeBindingIndices(InputConstants.KeyboardAndMouse, jump)[0];

            var conflict = InputSystemService.WouldConflict(
                _asset, InputConstants.KeyboardAndMouse, jump, jumpIndex, "<Keyboard>/numpad5");

            Assert.That(conflict, Is.False);
        }

        #endregion
    }
}
