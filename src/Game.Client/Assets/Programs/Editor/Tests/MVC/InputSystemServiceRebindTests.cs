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
        private ProjectInputActions _input;
        private InputActionAsset _asset;

        [SetUp]
        public void Setup()
        {
            _input = new ProjectInputActions();
            _asset = _input.asset;
        }

        [TearDown]
        public void TearDown()
        {
            // ProjectDefaultInputSystem.Dispose() は内部で Object.Destroy を呼ぶが
            // EditMode では使用できないため、アセット実体を DestroyImmediate で破棄する。
            if (_asset != null)
                UnityEngine.Object.DestroyImmediate(_asset);
        }

        // Reset は UI マップへ移動済みのため、マップ横断（アセット全体）で解決する
        private InputAction Action(string name) => _asset.FindAction(name);

        #region ResolveSchemeBindingIndices

        [Test]
        public void Resolve_Jump_KeyboardMouse_ReturnsSingleKeyboardBinding()
        {
            var action = Action("Jump");
            var indices = InputSystemService.GetBindingIndicesByControlScheme(InputControlSchemes.KeyboardAndMouse, action);

            Assert.That(indices.Count, Is.EqualTo(1));
            Assert.That(action.bindings[indices[0].Index].effectivePath, Is.EqualTo("<Keyboard>/space"));
        }

        [Test]
        public void Resolve_Jump_Gamepad_ReturnsSingleGamepadBinding()
        {
            var action = Action("Jump");
            var indices = InputSystemService.GetBindingIndicesByControlScheme(InputControlSchemes.Gamepad, action);

            Assert.That(indices.Count, Is.EqualTo(1));
            Assert.That(action.bindings[indices[0].Index].effectivePath, Is.EqualTo("<Gamepad>/buttonSouth"));
        }

        [Test]
        public void Resolve_Move_KeyboardMouse_ReturnsFourCompositeParts()
        {
            var action = Action("Move");
            var indices = InputSystemService.GetBindingIndicesByControlScheme(InputControlSchemes.KeyboardAndMouse, action);

            // WASD コンポジットの4パート（up/down/left/right）
            Assert.That(indices.Count, Is.EqualTo(4));
            foreach (var info in indices)
                Assert.That(action.bindings[info.Index].isPartOfComposite, Is.True);
        }

        [Test]
        public void Resolve_Move_Gamepad_ReturnsLeftStick()
        {
            var action = Action("Move");
            var indices = InputSystemService.GetBindingIndicesByControlScheme(InputControlSchemes.Gamepad, action);

            Assert.That(indices.Count, Is.EqualTo(1));
            Assert.That(action.bindings[indices[0].Index].effectivePath, Is.EqualTo("<Gamepad>/leftStick"));
        }

        [Test]
        public void Resolve_Reset_KeyboardMouse_ReturnsBindings()
        {
            // Reset（UI マップ）の KBM バインドが解決されることを確認
            var action = Action("Reset");
            var indices = InputSystemService.GetBindingIndicesByControlScheme(InputControlSchemes.KeyboardAndMouse, action);

            Assert.That(indices.Count, Is.EqualTo(1));
            Assert.That(action.bindings[indices[0].Index].effectivePath, Is.EqualTo("<Keyboard>/r"));
        }

        [Test]
        public void Resolve_Reset_Gamepad_ReturnsBinding()
        {
            // Reset（UI マップ）は Gamepad バインド（buttonNorth）も持つ
            var action = Action("Reset");
            var indices = InputSystemService.GetBindingIndicesByControlScheme(InputControlSchemes.Gamepad, action);

            Assert.That(indices.Count, Is.EqualTo(1));
            Assert.That(action.bindings[indices[0].Index].effectivePath, Is.EqualTo("<Gamepad>/buttonNorth"));
        }

        [Test]
        public void Resolve_Move_KeyboardMouse_PartUp_ReturnsSingleW()
        {
            var action = Action("Move");
            var indices = InputSystemService.GetBindingIndicesByControlScheme(InputControlSchemes.KeyboardAndMouse, action, "up");

            Assert.That(indices.Count, Is.EqualTo(1));
            Assert.That(action.bindings[indices[0].Index].effectivePath, Is.EqualTo("<Keyboard>/w"));
        }

        [Test]
        public void Resolve_Move_KeyboardMouse_PartDown_ReturnsSingleS()
        {
            var action = Action("Move");
            var indices = InputSystemService.GetBindingIndicesByControlScheme(InputControlSchemes.KeyboardAndMouse, action, "down");

            Assert.That(indices.Count, Is.EqualTo(1));
            Assert.That(action.bindings[indices[0].Index].effectivePath, Is.EqualTo("<Keyboard>/s"));
        }

        [Test]
        public void Resolve_SingleAction_WithPartName_ReturnsSingle()
        {
            var action = Action("Jump");
            var indices = InputSystemService.GetBindingIndicesByControlScheme(InputControlSchemes.KeyboardAndMouse, action, "up");

            Assert.That(indices.Count, Is.EqualTo(1));
        }

        #endregion

        #region TryFindConflict

        [Test]
        public void TryFindConflict_SameSchemeDuplicate_ReturnsConflictBinding()
        {
            // Jump を対象に、別アクションへ既定割当した <Keyboard>/numpad5 を候補にすると、その相手が返る
            var jump = Action("Jump");
            var jumpIndex = InputSystemService.GetBindingIndicesByControlScheme(InputControlSchemes.KeyboardAndMouse, jump)[0].Index;

            // Attack(KBM) を numpad5 に固定し、衝突相手として特定できることを確認
            var attack = Action("Attack");
            var attackIndex = InputSystemService.GetBindingIndicesByControlScheme(InputControlSchemes.KeyboardAndMouse, attack)[0].Index;
            attack.ApplyBindingOverride(attackIndex, "<Keyboard>/numpad5");

            var found = InputSystemService.TryFindConflictAction(
                _asset, InputControlSchemes.KeyboardAndMouse, jump, jumpIndex, "<Keyboard>/numpad5",
                out var conflictAction, out var conflictIndex);

            Assert.That(found, Is.True);
            Assert.That(conflictAction, Is.SameAs(attack));
            Assert.That(conflictIndex, Is.EqualTo(attackIndex));
        }

        [Test]
        public void TryFindConflict_UnusedKey_ReturnsFalseAndNullOut()
        {
            var jump = Action("Jump");
            var jumpIndex = InputSystemService.GetBindingIndicesByControlScheme(InputControlSchemes.KeyboardAndMouse, jump)[0].Index;

            var found = InputSystemService.TryFindConflictAction(
                _asset, InputControlSchemes.KeyboardAndMouse, jump, jumpIndex, "<Keyboard>/numpad5",
                out var conflictAction, out var conflictIndex);

            Assert.That(found, Is.False);
            Assert.That(conflictAction, Is.Null);
            Assert.That(conflictIndex, Is.EqualTo(-1));
        }

        [Test]
        public void TryFindConflict_DifferentScheme_ReturnsFalse()
        {
            // Gamepad のパスは KBM スキームでは衝突対象にならない
            var jump = Action("Jump");
            var jumpIndex = InputSystemService.GetBindingIndicesByControlScheme(InputControlSchemes.KeyboardAndMouse, jump)[0].Index;

            var found = InputSystemService.TryFindConflictAction(
                _asset, InputControlSchemes.KeyboardAndMouse, jump, jumpIndex, "<Gamepad>/buttonSouth",
                out _, out _);

            Assert.That(found, Is.False);
        }

        [Test]
        public void TryFindConflict_OwnCurrentPath_ReturnsFalse()
        {
            // 自分自身の現在パスは衝突扱いしない
            var jump = Action("Jump");
            var jumpIndex = InputSystemService.GetBindingIndicesByControlScheme(InputControlSchemes.KeyboardAndMouse, jump)[0].Index;
            var ownPath = jump.bindings[jumpIndex].effectivePath;

            var found = InputSystemService.TryFindConflictAction(
                _asset, InputControlSchemes.KeyboardAndMouse, jump, jumpIndex, ownPath,
                out _, out _);

            Assert.That(found, Is.False);
        }

        #endregion
    }
}
