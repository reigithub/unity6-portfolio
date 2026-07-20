using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using NUnit.Framework;
using R3;

namespace Game.Editor.Tests
{
    /// <summary>
    /// GameSceneService のダイアログライフサイクル保証のテスト。
    /// TransitionDialogAsync の complete-or-cancel 保証（TerminateCore がサービス側で
    /// ResultTcs をキャンセルする）と、ダイアログ完了継続が次シーン起動より先に
    /// 同期実行されることを固定する。
    /// この保証は InputSystemService の入力ブロック（using スコープの確実な Dispose）の前提。
    /// </summary>
    [TestFixture]
    public class GameSceneServiceDialogTests
    {
        private GameSceneService _service;
        private List<IGameScene> _gameScenes;
        private static List<string> s_eventLog;

        [SetUp]
        public void SetUp()
        {
            _service = new GameSceneService();
            _gameScenes = (List<IGameScene>)typeof(GameSceneService)
                .GetField("_gameScenes", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_service);
            s_eventLog = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            _gameScenes?.Clear();
            _service = null;
            s_eventLog = null;
        }

        [Test]
        public async Task TransitionDialog_NormalClose_ReturnsResult()
        {
            var dialogTask = _service.TransitionDialogAsync<FakeDialogScene, bool>().Preserve();
            var dialog = (FakeDialogScene)_gameScenes[^1];

            Assert.IsFalse(dialogTask.Status.IsCompleted(), "リザルト確定前は未完了のはず");

            dialog.TrySetResult(true);

            var result = await dialogTask;
            Assert.IsTrue(result);
            Assert.IsFalse(_gameScenes.Contains(dialog), "クローズ後は履歴から除去されるはず");
        }

        [Test]
        public async Task TransitionDialog_ExternalTransition_CompletesWithDefault()
        {
            // シーン側に cancel 実装が無くても、TerminateCore がサービス側でキャンセルし
            // ダイアログ Task が放棄されない（= 呼び出し元 using が必ず Dispose される）ことの検証
            var dialogTask = _service.TransitionDialogAsync<FakeDialogScene, bool>().Preserve();
            var dialog = (FakeDialogScene)_gameScenes[^1];

            await _service.TransitionAsync<RecordingScene>();

            Assert.IsTrue(dialogTask.Status.IsCompleted(), "外部遷移後にダイアログ Task が放棄されず完了するはず");
            var result = await dialogTask;
            Assert.IsFalse(result, "キャンセル時は default が返るはず");
            Assert.IsTrue(dialog.TerminateCalled, "外部遷移時に Terminate が呼ばれるはず");
            Assert.IsFalse(_gameScenes.Contains(dialog), "外部遷移後は履歴から除去されるはず");
        }

        [Test]
        public async Task TransitionDialog_ContinuationRunsBeforeNextSceneStartup()
        {
            // ダイアログ完了継続（実利用では using スコープの Dispose）が
            // 次シーンの Startup より前に同期実行されることを固定する
            var dialogTask = _service.TransitionDialogAsync<FakeDialogScene, bool>().Preserve();
            var tracked = dialogTask.ContinueWith(_ => s_eventLog.Add("dialog:continuation"));

            await _service.TransitionAsync<RecordingScene>();
            await tracked;

            int continuationIndex = s_eventLog.IndexOf("dialog:continuation");
            int startupIndex = s_eventLog.IndexOf("next:startup");
            Assert.GreaterOrEqual(continuationIndex, 0, "ダイアログ継続が実行されているはず");
            Assert.GreaterOrEqual(startupIndex, 0, "次シーンの Startup が実行されているはず");
            Assert.Less(continuationIndex, startupIndex, "ダイアログ継続は次シーン Startup より先に実行されるはず");
        }

        [Test]
        public async Task TerminateLastAsync_CancelsResultTcs()
        {
            var scene = new FakeDialogScene
            {
                State = GameSceneState.Processing,
                ResultTcs = new UniTaskCompletionSource<bool>(),
            };
            _gameScenes.Add(scene);

            await _service.TerminateLastAsync(clearHistory: true);

            Assert.IsTrue(scene.TerminateCalled);
            Assert.AreEqual(UniTaskStatus.Canceled, scene.ResultTcs.Task.Status, "TerminateCore がサービス側で ResultTcs をキャンセルするはず");
        }

        #region Fake Classes

        /// <summary>
        /// 自前の cancel 処理を持たないリザルトシーン。
        /// complete-or-cancel 保証がシーン実装ではなくサービス側にあることを検証するため、
        /// 意図的に GameDialogScene を継承しない。
        /// </summary>
        private class FakeDialogScene : IGameScene, IGameSceneResult<bool>
        {
            public GameSceneState State { get; set; }
            public Func<IGameScene, UniTask> ArgHandler { get; set; }
            public CompositeDisposable Disposables { get; } = new();
            public bool Result { get; set; }
            public UniTaskCompletionSource<bool> ResultTcs { get; set; }

            public bool TerminateCalled { get; private set; }

            public UniTask Terminate()
            {
                TerminateCalled = true;
                return UniTask.CompletedTask;
            }

            public bool TrySetResult(bool result)
            {
                Result = result;
                return ResultTcs.TrySetResult(result);
            }

            public bool TrySetCanceled() => ResultTcs.TrySetCanceled();

            public bool TrySetException(Exception e) => ResultTcs.TrySetException(e);
        }

        /// <summary>外部遷移先。Startup の実行を共有ログへ記録する。</summary>
        private class RecordingScene : IGameScene
        {
            public GameSceneState State { get; set; }
            public Func<IGameScene, UniTask> ArgHandler { get; set; }
            public CompositeDisposable Disposables { get; } = new();

            public UniTask Startup()
            {
                s_eventLog.Add("next:startup");
                return UniTask.CompletedTask;
            }
        }

        #endregion
    }
}
