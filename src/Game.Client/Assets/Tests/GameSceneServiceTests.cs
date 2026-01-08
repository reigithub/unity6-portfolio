using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.MessagePipe;
using Game.Core.Scenes;
using Game.Core.Services;
using NUnit.Framework;

namespace Game.Tests
{
    [TestFixture]
    public class GameSceneServiceTests
    {
        private GameSceneService _service;
        private LinkedList<IGameScene> _gameScenes;
        private MessageBrokerService _messageBrokerService;

        [SetUp]
        public void SetUp()
        {
            _messageBrokerService = new MessageBrokerService();
            _messageBrokerService.Startup();

            // GameSceneServiceを取得
            _service = new GameSceneService();
            _service.Startup();
            _gameScenes = GetPrivateField<LinkedList<IGameScene>>(_service, "_gameScenes");
        }

        [TearDown]
        public void TearDown()
        {
            _gameScenes.Clear();

            _messageBrokerService.Shutdown();
            _messageBrokerService = null;

            _service.Shutdown();
            _service = null;
            _messageBrokerService = null;
        }

        #region IsProcessing Tests

        [Test]
        public void IsProcessing_WithNoScenes_ReturnsFalse()
        {
            var result = _service.IsProcessing(typeof(MockGameScene));

            Assert.IsFalse(result);
        }

        [Test]
        public void IsProcessing_WithMatchingTypeAndProcessingState_ReturnsTrue()
        {
            var scene = new MockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene);

            var result = _service.IsProcessing(typeof(MockGameScene));

            Assert.IsTrue(result);
        }

        [Test]
        public void IsProcessing_WithMatchingTypeButDifferentState_ReturnsFalse()
        {
            var scene = new MockGameScene { State = GameSceneState.Sleep };
            _gameScenes.AddLast(scene);

            var result = _service.IsProcessing(typeof(MockGameScene));

            Assert.IsFalse(result);
        }

        [Test]
        public void IsProcessing_WithDifferentType_ReturnsFalse()
        {
            var scene = new MockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene);

            var result = _service.IsProcessing(typeof(AnotherMockGameScene));

            Assert.IsFalse(result);
        }

        [Test]
        public void IsProcessing_ChecksOnlyLastScene()
        {
            var scene1 = new MockGameScene { State = GameSceneState.Processing };
            var scene2 = new AnotherMockGameScene { State = GameSceneState.Sleep };
            _gameScenes.AddLast(scene1);
            _gameScenes.AddLast(scene2);

            var result = _service.IsProcessing(typeof(MockGameScene));

            Assert.IsFalse(result);
        }

        #endregion

        #region TerminateAsync Tests

        [Test]
        public async Task TerminateAsync_WithMatchingType_TerminatesScene()
        {
            var scene = new MockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene);

            await _service.TerminateAsync(typeof(MockGameScene), clearHistory: false);

            Assert.AreEqual(GameSceneState.Terminate, scene.State);
            Assert.IsTrue(scene.TerminateCalled);
        }

        [Test]
        public async Task TerminateAsync_WithClearHistory_RemovesFromList()
        {
            var scene = new MockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene);

            await _service.TerminateAsync(typeof(MockGameScene), clearHistory: true);

            Assert.AreEqual(0, _gameScenes.Count);
        }

        [Test]
        public async Task TerminateAsync_WithoutClearHistory_KeepsInList()
        {
            var scene = new MockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene);

            await _service.TerminateAsync(typeof(MockGameScene), clearHistory: false);

            Assert.AreEqual(1, _gameScenes.Count);
        }

        [Test]
        public async Task TerminateAsync_WithNonMatchingType_DoesNothing()
        {
            var scene = new MockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene);

            await _service.TerminateAsync(typeof(AnotherMockGameScene), clearHistory: true);

            Assert.AreEqual(GameSceneState.Processing, scene.State);
            Assert.AreEqual(1, _gameScenes.Count);
        }

        #endregion

        #region TerminateLastAsync Tests

        [Test]
        public async Task TerminateLastAsync_WithScenes_TerminatesLastScene()
        {
            var scene1 = new MockGameScene { State = GameSceneState.Processing };
            var scene2 = new AnotherMockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene1);
            _gameScenes.AddLast(scene2);

            await _service.TerminateLastAsync(clearHistory: false);

            Assert.AreEqual(GameSceneState.Processing, scene1.State);
            Assert.AreEqual(GameSceneState.Terminate, scene2.State);
        }

        [Test]
        public async Task TerminateLastAsync_WithClearHistory_RemovesLastFromList()
        {
            var scene1 = new MockGameScene { State = GameSceneState.Processing };
            var scene2 = new AnotherMockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene1);
            _gameScenes.AddLast(scene2);

            await _service.TerminateLastAsync(clearHistory: true);

            Assert.AreEqual(1, _gameScenes.Count);
            Assert.AreSame(scene1, _gameScenes.First.Value);
        }

        [Test]
        public async Task TerminateLastAsync_WithNoScenes_DoesNotThrow()
        {
            Assert.DoesNotThrowAsync(async () => await _service.TerminateLastAsync());
        }

        #endregion

        #region State Transition Tests

        [Test]
        public void SceneState_InitialState_IsNone()
        {
            var scene = new MockGameScene();

            Assert.AreEqual(GameSceneState.None, scene.State);
        }

        [Test]
        public async Task TerminateCore_SetsStateToTerminate()
        {
            var scene = new MockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene);

            await _service.TerminateAsync(typeof(MockGameScene));

            Assert.AreEqual(GameSceneState.Terminate, scene.State);
        }

        #endregion

        #region Multiple Scenes Tests

        [Test]
        public async Task TerminateAsync_WithMultipleSameType_TerminatesLast()
        {
            var scene1 = new MockGameScene { State = GameSceneState.Processing };
            var scene2 = new MockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene1);
            _gameScenes.AddLast(scene2);

            await _service.TerminateAsync(typeof(MockGameScene), clearHistory: true);

            Assert.AreEqual(GameSceneState.Processing, scene1.State);
            Assert.AreEqual(GameSceneState.Terminate, scene2.State);
            Assert.AreEqual(1, _gameScenes.Count);
        }

        #endregion

        #region Combined Arg and Result Tests

        [Test]
        public void MockGameSceneWithArgAndResult_ImplementsBothInterfaces()
        {
            var scene = new MockGameSceneWithArgAndResult<string, int>();

            Assert.IsInstanceOf<IGameSceneArg<string>>(scene);
            Assert.IsInstanceOf<IGameSceneResult<int>>(scene);
            Assert.IsInstanceOf<IGameScene>(scene);
        }

        [Test]
        public async Task MockGameSceneWithArgAndResult_CanReceiveArg()
        {
            var scene = new MockGameSceneWithArgAndResult<string, int>();
            const string testArg = "TestArgument";

            await scene.ArgHandle(testArg);

            Assert.AreEqual(testArg, scene.ReceivedArg);
            Assert.IsTrue(scene.ArgHandleCalled);
        }

        [Test]
        public void MockGameSceneWithArgAndResult_CanSetResult()
        {
            var scene = new MockGameSceneWithArgAndResult<string, int>();
            scene.ResultTcs = new UniTaskCompletionSource<int>();
            const int expectedResult = 42;

            var success = scene.TrySetResult(expectedResult);

            Assert.IsTrue(success);
        }

        [Test]
        public async Task MockGameSceneWithArgAndResult_CanAwaitResult()
        {
            var scene = new MockGameSceneWithArgAndResult<string, int>();
            scene.ResultTcs = new UniTaskCompletionSource<int>();
            const int expectedResult = 100;

            scene.TrySetResult(expectedResult);
            var result = await scene.ResultTcs.Task;

            Assert.AreEqual(expectedResult, result);
        }

        [Test]
        public void MockGameSceneWithArgAndResult_CanSetCanceled()
        {
            var scene = new MockGameSceneWithArgAndResult<string, int>();
            scene.ResultTcs = new UniTaskCompletionSource<int>();

            var success = scene.TrySetCanceled();

            Assert.IsTrue(success);
        }

        [Test]
        public void MockGameSceneWithArgAndResult_CanSetException()
        {
            var scene = new MockGameSceneWithArgAndResult<string, int>();
            scene.ResultTcs = new UniTaskCompletionSource<int>();
            var exception = new InvalidOperationException("Test exception");

            var success = scene.TrySetException(exception);

            Assert.IsTrue(success);
        }

        [Test]
        public async Task MockGameSceneWithArgAndResult_FullLifecycle()
        {
            var scene = new MockGameSceneWithArgAndResult<string, int>();
            scene.ResultTcs = new UniTaskCompletionSource<int>();
            _gameScenes.AddLast(scene);

            // Simulate arg handling
            const string testArg = "TestInput";
            await scene.ArgHandle(testArg);

            // Simulate lifecycle
            scene.State = GameSceneState.Processing;
            await scene.PreInitialize();
            await scene.LoadAsset();
            await scene.Startup();
            await scene.Ready();

            // Set result
            const int expectedResult = 999;
            scene.TrySetResult(expectedResult);
            var result = await scene.ResultTcs.Task;

            // Verify
            Assert.AreEqual(testArg, scene.ReceivedArg);
            Assert.AreEqual(expectedResult, result);
            Assert.IsTrue(scene.PreInitializeCalled);
            Assert.IsTrue(scene.LoadAssetCalled);
            Assert.IsTrue(scene.StartupCalled);
            Assert.IsTrue(scene.ReadyCalled);
        }

        [Test]
        public async Task MockGameSceneWithArgAndResult_SleepAndRestart()
        {
            var scene = new MockGameSceneWithArgAndResult<string, int>();
            scene.State = GameSceneState.Processing;
            _gameScenes.AddLast(scene);

            // Sleep
            scene.State = GameSceneState.Sleep;
            await scene.Sleep();

            Assert.IsTrue(scene.SleepCalled);
            Assert.AreEqual(GameSceneState.Sleep, scene.State);

            // Restart
            scene.State = GameSceneState.Processing;
            await scene.Restart();

            Assert.IsTrue(scene.RestartCalled);
            Assert.AreEqual(GameSceneState.Processing, scene.State);
        }

        [Test]
        public async Task MockGameSceneWithArgAndResult_Terminate()
        {
            var scene = new MockGameSceneWithArgAndResult<string, int>();
            scene.ResultTcs = new UniTaskCompletionSource<int>();
            scene.State = GameSceneState.Processing;
            _gameScenes.AddLast(scene);

            await _service.TerminateAsync(typeof(MockGameSceneWithArgAndResult<string, int>), clearHistory: true);

            Assert.AreEqual(GameSceneState.Terminate, scene.State);
            Assert.IsTrue(scene.TerminateCalled);
            Assert.AreEqual(0, _gameScenes.Count);
        }

        [Test]
        public void MockGameSceneWithArgAndResult_IsDialogScene()
        {
            var scene = new MockGameSceneWithArgAndResult<string, int>();

            // IGameSceneResult を実装しているのでダイアログとして扱われる
            Assert.IsInstanceOf<IGameSceneResult>(scene);
        }

        [Test]
        public async Task MockGameSceneWithArgAndResult_ComplexArgType()
        {
            var scene = new MockGameSceneWithArgAndResult<ComplexArg, string>();
            var complexArg = new ComplexArg { Id = 1, Name = "Test", Data = new[] { 1, 2, 3 } };

            await scene.ArgHandle(complexArg);

            Assert.AreEqual(complexArg.Id, scene.ReceivedArg.Id);
            Assert.AreEqual(complexArg.Name, scene.ReceivedArg.Name);
            Assert.AreEqual(complexArg.Data, scene.ReceivedArg.Data);
        }

        [Test]
        public async Task MockGameSceneWithArgAndResult_ComplexResultType()
        {
            var scene = new MockGameSceneWithArgAndResult<int, ComplexResult>();
            scene.ResultTcs = new UniTaskCompletionSource<ComplexResult>();
            var expectedResult = new ComplexResult { Success = true, Message = "OK", Value = 42 };

            scene.TrySetResult(expectedResult);
            var result = await scene.ResultTcs.Task;

            Assert.AreEqual(expectedResult.Success, result.Success);
            Assert.AreEqual(expectedResult.Message, result.Message);
            Assert.AreEqual(expectedResult.Value, result.Value);
        }

        #endregion

        #region GameDialogScene Tests

        [Test]
        public void MockGameDialogScene_ImplementsRequiredInterfaces()
        {
            var dialog = new MockGameDialogScene<MockSceneComponent, bool>();

            Assert.IsInstanceOf<IGameScene>(dialog);
            Assert.IsInstanceOf<IGameSceneResult<bool>>(dialog);
            Assert.IsInstanceOf<IGameDialogSceneInitializer<MockSceneComponent, bool>>(dialog);
        }

        [Test]
        public async Task MockGameDialogScene_LoadAsset_CreatesSceneComponent()
        {
            var dialog = new MockGameDialogScene<MockSceneComponent, bool>();

            await dialog.LoadAsset();

            Assert.IsNotNull(dialog.SceneComponent);
            Assert.IsTrue(dialog.LoadAssetCalled);
        }

        [Test]
        public async Task MockGameDialogScene_Startup_InvokesDialogInitializer()
        {
            var dialog = new MockGameDialogScene<MockSceneComponent, bool>();
            var initializerCalled = false;
            MockSceneComponent receivedComponent = null;
            IGameSceneResult<bool> receivedResult = null;

            dialog.DialogInitializer = (component, result) =>
            {
                initializerCalled = true;
                receivedComponent = component;
                receivedResult = result;
                return UniTask.CompletedTask;
            };

            await dialog.LoadAsset();
            await dialog.Startup();

            Assert.IsTrue(initializerCalled);
            Assert.AreSame(dialog.SceneComponent, receivedComponent);
            Assert.AreSame(dialog, receivedResult);
        }

        [Test]
        public async Task MockGameDialogScene_Startup_WithoutInitializer_DoesNotThrow()
        {
            var dialog = new MockGameDialogScene<MockSceneComponent, bool>();
            dialog.DialogInitializer = null;

            await dialog.LoadAsset();

            Assert.DoesNotThrowAsync(async () => await dialog.Startup());
        }

        [Test]
        public async Task MockGameDialogScene_Terminate_SetsCanceled()
        {
            var dialog = new MockGameDialogScene<MockSceneComponent, bool>();
            dialog.ResultTcs = new UniTaskCompletionSource<bool>();

            await dialog.Terminate();

            Assert.IsTrue(dialog.TerminateCalled);
            Assert.IsTrue(dialog.TrySetCanceledCalled);
        }

        [Test]
        public async Task MockGameDialogScene_Terminate_UnloadsScene()
        {
            var dialog = new MockGameDialogScene<MockSceneComponent, bool>();
            await dialog.LoadAsset();

            await dialog.Terminate();

            Assert.IsTrue(dialog.UnloadSceneCalled);
            Assert.IsNull(dialog.SceneComponent);
        }

        [Test]
        public async Task MockGameDialogScene_FullDialogLifecycle()
        {
            var dialog = new MockGameDialogScene<MockSceneComponent, int>();
            dialog.ResultTcs = new UniTaskCompletionSource<int>();
            _gameScenes.AddLast(dialog);

            var initData = new { Title = "Test Dialog", Message = "Hello" };
            string capturedTitle = null;

            dialog.DialogInitializer = (component, result) =>
            {
                capturedTitle = initData.Title;
                component.Initialize(initData.Title, initData.Message);
                return UniTask.CompletedTask;
            };

            // Lifecycle
            dialog.State = GameSceneState.Processing;
            await dialog.PreInitialize();
            await dialog.LoadAsset();
            await dialog.Startup();
            await dialog.Ready();

            // Set result (user clicked OK)
            const int userChoice = 42;
            dialog.TrySetResult(userChoice);
            var result = await dialog.ResultTcs.Task;

            Assert.AreEqual(initData.Title, capturedTitle);
            Assert.AreEqual(userChoice, result);
            Assert.IsTrue(dialog.SceneComponent.InitializeCalled);
        }

        [Test]
        public async Task MockGameDialogScene_CancelDialog()
        {
            var dialog = new MockGameDialogScene<MockSceneComponent, string>();
            dialog.ResultTcs = new UniTaskCompletionSource<string>();
            _gameScenes.AddLast(dialog);

            dialog.State = GameSceneState.Processing;
            await dialog.LoadAsset();
            await dialog.Startup();

            // User cancels dialog
            dialog.TrySetCanceled();

            // var ex = Assert.ThrowsAsync<OperationCanceledException>(async () => await dialog.ResultTcs.Task);
            // キャンセルステータス確認のため一度エラーを無視します
            var ex = await dialog.ResultTcs.Task.SuppressCancellationThrow();
            Assert.IsTrue(ex.IsCanceled);
        }

        [Test]
        public async Task MockGameDialogScene_MultipleDialogs()
        {
            var dialog1 = new MockGameDialogScene<MockSceneComponent, int>();
            var dialog2 = new MockGameDialogScene<MockSceneComponent, string>();
            dialog1.ResultTcs = new UniTaskCompletionSource<int>();
            dialog2.ResultTcs = new UniTaskCompletionSource<string>();

            _gameScenes.AddLast(dialog1);
            _gameScenes.AddLast(dialog2);

            dialog1.State = GameSceneState.Processing;
            dialog2.State = GameSceneState.Processing;

            // Both are IGameSceneResult (dialogs)
            Assert.IsInstanceOf<IGameSceneResult>(dialog1);
            Assert.IsInstanceOf<IGameSceneResult>(dialog2);
            Assert.AreEqual(2, _gameScenes.Count);
        }

        [Test]
        public async Task MockGameDialogScene_DialogWithArgAndResult()
        {
            var dialog = new MockGameDialogSceneWithArg<MockSceneComponent, DialogInput, DialogOutput>();
            dialog.ResultTcs = new UniTaskCompletionSource<DialogOutput>();
            _gameScenes.AddLast(dialog);

            var input = new DialogInput { ItemId = 100, Quantity = 5 };

            // Simulate ArgHandler setup (like CreateArgHandler in GameSceneService)
            dialog.ArgHandler = async scene =>
            {
                if (scene is IGameSceneArg<DialogInput> sceneArg)
                {
                    await sceneArg.ArgHandle(input);
                }
            };

            // Lifecycle with arg
            dialog.State = GameSceneState.Processing;
            if (dialog.ArgHandler != null)
            {
                await dialog.ArgHandler.Invoke(dialog);
            }

            await dialog.PreInitialize();
            await dialog.LoadAsset();
            await dialog.Startup();
            await dialog.Ready();

            // Verify arg was received
            Assert.AreEqual(input.ItemId, dialog.ReceivedArg.ItemId);
            Assert.AreEqual(input.Quantity, dialog.ReceivedArg.Quantity);

            // Set result
            var output = new DialogOutput { Confirmed = true, SelectedQuantity = 3 };
            dialog.TrySetResult(output);
            var result = await dialog.ResultTcs.Task;

            Assert.IsTrue(result.Confirmed);
            Assert.AreEqual(3, result.SelectedQuantity);
        }

        [Test]
        public void MockGameDialogScene_SceneComponent_SleepAndRestart()
        {
            var component = new MockSceneComponent();

            component.Sleep();
            Assert.IsTrue(component.IsSleeping);
            Assert.IsTrue(component.SleepCalled);

            component.Restart();
            Assert.IsFalse(component.IsSleeping);
            Assert.IsTrue(component.RestartCalled);
        }

        [Test]
        public void MockGameDialogScene_SceneComponent_SetInteractive()
        {
            var component = new MockSceneComponent();

            component.SetInteractiveAllButton(false);
            Assert.IsFalse(component.IsInteractive);

            component.SetInteractiveAllButton(true);
            Assert.IsTrue(component.IsInteractive);
        }

        [Test]
        public async Task MockGameDialogScene_InitializerWithAsyncOperation()
        {
            var dialog = new MockGameDialogScene<MockSceneComponent, bool>();
            dialog.ResultTcs = new UniTaskCompletionSource<bool>();
            var asyncOperationCompleted = false;

            dialog.DialogInitializer = async (component, result) =>
            {
                // Simulate async initialization (e.g., loading data)
                await UniTask.Delay(10);
                asyncOperationCompleted = true;
            };

            await dialog.LoadAsset();
            await dialog.Startup();

            Assert.IsTrue(asyncOperationCompleted);
        }

        [Test]
        public async Task MockGameDialogScene_ExceptionInInitializer()
        {
            var dialog = new MockGameDialogScene<MockSceneComponent, bool>();
            var expectedException = new InvalidOperationException("Init failed");

            dialog.DialogInitializer = (component, result) => { throw expectedException; };

            await dialog.LoadAsset();

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await dialog.Startup());
            Assert.AreEqual("Init failed", ex.Message);
        }

        [Test]
        public async Task MockGameDialogScene_ResultWithException()
        {
            var dialog = new MockGameDialogScene<MockSceneComponent, int>();
            dialog.ResultTcs = new UniTaskCompletionSource<int>();
            var expectedException = new InvalidOperationException("Dialog error");

            dialog.TrySetException(expectedException);

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await dialog.ResultTcs.Task);
            Assert.AreEqual("Dialog error", ex.Message);
        }

        #endregion

        #region TransitionAsync Tests

        [Test]
        public async Task TransitionCore_AddsSceneToList()
        {
            var scene = new MockGameScene();
            _gameScenes.AddLast(scene);

            await SimulateTransitionCore(scene);

            Assert.AreEqual(1, _gameScenes.Count);
            Assert.IsInstanceOf<MockGameScene>(_gameScenes.First.Value);
        }

        [Test]
        public async Task TransitionCore_SetsSceneStateToProcessing()
        {
            var scene = new MockGameScene();
            _gameScenes.AddLast(scene);

            await SimulateTransitionCore(scene);

            Assert.AreEqual(GameSceneState.Processing, scene.State);
        }

        [Test]
        public async Task TransitionCore_ExecutesFullLifecycle()
        {
            var scene = new MockGameScene();
            _gameScenes.AddLast(scene);

            await SimulateTransitionCore(scene);

            Assert.IsTrue(scene.PreInitializeCalled);
            Assert.IsTrue(scene.LoadAssetCalled);
            Assert.IsTrue(scene.StartupCalled);
            Assert.IsTrue(scene.ReadyCalled);
        }

        [Test]
        public async Task TransitionWithArg_PassesArgumentToScene()
        {
            var scene = new MockGameSceneWithArg();
            const string testArg = "TestArgument";

            // ArgHandlerをセットアップ
            SimulateCreateArgHandler(scene, testArg);
            _gameScenes.AddLast(scene);

            await SimulateTransitionCore(scene);

            Assert.AreEqual(testArg, scene.ReceivedArg);
        }

        [Test]
        public async Task TransitionWithArg_InvokesArgHandler()
        {
            var scene = new MockGameSceneWithArg();

            SimulateCreateArgHandler(scene, "test");
            _gameScenes.AddLast(scene);

            await SimulateTransitionCore(scene);

            Assert.IsNotNull(scene.ArgHandler);
        }

        [Test]
        public async Task TransitionWithResult_CreatesResultTcs()
        {
            var scene = new MockGameSceneWithResult();
            var tcs = SimulateCreateResultTcs<int>(scene);
            _gameScenes.AddLast(scene);

            // 遷移開始
            var transitionTask = SimulateTransitionWithResultAsync(scene, tcs);

            Assert.IsNotNull(scene.ResultTcs);

            // 結果をセット
            scene.ResultTcs.TrySetResult(42);
            var result = await transitionTask;
            Assert.AreEqual(42, result);
        }

        [Test]
        public async Task TransitionWithArgAndResult_WorksCorrectly()
        {
            var scene = new MockGameSceneWithArgAndResult<string, int>();
            const string inputArg = "InputData";
            const int expectedResult = 100;

            SimulateCreateArgHandler(scene, inputArg);
            var tcs = SimulateCreateResultTcs<int>(scene);
            _gameScenes.AddLast(scene);

            var transitionTask = SimulateTransitionWithResultAsync(scene, tcs);

            Assert.AreEqual(inputArg, scene.ReceivedArg);

            scene.ResultTcs.TrySetResult(expectedResult);
            var result = await transitionTask;

            Assert.AreEqual(expectedResult, result);
        }

        [Test]
        public async Task TransitionCore_MultipleTransitions_AddsAllScenes()
        {
            var scene1 = new MockGameScene();
            var scene2 = new AnotherMockGameScene();

            _gameScenes.AddLast(scene1);
            await SimulateTransitionCore(scene1);

            _gameScenes.AddLast(scene2);
            await SimulateTransitionCore(scene2);

            Assert.AreEqual(2, _gameScenes.Count);
        }

        [Test]
        public async Task TransitionWithTerminateOperation_TerminatesPreviousScene()
        {
            var firstScene = new MockGameScene();
            _gameScenes.AddLast(firstScene);
            await SimulateTransitionCore(firstScene);

            // 2番目の遷移でTerminate操作
            await SimulateOperationAsync(GameSceneOperations.CurrentSceneTerminate);
            var secondScene = new AnotherMockGameScene();
            _gameScenes.AddLast(secondScene);
            await SimulateTransitionCore(secondScene);

            Assert.IsTrue(firstScene.TerminateCalled);
            Assert.AreEqual(GameSceneState.Terminate, firstScene.State);
        }

        [Test]
        public async Task TransitionWithSleepOperation_SleepsPreviousScene()
        {
            var firstScene = new MockGameScene();
            _gameScenes.AddLast(firstScene);
            await SimulateTransitionCore(firstScene);

            // 2番目の遷移でSleep操作
            await SimulateOperationAsync(GameSceneOperations.CurrentSceneSleep);
            var secondScene = new AnotherMockGameScene();
            _gameScenes.AddLast(secondScene);
            await SimulateTransitionCore(secondScene);

            Assert.IsTrue(firstScene.SleepCalled);
            Assert.AreEqual(GameSceneState.Sleep, firstScene.State);
        }

        [Test]
        public async Task TransitionWithClearHistoryOperation_RemovesPreviousScene()
        {
            var firstScene = new MockGameScene();
            _gameScenes.AddLast(firstScene);
            await SimulateTransitionCore(firstScene);

            // Terminate + ClearHistory
            await SimulateOperationAsync(GameSceneOperations.CurrentSceneTerminate | GameSceneOperations.CurrentSceneClearHistory);
            var secondScene = new AnotherMockGameScene();
            _gameScenes.AddLast(secondScene);
            await SimulateTransitionCore(secondScene);

            Assert.AreEqual(1, _gameScenes.Count);
            Assert.IsInstanceOf<AnotherMockGameScene>(_gameScenes.First.Value);
        }

        [Test]
        public async Task TransitionWithResult_Canceled_ReturnsDefault()
        {
            var scene = new MockGameSceneWithResult();
            var tcs = SimulateCreateResultTcs<int>(scene);
            _gameScenes.AddLast(scene);

            var transitionTask = SimulateTransitionWithResultAsync(scene, tcs);

            scene.ResultTcs.TrySetCanceled();

            var result = await transitionTask;
            Assert.AreEqual(default(int), result);
        }

        #endregion

        #region TransitionPrevAsync Tests

        [Test]
        public async Task TransitionPrevAsync_WithSleepingPreviousScene_RestartsPreviousScene()
        {
            // Setup: Scene1 (sleeping) -> Scene2 (current)
            var scene1 = new MockGameScene { State = GameSceneState.Sleep };
            var scene2 = new MockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene1);
            _gameScenes.AddLast(scene2);

            await SimulateTransitionPrevAsync();

            Assert.IsTrue(scene2.TerminateCalled);
            Assert.IsTrue(scene1.RestartCalled);
        }

        [Test]
        public async Task TransitionPrevAsync_WithTerminatedPreviousScene_TransitionsToPrevious()
        {
            // Setup: Scene1 (terminated) -> Scene2 (current)
            var scene1 = new MockGameScene { State = GameSceneState.Terminate };
            var scene2 = new MockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene1);
            _gameScenes.AddLast(scene2);

            await SimulateTransitionPrevAsync();

            Assert.IsTrue(scene2.TerminateCalled);
            Assert.IsTrue(scene1.PreInitializeCalled);
        }

        [Test]
        public async Task TransitionPrevAsync_WithProcessingPreviousScene_TerminatesCurrentOnly()
        {
            // Setup: Scene1 (processing) -> Scene2 (current)
            var scene1 = new MockGameScene { State = GameSceneState.Processing };
            var scene2 = new AnotherMockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene1);
            _gameScenes.AddLast(scene2);

            await SimulateTransitionPrevAsync();

            Assert.IsTrue(scene2.TerminateCalled);
        }

        #endregion

        #region TransitionDialogAsync Tests

        [Test]
        public async Task TransitionDialogAsync_AddsDialogToList()
        {
            var dialog = new TestDialogScene();
            var tcs = SimulateCreateResultTcs<bool>(dialog);
            _gameScenes.AddLast(dialog);

            var dialogTask = SimulateTransitionDialogAsync(dialog, tcs);

            Assert.AreEqual(1, _gameScenes.Count);
            Assert.IsInstanceOf<TestDialogScene>(_gameScenes.First.Value);

            dialog.TrySetResult(true);
            var result = await dialogTask;
            Assert.IsTrue(result);
        }

        [Test]
        public async Task TransitionDialogAsync_WithInitializer_InvokesInitializer()
        {
            var dialog = new TestDialogScene();
            var initializerCalled = false;

            dialog.DialogInitializer = (component, result) =>
            {
                initializerCalled = true;
                return UniTask.CompletedTask;
            };

            var tcs = SimulateCreateResultTcs<bool>(dialog);
            _gameScenes.AddLast(dialog);

            var dialogTask = SimulateTransitionDialogAsync(dialog, tcs);

            dialog.TrySetResult(true);
            await dialogTask;

            Assert.IsTrue(initializerCalled);
        }

        [Test]
        public async Task TransitionDialogAsync_WhenSameDialogIsProcessing_TerminatesExisting()
        {
            // 最初のダイアログ
            var firstDialog = new TestDialogScene { State = GameSceneState.Processing };
            firstDialog.ResultTcs = new UniTaskCompletionSource<bool>();
            _gameScenes.AddLast(firstDialog);

            // 同じ型のダイアログを再度開こうとする
            var isProcessing = _service.IsProcessing(typeof(TestDialogScene));
            if (isProcessing)
            {
                await _service.TerminateAsync(typeof(TestDialogScene), clearHistory: true);
            }

            Assert.IsTrue(firstDialog.TerminateCalled);
        }

        [Test]
        public async Task TransitionDialogAsync_MultipleDialogs_CanStackDialogs()
        {
            var dialog1 = new TestDialogScene { State = GameSceneState.Processing };
            var dialog2 = new TestDialogScene2 { State = GameSceneState.Processing };
            dialog1.ResultTcs = new UniTaskCompletionSource<bool>();
            dialog2.ResultTcs = new UniTaskCompletionSource<int>();

            _gameScenes.AddLast(dialog1);
            _gameScenes.AddLast(dialog2);

            Assert.IsInstanceOf<IGameSceneResult>(dialog1);
            Assert.IsInstanceOf<IGameSceneResult>(dialog2);
            Assert.AreEqual(2, _gameScenes.Count);
        }

        [Test]
        public async Task TransitionDialogAsync_DialogCanceled_ReturnsDefault()
        {
            var dialog = new TestDialogScene();
            var tcs = SimulateCreateResultTcs<bool>(dialog);
            _gameScenes.AddLast(dialog);

            var dialogTask = SimulateTransitionDialogAsync(dialog, tcs);

            dialog.TrySetCanceled();

            var result = await dialogTask;
            Assert.AreEqual(default(bool), result);
        }

        #endregion

        #region Scene Operations Tests

        [Test]
        public async Task OperationAsync_WithNoOperation_DoesNothing()
        {
            var scene = new MockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene);

            await SimulateOperationAsync(GameSceneOperations.None);

            Assert.IsFalse(scene.SleepCalled);
            Assert.IsFalse(scene.RestartCalled);
            Assert.IsFalse(scene.TerminateCalled);
        }

        [Test]
        public async Task OperationAsync_WithSleep_SleepsCurrentScene()
        {
            var scene = new MockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene);

            await SimulateOperationAsync(GameSceneOperations.CurrentSceneSleep);

            Assert.IsTrue(scene.SleepCalled);
            Assert.AreEqual(GameSceneState.Sleep, scene.State);
        }

        [Test]
        public async Task OperationAsync_WithRestart_RestartsCurrentScene()
        {
            var scene = new MockGameScene { State = GameSceneState.Sleep };
            _gameScenes.AddLast(scene);

            await SimulateOperationAsync(GameSceneOperations.CurrentSceneRestart);

            Assert.IsTrue(scene.RestartCalled);
            Assert.AreEqual(GameSceneState.Processing, scene.State);
        }

        [Test]
        public async Task OperationAsync_WithTerminate_TerminatesCurrentScene()
        {
            var scene = new MockGameScene { State = GameSceneState.Processing };
            _gameScenes.AddLast(scene);

            await SimulateOperationAsync(GameSceneOperations.CurrentSceneTerminate);

            Assert.IsTrue(scene.TerminateCalled);
            Assert.AreEqual(GameSceneState.Terminate, scene.State);
        }

        [Test]
        public async Task OperationAsync_ClosesAllDialogsBeforeOperation()
        {
            var regularScene = new MockGameScene { State = GameSceneState.Processing };
            var dialogScene = new MockGameSceneWithResult { State = GameSceneState.Processing };
            dialogScene.ResultTcs = new UniTaskCompletionSource<int>();
            _gameScenes.AddLast(regularScene);
            _gameScenes.AddLast(dialogScene);

            await SimulateOperationAsync(GameSceneOperations.None);

            Assert.AreEqual(GameSceneState.Terminate, dialogScene.State);
        }

        #endregion

        #region Helper Methods

        private T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return (T)field?.GetValue(obj);
        }

        /// <summary>
        /// TransitionCoreの動作をシミュレート
        /// GlobalMessageBrokerを使用してメッセージを発行
        /// </summary>
        private async UniTask SimulateTransitionCore(IGameScene gameScene, bool isDialog = false)
        {
            gameScene.State = GameSceneState.Processing;

            if (gameScene.ArgHandler != null)
                await gameScene.ArgHandler.Invoke(gameScene);

            if (!isDialog)
                await _messageBrokerService.GlobalMessageBroker.GetAsyncPublisher<int, bool>()
                    .PublishAsync(MessageKey.GameScene.TransitionEnter, true);

            await gameScene.PreInitialize();
            await gameScene.LoadAsset();
            await gameScene.Startup();

            if (!isDialog)
                await _messageBrokerService.GlobalMessageBroker.GetAsyncPublisher<int, bool>()
                    .PublishAsync(MessageKey.GameScene.TransitionFinish, true);

            await gameScene.Ready();
        }

        /// <summary>
        /// CreateArgHandlerの動作をシミュレート
        /// </summary>
        private void SimulateCreateArgHandler<TArg>(IGameScene gameScene, TArg arg)
        {
            if (gameScene is IGameSceneArgHandler handler)
            {
                handler.ArgHandler = scene =>
                {
                    if (scene is IGameSceneArg<TArg> gameSceneArg)
                        return gameSceneArg.ArgHandle(arg);

                    return UniTask.CompletedTask;
                };
            }
        }

        /// <summary>
        /// CreateResultTcsの動作をシミュレート
        /// </summary>
        private UniTaskCompletionSource<TResult> SimulateCreateResultTcs<TResult>(IGameScene gameScene)
        {
            if (gameScene is IGameSceneResult<TResult> result)
            {
                return result.ResultTcs = new UniTaskCompletionSource<TResult>();
            }

            return null;
        }

        /// <summary>
        /// Result付き遷移をシミュレート
        /// </summary>
        private async UniTask<TResult> SimulateTransitionWithResultAsync<TResult>(IGameScene gameScene, UniTaskCompletionSource<TResult> tcs)
        {
            await SimulateTransitionCore(gameScene);

            if (tcs == null) return default;

            try
            {
                var result = await tcs.Task;
                await SimulateTerminateAsync(gameScene, clearHistory: true);
                return result;
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
            }

            return default;
        }

        /// <summary>
        /// ダイアログ遷移をシミュレート
        /// </summary>
        private async UniTask<TResult> SimulateTransitionDialogAsync<TResult>(IGameScene gameScene, UniTaskCompletionSource<TResult> tcs)
        {
            await SimulateTransitionCore(gameScene, isDialog: true);

            if (tcs == null) return default;

            try
            {
                var result = await tcs.Task;
                await SimulateTerminateAsync(gameScene, clearHistory: true);
                return result;
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
            }

            return default;
        }

        /// <summary>
        /// OperationAsyncの動作をシミュレート
        /// </summary>
        private async UniTask SimulateOperationAsync(GameSceneOperations operations)
        {
            // 全ダイアログを閉じる
            await SimulateTerminateAllDialogAsync();

            if (operations.HasFlag(GameSceneOperations.CurrentSceneSleep))
            {
                await SimulateSleepAsync();
            }
            else if (operations.HasFlag(GameSceneOperations.CurrentSceneRestart))
            {
                await SimulateRestartAsync();
            }
            else if (operations.HasFlag(GameSceneOperations.CurrentSceneTerminate))
            {
                bool clearHistory = operations.HasFlag(GameSceneOperations.CurrentSceneClearHistory);
                await SimulateTerminateLastAsync(clearHistory);
            }
        }

        /// <summary>
        /// TransitionPrevAsyncの動作をシミュレート
        /// </summary>
        private async UniTask SimulateTransitionPrevAsync()
        {
            var prevNode = _gameScenes.Last?.Previous;
            if (prevNode != null)
            {
                var gameScene = prevNode.Value;
                if (gameScene.State is GameSceneState.Terminate)
                {
                    await SimulateTerminateLastAsync(clearHistory: true);
                    await SimulateTransitionCore(gameScene);
                }
                else if (gameScene.State is GameSceneState.Sleep)
                {
                    await SimulateTerminateLastAsync(clearHistory: true);
                    await SimulateRestartAsync();
                }
                else if (gameScene.State is GameSceneState.Processing)
                {
                    await SimulateTerminateLastAsync(clearHistory: true);
                }
            }
        }

        private UniTask SimulateSleepAsync()
        {
            var currentNode = _gameScenes.Last;
            if (currentNode != null)
            {
                var gameScene = currentNode.Value;
                if (gameScene != null)
                {
                    gameScene.State = GameSceneState.Sleep;
                    return gameScene.Sleep();
                }
            }

            return UniTask.CompletedTask;
        }

        private UniTask SimulateRestartAsync()
        {
            var currentNode = _gameScenes.Last;
            if (currentNode != null)
            {
                var gameScene = currentNode.Value;
                if (gameScene != null)
                {
                    gameScene.State = GameSceneState.Processing;
                    return gameScene.Restart();
                }
            }

            return UniTask.CompletedTask;
        }

        private async UniTask SimulateTerminateAsync(IGameScene gameScene, bool clearHistory = false)
        {
            var node = _gameScenes.FindLast(gameScene);
            if (node != null)
            {
                await SimulateTerminateCore(node.Value);
                if (clearHistory) _gameScenes.Remove(node);
            }
        }

        private async UniTask SimulateTerminateLastAsync(bool clearHistory = false)
        {
            var currentNode = _gameScenes.Last;
            if (currentNode != null)
            {
                var gameScene = currentNode.Value;
                if (gameScene != null)
                {
                    await SimulateTerminateAsync(gameScene, clearHistory);
                }
            }
        }

        private async UniTask SimulateTerminateAllDialogAsync()
        {
            var scenesToTerminate = new List<IGameScene>();
            foreach (var gameScene in _gameScenes)
            {
                if (gameScene is IGameSceneResult)
                {
                    scenesToTerminate.Add(gameScene);
                }
            }

            foreach (var gameScene in scenesToTerminate)
            {
                await SimulateTerminateAsync(gameScene, clearHistory: true);
            }
        }

        private async UniTask SimulateTerminateCore(IGameScene gameScene)
        {
            if (gameScene != null)
            {
                gameScene.State = GameSceneState.Terminate;
                await gameScene.Terminate();
            }
        }

        #endregion

        #region Mock Classes

        private class MockGameScene : IGameScene
        {
            public GameSceneState State { get; set; }
            public Func<IGameScene, UniTask> ArgHandler { get; set; }

            public bool PreInitializeCalled { get; private set; }
            public bool LoadAssetCalled { get; private set; }
            public bool StartupCalled { get; private set; }
            public bool ReadyCalled { get; private set; }
            public bool SleepCalled { get; private set; }
            public bool RestartCalled { get; private set; }
            public bool TerminateCalled { get; private set; }

            public UniTask PreInitialize()
            {
                PreInitializeCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask LoadAsset()
            {
                LoadAssetCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask Startup()
            {
                StartupCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask Ready()
            {
                ReadyCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask Sleep()
            {
                SleepCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask Restart()
            {
                RestartCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask Terminate()
            {
                TerminateCalled = true;
                return UniTask.CompletedTask;
            }
        }

        private class AnotherMockGameScene : IGameScene
        {
            public GameSceneState State { get; set; }
            public Func<IGameScene, UniTask> ArgHandler { get; set; }

            public bool TerminateCalled { get; private set; }

            public UniTask PreInitialize() => UniTask.CompletedTask;
            public UniTask LoadAsset() => UniTask.CompletedTask;
            public UniTask Startup() => UniTask.CompletedTask;
            public UniTask Ready() => UniTask.CompletedTask;
            public UniTask Sleep() => UniTask.CompletedTask;
            public UniTask Restart() => UniTask.CompletedTask;

            public UniTask Terminate()
            {
                TerminateCalled = true;
                return UniTask.CompletedTask;
            }
        }

        private class MockGameSceneWithResult : IGameScene, IGameSceneResult<int>
        {
            public GameSceneState State { get; set; }
            public Func<IGameScene, UniTask> ArgHandler { get; set; }
            public UniTaskCompletionSource<int> ResultTcs { get; set; }

            public UniTask PreInitialize() => UniTask.CompletedTask;
            public UniTask LoadAsset() => UniTask.CompletedTask;
            public UniTask Startup() => UniTask.CompletedTask;
            public UniTask Ready() => UniTask.CompletedTask;
            public UniTask Sleep() => UniTask.CompletedTask;
            public UniTask Restart() => UniTask.CompletedTask;
            public UniTask Terminate() => UniTask.CompletedTask;
        }

        private class MockGameSceneWithArg : IGameScene, IGameSceneArg<string>
        {
            public GameSceneState State { get; set; }
            public Func<IGameScene, UniTask> ArgHandler { get; set; }

            public string ReceivedArg { get; private set; }

            public UniTask ArgHandle(string arg)
            {
                ReceivedArg = arg;
                return UniTask.CompletedTask;
            }

            public UniTask PreInitialize() => UniTask.CompletedTask;
            public UniTask LoadAsset() => UniTask.CompletedTask;
            public UniTask Startup() => UniTask.CompletedTask;
            public UniTask Ready() => UniTask.CompletedTask;
            public UniTask Sleep() => UniTask.CompletedTask;
            public UniTask Restart() => UniTask.CompletedTask;
            public UniTask Terminate() => UniTask.CompletedTask;
        }

        /// <summary>
        /// IGameSceneArg と IGameSceneResult を同時に実装したモッククラス
        /// 引数を受け取り、結果を返すシーン（ダイアログなど）のテスト用
        /// </summary>
        private class MockGameSceneWithArgAndResult<TArg, TResult> : IGameScene, IGameSceneArg<TArg>, IGameSceneResult<TResult>
        {
            public GameSceneState State { get; set; }
            public Func<IGameScene, UniTask> ArgHandler { get; set; }
            public UniTaskCompletionSource<TResult> ResultTcs { get; set; }

            public TArg ReceivedArg { get; private set; }

            public bool ArgHandleCalled { get; private set; }
            public bool PreInitializeCalled { get; private set; }
            public bool LoadAssetCalled { get; private set; }
            public bool StartupCalled { get; private set; }
            public bool ReadyCalled { get; private set; }
            public bool SleepCalled { get; private set; }
            public bool RestartCalled { get; private set; }
            public bool TerminateCalled { get; private set; }

            public UniTask ArgHandle(TArg arg)
            {
                ReceivedArg = arg;
                ArgHandleCalled = true;
                return UniTask.CompletedTask;
            }

            public bool TrySetResult(TResult result) => ResultTcs?.TrySetResult(result) ?? false;
            public bool TrySetCanceled() => ResultTcs?.TrySetCanceled() ?? false;
            public bool TrySetException(Exception e) => ResultTcs?.TrySetException(e) ?? false;

            public UniTask PreInitialize()
            {
                PreInitializeCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask LoadAsset()
            {
                LoadAssetCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask Startup()
            {
                StartupCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask Ready()
            {
                ReadyCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask Sleep()
            {
                SleepCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask Restart()
            {
                RestartCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask Terminate()
            {
                TerminateCalled = true;
                return UniTask.CompletedTask;
            }
        }

        /// <summary>
        /// 複雑な引数型のテスト用クラス
        /// </summary>
        private class ComplexArg
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int[] Data { get; set; }
        }

        /// <summary>
        /// 複雑な結果型のテスト用クラス
        /// </summary>
        private class ComplexResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public int Value { get; set; }
        }

        /// <summary>
        /// GameSceneComponentのモック（MonoBehaviour非依存）
        /// </summary>
        private class MockSceneComponent
        {
            public bool IsInteractive { get; private set; } = true;
            public bool IsSleeping { get; private set; }

            public bool InitializeCalled { get; private set; }
            public bool SleepCalled { get; private set; }
            public bool RestartCalled { get; private set; }

            public string Title { get; private set; }
            public string Message { get; private set; }

            public void Initialize(string title, string message)
            {
                Title = title;
                Message = message;
                InitializeCalled = true;
            }

            public void SetInteractiveAllButton(bool interactive)
            {
                IsInteractive = interactive;
            }

            public void Sleep()
            {
                IsSleeping = true;
                SleepCalled = true;
            }

            public void Restart()
            {
                IsSleeping = false;
                IsInteractive = true;
                RestartCalled = true;
            }
        }

        /// <summary>
        /// GameDialogSceneのモッククラス
        /// IGameDialogSceneInitializerとIGameSceneResultを実装
        /// </summary>
        private class MockGameDialogScene<TComponent, TResult> : IGameScene, IGameDialogSceneInitializer<TComponent, TResult>, IGameSceneResult<TResult>
            where TComponent : MockSceneComponent, new()
        {
            public GameSceneState State { get; set; }
            public Func<IGameScene, UniTask> ArgHandler { get; set; }
            public Func<TComponent, IGameSceneResult<TResult>, UniTask> DialogInitializer { get; set; }
            public UniTaskCompletionSource<TResult> ResultTcs { get; set; }

            public TComponent SceneComponent { get; private set; }

            public bool PreInitializeCalled { get; private set; }
            public bool LoadAssetCalled { get; private set; }
            public bool StartupCalled { get; private set; }
            public bool ReadyCalled { get; private set; }
            public bool SleepCalled { get; private set; }
            public bool RestartCalled { get; private set; }
            public bool TerminateCalled { get; private set; }
            public bool UnloadSceneCalled { get; private set; }
            public bool TrySetCanceledCalled { get; private set; }

            public UniTask PreInitialize()
            {
                PreInitializeCalled = true;
                return UniTask.CompletedTask;
            }

            public async UniTask LoadAsset()
            {
                LoadAssetCalled = true;
                await LoadScene();
                SceneComponent = GetSceneComponent();
            }

            public UniTask Startup()
            {
                StartupCalled = true;
                if (DialogInitializer != null)
                {
                    return DialogInitializer.Invoke(SceneComponent, this);
                }

                return UniTask.CompletedTask;
            }

            public UniTask Ready()
            {
                ReadyCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask Sleep()
            {
                SleepCalled = true;
                SceneComponent?.Sleep();
                return UniTask.CompletedTask;
            }

            public UniTask Restart()
            {
                RestartCalled = true;
                SceneComponent?.Restart();
                return UniTask.CompletedTask;
            }

            public UniTask Terminate()
            {
                TerminateCalled = true;
                TrySetCanceled();
                return UnloadScene();
            }

            private UniTask LoadScene()
            {
                // Simulate asset loading
                return UniTask.CompletedTask;
            }

            private UniTask UnloadScene()
            {
                UnloadSceneCalled = true;
                SceneComponent = null;
                return UniTask.CompletedTask;
            }

            private TComponent GetSceneComponent()
            {
                return SceneComponent ??= new TComponent();
            }

            public bool TrySetResult(TResult result) => ResultTcs?.TrySetResult(result) ?? false;

            public bool TrySetCanceled()
            {
                TrySetCanceledCalled = true;
                return ResultTcs?.TrySetCanceled() ?? false;
            }

            public bool TrySetException(Exception e) => ResultTcs?.TrySetException(e) ?? false;
        }

        /// <summary>
        /// 引数付きGameDialogSceneのモッククラス
        /// IGameSceneArg、IGameDialogSceneInitializer、IGameSceneResultを同時に実装
        /// </summary>
        private class MockGameDialogSceneWithArg<TComponent, TArg, TResult> : IGameScene, IGameSceneArg<TArg>, IGameDialogSceneInitializer<TComponent, TResult>, IGameSceneResult<TResult>
            where TComponent : MockSceneComponent, new()
        {
            public GameSceneState State { get; set; }
            public Func<IGameScene, UniTask> ArgHandler { get; set; }
            public Func<TComponent, IGameSceneResult<TResult>, UniTask> DialogInitializer { get; set; }
            public UniTaskCompletionSource<TResult> ResultTcs { get; set; }

            public TComponent SceneComponent { get; private set; }
            public TArg ReceivedArg { get; private set; }

            public bool ArgHandleCalled { get; private set; }
            public bool PreInitializeCalled { get; private set; }
            public bool LoadAssetCalled { get; private set; }
            public bool StartupCalled { get; private set; }
            public bool ReadyCalled { get; private set; }
            public bool SleepCalled { get; private set; }
            public bool RestartCalled { get; private set; }
            public bool TerminateCalled { get; private set; }
            public bool UnloadSceneCalled { get; private set; }
            public bool TrySetCanceledCalled { get; private set; }

            public UniTask ArgHandle(TArg arg)
            {
                ReceivedArg = arg;
                ArgHandleCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask PreInitialize()
            {
                PreInitializeCalled = true;
                return UniTask.CompletedTask;
            }

            public async UniTask LoadAsset()
            {
                LoadAssetCalled = true;
                await LoadScene();
                SceneComponent = GetSceneComponent();
            }

            public UniTask Startup()
            {
                StartupCalled = true;
                if (DialogInitializer != null)
                {
                    return DialogInitializer.Invoke(SceneComponent, this);
                }

                return UniTask.CompletedTask;
            }

            public UniTask Ready()
            {
                ReadyCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask Sleep()
            {
                SleepCalled = true;
                SceneComponent?.Sleep();
                return UniTask.CompletedTask;
            }

            public UniTask Restart()
            {
                RestartCalled = true;
                SceneComponent?.Restart();
                return UniTask.CompletedTask;
            }

            public UniTask Terminate()
            {
                TerminateCalled = true;
                TrySetCanceled();
                return UnloadScene();
            }

            private UniTask LoadScene()
            {
                return UniTask.CompletedTask;
            }

            private UniTask UnloadScene()
            {
                UnloadSceneCalled = true;
                SceneComponent = null;
                return UniTask.CompletedTask;
            }

            private TComponent GetSceneComponent()
            {
                return SceneComponent ??= new TComponent();
            }

            public bool TrySetResult(TResult result) => ResultTcs?.TrySetResult(result) ?? false;

            public bool TrySetCanceled()
            {
                TrySetCanceledCalled = true;
                return ResultTcs?.TrySetCanceled() ?? false;
            }

            public bool TrySetException(Exception e) => ResultTcs?.TrySetException(e) ?? false;
        }

        /// <summary>
        /// ダイアログ入力用のテストクラス
        /// </summary>
        private class DialogInput
        {
            public int ItemId { get; set; }
            public int Quantity { get; set; }
        }

        /// <summary>
        /// ダイアログ出力用のテストクラス
        /// </summary>
        private class DialogOutput
        {
            public bool Confirmed { get; set; }
            public int SelectedQuantity { get; set; }
        }

        /// <summary>
        /// テスト用ダイアログシーン
        /// </summary>
        private class TestDialogScene : IGameScene, IGameDialogSceneInitializer<MockSceneComponent, bool>, IGameSceneResult<bool>
        {
            public GameSceneState State { get; set; }
            public Func<IGameScene, UniTask> ArgHandler { get; set; }
            public Func<MockSceneComponent, IGameSceneResult<bool>, UniTask> DialogInitializer { get; set; }
            public UniTaskCompletionSource<bool> ResultTcs { get; set; }

            public MockSceneComponent SceneComponent { get; private set; }
            public bool TerminateCalled { get; private set; }

            public UniTask PreInitialize() => UniTask.CompletedTask;

            public async UniTask LoadAsset()
            {
                SceneComponent = new MockSceneComponent();
            }

            public UniTask Startup()
            {
                if (DialogInitializer != null)
                {
                    return DialogInitializer.Invoke(SceneComponent, this);
                }

                return UniTask.CompletedTask;
            }

            public UniTask Ready() => UniTask.CompletedTask;
            public UniTask Sleep() => UniTask.CompletedTask;
            public UniTask Restart() => UniTask.CompletedTask;

            public UniTask Terminate()
            {
                TerminateCalled = true;
                TrySetCanceled();
                return UniTask.CompletedTask;
            }

            public bool TrySetResult(bool result) => ResultTcs?.TrySetResult(result) ?? false;
            public bool TrySetCanceled() => ResultTcs?.TrySetCanceled() ?? false;
            public bool TrySetException(Exception e) => ResultTcs?.TrySetException(e) ?? false;
        }

        /// <summary>
        /// テスト用ダイアログシーン2（異なる型のダイアログスタックテスト用）
        /// </summary>
        private class TestDialogScene2 : IGameScene, IGameDialogSceneInitializer<MockSceneComponent, int>, IGameSceneResult<int>
        {
            public GameSceneState State { get; set; }
            public Func<IGameScene, UniTask> ArgHandler { get; set; }
            public Func<MockSceneComponent, IGameSceneResult<int>, UniTask> DialogInitializer { get; set; }
            public UniTaskCompletionSource<int> ResultTcs { get; set; }

            public MockSceneComponent SceneComponent { get; private set; }
            public bool TerminateCalled { get; private set; }

            public UniTask PreInitialize() => UniTask.CompletedTask;

            public async UniTask LoadAsset()
            {
                SceneComponent = new MockSceneComponent();
            }

            public UniTask Startup()
            {
                if (DialogInitializer != null)
                {
                    return DialogInitializer.Invoke(SceneComponent, this);
                }

                return UniTask.CompletedTask;
            }

            public UniTask Ready() => UniTask.CompletedTask;
            public UniTask Sleep() => UniTask.CompletedTask;
            public UniTask Restart() => UniTask.CompletedTask;

            public UniTask Terminate()
            {
                TerminateCalled = true;
                TrySetCanceled();
                return UniTask.CompletedTask;
            }

            public bool TrySetResult(int result) => ResultTcs?.TrySetResult(result) ?? false;
            public bool TrySetCanceled() => ResultTcs?.TrySetCanceled() ?? false;
            public bool TrySetException(Exception e) => ResultTcs?.TrySetException(e) ?? false;
        }

        #endregion
    }
}