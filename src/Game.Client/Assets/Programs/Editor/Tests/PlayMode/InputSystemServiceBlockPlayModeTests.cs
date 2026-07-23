using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core.Services;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// InputSystemService の入力ブロック（アクション単位参照カウント）仕様の PlayMode テスト。
    /// 仕様:
    /// - Enable/Disable/Block は全てアクション単位のカウント台帳を共有する
    /// - Disable/Block はカウント +1、Enable/Block解除 はカウント -1 で 0 以下なら有効化（0 にクランプ）
    /// - forceEnable はカウントを無視して有効化し、台帳を 0 にリセットする（ドリフト回復用）
    /// </summary>
    [TestFixture]
    public class InputSystemServiceBlockPlayModeTests : InputTestFixture
    {
        private InputSystemService _service;

        public override void Setup()
        {
            base.Setup();
            _service = new InputSystemService(new LocalizationService());
        }

        public override void TearDown()
        {
            _service?.Shutdown();
            _service = null;
            base.TearDown();
        }

        #region 基本状態

        [Test]
        public void Startup_InitialState_UIEnabledPlayerDisabled()
        {
            _service.Startup();

            Assert.IsTrue(_service.UI.Cancel.enabled, "Startup 直後は UI 入力が有効のはず");
            Assert.IsTrue(_service.UI.Submit.enabled);
            Assert.IsFalse(_service.Player.Move.enabled, "Startup 直後は Player 入力が無効のはず");
            Assert.IsFalse(_service.Player.Jump.enabled);
        }

        [Test]
        public void Shutdown_Restartup_DoesNotCarryOverBlockCounts()
        {
            _service.Startup();
            _service.BlockInputAction(_service.UI.Click); // 意図的に未解放のまま
            Assert.IsFalse(_service.UI.Click.enabled);

            _service.Shutdown();
            _service.Startup();

            Assert.IsTrue(_service.UI.Click.enabled, "再 Startup 後にブロックカウントが持ち越されないはず");
        }

        [Test]
        public void BeforeStartup_Throws()
        {
            Assert.Throws<NullReferenceException>(() => _service.EnablePlayer());
            Assert.Throws<NullReferenceException>(() => _service.DisablePlayer());
            Assert.Throws<NullReferenceException>(() => _service.EnableUI());
            Assert.Throws<NullReferenceException>(() => _service.DisableUI());
            Assert.Throws<NullReferenceException>(() => _service.BlockPlayer());
        }

        #endregion

        #region BlockInputAction

        [Test]
        public void BlockAction_DisablesUntilDispose()
        {
            _service.Startup();

            var scope = _service.BlockInputAction(_service.UI.Cancel);
            Assert.IsFalse(_service.UI.Cancel.enabled, "ブロック中は無効のはず");
            Assert.IsTrue(_service.UI.Submit.enabled, "他のアクションには影響しないはず");

            scope.Dispose();
            Assert.IsTrue(_service.UI.Cancel.enabled, "解放後は有効に戻るはず");
        }

        [Test]
        public void BlockAction_Nested_ReleaseOrderIndependent()
        {
            _service.Startup();

            // 取得と逆順（LIFO）の解放
            var outer = _service.BlockInputAction(_service.UI.Click);
            var inner = _service.BlockInputAction(_service.UI.Click);
            inner.Dispose();
            Assert.IsFalse(_service.UI.Click.enabled, "他方のブロックが残っている間は無効のはず");
            outer.Dispose();
            Assert.IsTrue(_service.UI.Click.enabled);

            // 取得順（FIFO）の解放でも同じ結果になる
            var first = _service.BlockInputAction(_service.UI.Click);
            var second = _service.BlockInputAction(_service.UI.Click);
            first.Dispose();
            Assert.IsFalse(_service.UI.Click.enabled, "解放順に関わらず残ブロックがあれば無効のはず");
            second.Dispose();
            Assert.IsTrue(_service.UI.Click.enabled);
        }

        [Test]
        public void BlockAction_DoubleDispose_DoesNotOverRelease()
        {
            _service.Startup();

            var disposed = _service.BlockInputAction(_service.UI.Click);
            var alive = _service.BlockInputAction(_service.UI.Click);

            disposed.Dispose();
            disposed.Dispose(); // 二重解放

            Assert.IsFalse(_service.UI.Click.enabled, "二重 Dispose で生存中のブロックが解除されないはず");

            alive.Dispose();
            Assert.IsTrue(_service.UI.Click.enabled);
        }

        [Test]
        public void BlockAction_ForeignAction_ThrowsKeyNotFound()
        {
            _service.Startup();

            // Startup 時に列挙されない別アセット由来のアクションは台帳管理外（仕様として例外で顕在化）
            var foreign = new InputAction("Foreign", binding: "<Keyboard>/g");
            Assert.Throws<KeyNotFoundException>(() => _service.BlockInputAction(foreign));
            foreign.Dispose();
        }

        #endregion

        #region BlockPlayer / BlockUI

        [Test]
        public void BlockPlayer_DisablesAllPlayerActions()
        {
            _service.Startup();
            _service.EnablePlayer();

            var scope = _service.BlockPlayer();
            Assert.IsFalse(_service.Player.Move.enabled);
            Assert.IsFalse(_service.Player.Jump.enabled);
            Assert.IsFalse(_service.Player.Attack.enabled);
            Assert.IsTrue(_service.UI.Click.enabled, "UI 側には影響しないはず");

            scope.Dispose();
            Assert.IsTrue(_service.Player.Move.enabled);
            Assert.IsTrue(_service.Player.Jump.enabled);
            Assert.IsTrue(_service.Player.Attack.enabled);
        }

        [Test]
        public void BlockUI_DisablesAllUIActions()
        {
            _service.Startup();

            var scope = _service.BlockUI();
            Assert.IsFalse(_service.UI.Cancel.enabled);
            Assert.IsFalse(_service.UI.Submit.enabled);

            scope.Dispose();
            Assert.IsTrue(_service.UI.Cancel.enabled);
            Assert.IsTrue(_service.UI.Submit.enabled);
        }

        [Test]
        public void BlockPlayer_OverlapsWithBlockAction()
        {
            _service.Startup();
            _service.EnablePlayer();

            var mapScope = _service.BlockPlayer();
            var actionScope = _service.BlockInputAction(_service.Player.Move);

            mapScope.Dispose();
            Assert.IsFalse(_service.Player.Move.enabled, "個別ブロックが残る Move は無効のままのはず");
            Assert.IsTrue(_service.Player.Jump.enabled, "個別ブロックのない Jump は復帰するはず");

            actionScope.Dispose();
            Assert.IsTrue(_service.Player.Move.enabled);
        }

        [Test]
        public void DialogPattern_NestedScopes()
        {
            // Horror ダイアログの実利用パターン: 親ダイアログ表示中に子ダイアログを開閉する
            _service.Startup();
            _service.EnablePlayer();

            var outerPlayer = _service.BlockPlayer();

            var innerPlayer = _service.BlockPlayer();

            innerPlayer.Dispose();

            Assert.IsFalse(_service.Player.Move.enabled, "親ダイアログのブロックが維持されるはず");
            Assert.IsTrue(_service.UI.Submit.enabled, "ブロック対象外の UI 入力は生きているはず");

            outerPlayer.Dispose();

            Assert.IsTrue(_service.Player.Move.enabled);
        }

        #endregion

        #region Enable/Disable の参照カウント

        [Test]
        public void Disable_Twice_RequiresEnableTwice()
        {
            _service.Startup();
            _service.EnablePlayer();

            _service.DisablePlayer();
            _service.DisablePlayer();

            _service.EnablePlayer();
            Assert.IsFalse(_service.Player.Move.enabled, "Disable 2回に対し Enable 1回では無効のままのはず");

            _service.EnablePlayer();
            Assert.IsTrue(_service.Player.Move.enabled);
        }

        [Test]
        public void Enable_OverCall_ClampsAtZero()
        {
            _service.Startup();
            _service.EnablePlayer();
            _service.EnablePlayer();
            _service.EnablePlayer();

            // カウントが負に沈んでいれば以降の Block が 1 対 1 で効かなくなるはず
            var scope = _service.BlockPlayer();
            Assert.IsFalse(_service.Player.Move.enabled, "過剰 Enable 後もブロックは 1 回で効くはず");

            scope.Dispose();
            Assert.IsTrue(_service.Player.Move.enabled);
        }

        [Test]
        public void Disable_And_Block_ShareLedger()
        {
            _service.Startup();
            _service.EnablePlayer();

            _service.DisablePlayer();
            var scope = _service.BlockPlayer();

            _service.EnablePlayer();
            Assert.IsFalse(_service.Player.Move.enabled, "Block 分のカウントが残っている間は無効のままのはず");

            scope.Dispose();
            Assert.IsTrue(_service.Player.Move.enabled);
        }

        #endregion

        #region forceEnable（リセット方式）

        [Test]
        public void Force_RecoversFromDrift()
        {
            _service.Startup();
            _service.EnablePlayer();

            // Enable/Disable の非対称呼び出しによるドリフトを模擬
            _service.DisablePlayer();
            _service.DisablePlayer();

            _service.EnablePlayer(forceEnable: true);
            Assert.IsTrue(_service.Player.Move.enabled, "force はカウントを無視して有効化するはず");

            // 台帳が 0 にリセットされ、以降のブロックが 1 対 1 で機能する（ドリフト完治）
            var scope = _service.BlockPlayer();
            Assert.IsFalse(_service.Player.Move.enabled);
            scope.Dispose();
            Assert.IsTrue(_service.Player.Move.enabled);
        }

        [Test]
        public void Force_IsMapScoped()
        {
            _service.Startup();
            _service.EnablePlayer();

            var uiScope = _service.BlockUI();
            _service.EnablePlayer(forceEnable: true);

            Assert.IsFalse(_service.UI.Click.enabled, "Player の force が UI 側のブロックへ影響しないはず");

            uiScope.Dispose();
            Assert.IsTrue(_service.UI.Click.enabled);
        }

        [Test]
        public void Force_ThenStaleDispose_StaysEnabled()
        {
            // force 後に旧スコープの Dispose が着弾しても 0 クランプで無害に処理される境界挙動の固定
            _service.Startup();
            _service.EnablePlayer();

            var stale = _service.BlockPlayer();
            _service.EnablePlayer(forceEnable: true);
            Assert.IsTrue(_service.Player.Move.enabled);

            stale.Dispose();
            Assert.IsTrue(_service.Player.Move.enabled, "リセット済み台帳への旧 Dispose は無害のはず");

            var scope = _service.BlockPlayer();
            Assert.IsFalse(_service.Player.Move.enabled);
            scope.Dispose();
            Assert.IsTrue(_service.Player.Move.enabled);
        }

        #endregion

        #region 実入力の遮断

        [UnityTest]
        public IEnumerator BlockedAction_DoesNotFirePerformed()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            _service.Startup();
            _service.EnablePlayer();
            yield return null;

            bool fired = false;
            _service.Player.Jump.performed += _ => fired = true;

            var scope = _service.BlockInputAction(_service.Player.Jump);
            Press(keyboard.spaceKey);
            yield return null;
            Release(keyboard.spaceKey);
            yield return null;

            Assert.IsFalse(fired, "ブロック中は performed が発火しないはず");

            scope.Dispose();
            Press(keyboard.spaceKey);
            yield return null;

            Assert.IsTrue(fired, "ブロック解除後は performed が発火するはず");
        }

        [UnityTest]
        public IEnumerator BlockPlayer_DoesNotAffectUIInput()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            _service.Startup();
            _service.EnablePlayer();
            yield return null;

            bool menuFired = false;
            _service.UI.Click.performed += _ => menuFired = true;

            using (_service.BlockPlayer())
            {
                Press(keyboard.escapeKey);
                yield return null;
            }

            Assert.IsTrue(menuFired, "BlockPlayer 中も UI 入力は通るはず");
        }

        #endregion
    }
}
