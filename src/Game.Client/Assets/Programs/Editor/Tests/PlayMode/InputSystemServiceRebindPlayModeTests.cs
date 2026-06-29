using System;
using System.Collections;
using Game.Core.Services;
using Game.Shared.Constants;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// InputSystemService のキーリバインド機能の PlayMode テスト。
    /// InputTestFixture で仮想デバイス入力をシミュレートし、実際のリバインド挙動を検証する。
    /// </summary>
    [TestFixture]
    public class InputSystemServiceRebindPlayModeTests : InputTestFixture
    {
        private InputSystemService _service;
        private Keyboard _keyboard;
        private Gamepad _gamepad;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _gamepad = InputSystem.AddDevice<Gamepad>();
            _service = new InputSystemService();
        }

        public override void TearDown()
        {
            _service?.Shutdown();
            _service = null;
            base.TearDown();
        }

        private InputAction PlayerAction(InputSystemService service, string name)
            => service.Asset.FindActionMap("Player").FindAction(name);

        private int FirstIndex(InputAction action, string scheme)
            => InputSystemService.ResolveSchemeBindingIndices(scheme, action)[0];

        #region Save / Load

        [UnityTest]
        public IEnumerator SaveLoadBindingOverrides_RoundTrip_RestoresOverride()
        {
            _service.Startup();
            yield return null;

            var jump = PlayerAction(_service, "Jump");
            var idx = FirstIndex(jump, InputControlSchemes.KeyboardAndMouse);
            jump.ApplyBindingOverride(idx, "<Keyboard>/j");

            var json = _service.SaveBindingOverridesAsJson();
            Assert.That(json, Is.Not.Null.And.Not.Empty);

            // 別インスタンスへロードして再現
            var service2 = new InputSystemService();
            service2.Startup();
            yield return null;
            service2.LoadBindingOverrides(json);

            var jump2 = PlayerAction(service2, "Jump");
            var idx2 = FirstIndex(jump2, InputControlSchemes.KeyboardAndMouse);
            Assert.That(jump2.bindings[idx2].effectivePath, Is.EqualTo("<Keyboard>/j"));

            service2.Shutdown();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadBindingOverrides_EmptyJson_IsIgnored()
        {
            _service.Startup();
            yield return null;

            var jump = PlayerAction(_service, "Jump");
            var idx = FirstIndex(jump, InputControlSchemes.KeyboardAndMouse);
            var defaultPath = jump.bindings[idx].effectivePath;

            Assert.DoesNotThrow(() => _service.LoadBindingOverrides(""));
            Assert.DoesNotThrow(() => _service.LoadBindingOverrides(null));

            Assert.That(jump.bindings[idx].effectivePath, Is.EqualTo(defaultPath));
            yield return null;
        }

        #endregion

        #region Reset

        [UnityTest]
        public IEnumerator ResetBinding_RemovesOverride_RestoresDefault()
        {
            _service.Startup();
            yield return null;

            var jump = PlayerAction(_service, "Jump");
            var idx = FirstIndex(jump, InputControlSchemes.KeyboardAndMouse);
            var defaultPath = jump.bindings[idx].effectivePath;

            jump.ApplyBindingOverride(idx, "<Keyboard>/j");
            Assert.That(jump.bindings[idx].effectivePath, Is.EqualTo("<Keyboard>/j"));

            _service.ResetBinding(InputControlSchemes.KeyboardAndMouse, "Jump");
            Assert.That(jump.bindings[idx].effectivePath, Is.EqualTo(defaultPath));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResetSchemeBindings_RemovesOnlyTargetScheme()
        {
            _service.Startup();
            yield return null;

            var jump = PlayerAction(_service, "Jump");
            var kbmIdx = FirstIndex(jump, InputControlSchemes.KeyboardAndMouse);
            var padIdx = FirstIndex(jump, InputControlSchemes.Gamepad);
            var kbmDefault = jump.bindings[kbmIdx].effectivePath;
            var padDefault = jump.bindings[padIdx].effectivePath;

            // KBM・Gamepad 両方へ override を適用
            jump.ApplyBindingOverride(kbmIdx, "<Keyboard>/j");
            jump.ApplyBindingOverride(padIdx, "<Gamepad>/buttonNorth");

            // Gamepad のみリセット
            _service.ResetSchemeBindings(InputControlSchemes.Gamepad);

            Assert.That(jump.bindings[padIdx].effectivePath, Is.EqualTo(padDefault),
                "対象スキーム（Gamepad）は既定へ戻る");
            Assert.That(jump.bindings[kbmIdx].effectivePath, Is.EqualTo("<Keyboard>/j"),
                "他スキーム（KBM）の override は保持される");

            // KBM もリセットすると既定へ戻る
            _service.ResetSchemeBindings(InputControlSchemes.KeyboardAndMouse);
            Assert.That(jump.bindings[kbmIdx].effectivePath, Is.EqualTo(kbmDefault));
            yield return null;
        }

        #endregion

        #region StartRebind

        [UnityTest]
        public IEnumerator StartRebind_Completes_AppliesNewBinding()
        {
            _service.Startup();
            yield return null;

            var completed = false;
            var op = _service.StartRebind(InputControlSchemes.KeyboardAndMouse, "Jump", null,
                _ => completed = true,
                () => { });
            yield return null;

            Press(_keyboard.jKey);
            yield return null;
            Release(_keyboard.jKey);
            for (var i = 0; i < 20 && !completed; i++) yield return null;

            Assert.That(completed, Is.True, "リバインドが完了するはず");

            var jump = PlayerAction(_service, "Jump");
            var idx = FirstIndex(jump, InputControlSchemes.KeyboardAndMouse);
            Assert.That(jump.bindings[idx].effectivePath, Is.EqualTo("<Keyboard>/j"));

            op.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartRebind_DuplicateKey_Swaps()
        {
            _service.Startup();
            yield return null;

            var jump = PlayerAction(_service, "Jump");
            var jumpIdx = FirstIndex(jump, InputControlSchemes.KeyboardAndMouse);
            var jumpOriginalPath = jump.bindings[jumpIdx].effectivePath; // <Keyboard>/space

            // Attack(KBM) を <Keyboard>/j に固定し、Jump をその j へリバインドして衝突させる
            var attack = PlayerAction(_service, "Attack");
            var attackIdx = FirstIndex(attack, InputControlSchemes.KeyboardAndMouse);
            attack.ApplyBindingOverride(attackIdx, "<Keyboard>/j");

            var completed = false;
            var op = _service.StartRebind(InputControlSchemes.KeyboardAndMouse, "Jump", null,
                _ => completed = true,
                () => { });
            yield return null;

            Press(_keyboard.jKey);
            yield return null;
            Release(_keyboard.jKey);
            for (var i = 0; i < 20 && !completed; i++) yield return null;

            Assert.That(completed, Is.True, "完了コールバックは呼ばれる（swap 後）");
            Assert.That(jump.bindings[jumpIdx].effectivePath, Is.EqualTo("<Keyboard>/j"),
                "ターゲットは新しいキーを取得する");
            Assert.That(attack.bindings[attackIdx].effectivePath, Is.EqualTo(jumpOriginalPath),
                "相手にはターゲットの旧キーが入れ替わりで割り当てられる");

            op.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartRebind_Dispose_CancelsAndRestoresEnabled()
        {
            _service.Startup();
            yield return null;

            var jump = PlayerAction(_service, "Jump");
            var idx = FirstIndex(jump, InputControlSchemes.KeyboardAndMouse);
            var originalPath = jump.bindings[idx].effectivePath;

            var canceled = false;
            var op = _service.StartRebind(InputControlSchemes.KeyboardAndMouse, "Jump", null,
                _ => { },
                () => canceled = true);
            yield return null;

            op.Dispose(); // 進行中リバインドのキャンセル
            yield return null;

            Assert.That(canceled, Is.True, "Dispose でキャンセルコールバックが呼ばれる");
            Assert.That(jump.bindings[idx].effectivePath, Is.EqualTo(originalPath), "バインドは未変更");
            Assert.That(_service.Player.enabled, Is.True, "キャンセル後に Player マップが再有効化される");

            Assert.DoesNotThrow(() => op.Dispose(), "二重 Dispose でも例外を投げない");
            yield return null;
        }

        #endregion
    }
}
