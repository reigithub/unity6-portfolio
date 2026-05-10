using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Client.MasterData;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Scenes;
using Game.MVP.Survivor.Server;
using Game.Shared.Network.Fusion;
using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using Game.Shared.Unity.Server;
using MasterMemory;
using MessagePack;
using MessagePack.Resolvers;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace Game.Tests.MVP.Survivor.Server
{
    /// <summary>
    /// <see cref="SurvivorServerGameLoop"/> のユニットテスト。
    /// セッション例外境界・クリーンアップ保証・事前バリデーションを検証する。
    /// </summary>
    [TestFixture]
    public class SurvivorServerGameLoopTests
    {
        // ---------------------------------------------------------------
        // モックフィールド
        // ---------------------------------------------------------------

        private IGameSceneService _sceneService;
        private ISurvivorSaveService _saveService;
        private IMasterDataService _masterDataService;
        private IFusionRunnerService _runnerService;
        private ISurvivorNetworkStageConnector _networkConnector;
        private IGameSessionConfig _sessionConfig;
        private IUnityServerHttpListener _listener;
        private IUnityServerRegistryApiClient _registry;
        private UnityServerBootstrap _bootstrap;
        private ISubscriber<SurvivorSignals.Session.AllPlayersReady> _allPlayersReadySub;
        private ISubscriber<SurvivorSignals.Session.AllPlayersDisconnected> _allPlayersDisconnectedSub;

        // ---------------------------------------------------------------
        // セットアップ
        // ---------------------------------------------------------------

        [SetUp]
        public void SetUp()
        {
            _sceneService = Substitute.For<IGameSceneService>();
            _saveService = Substitute.For<ISurvivorSaveService>();
            _masterDataService = Substitute.For<IMasterDataService>();
            _runnerService = Substitute.For<IFusionRunnerService>();
            _networkConnector = Substitute.For<ISurvivorNetworkStageConnector>();
            _sessionConfig = Substitute.For<IGameSessionConfig>();
            _listener = Substitute.For<IUnityServerHttpListener>();
            _registry = Substitute.For<IUnityServerRegistryApiClient>();
            _bootstrap = CreateBootstrapMock();
            _allPlayersReadySub = Substitute.For<ISubscriber<SurvivorSignals.Session.AllPlayersReady>>();
            _allPlayersDisconnectedSub = Substitute.For<ISubscriber<SurvivorSignals.Session.AllPlayersDisconnected>>();

            // デフォルトの戻り値設定
            _masterDataService.LoadMasterDataAsync().Returns(UniTask.CompletedTask);
            _networkConnector.StartServerAsync(Arg.Any<int>()).Returns(UniTask.CompletedTask);
            _networkConnector.DisconnectAsync().Returns(UniTask.CompletedTask);
            _sceneService.TransitionAsync<SurvivorNetworkStageScene>().Returns(UniTask.CompletedTask);
            _registry.NotifySessionEndedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(true));

            // Subscribe はデフォルトで何も発火しない（テストごとにオーバーライド）
            // ISubscriber<T>.Subscribe(IMessageHandler<T>, params MessageHandlerFilter<T>[]) が virtual なのでこれを match する
            _allPlayersReadySub.Subscribe(Arg.Any<IMessageHandler<SurvivorSignals.Session.AllPlayersReady>>())
                .Returns(Substitute.For<IDisposable>());
            _allPlayersDisconnectedSub.Subscribe(Arg.Any<IMessageHandler<SurvivorSignals.Session.AllPlayersDisconnected>>())
                .Returns(Substitute.For<IDisposable>());
        }

        // ---------------------------------------------------------------
        // テストケース
        // ---------------------------------------------------------------

        /// <summary>
        /// 不明な stageId（マスターデータに存在しない）を受信した場合、
        /// StartServerAsync を呼ばずに CompletionSource を false で完了させてループを継続する。
        /// </summary>
        [Test]
        public async Task UnknownStageId_RejectsAndContinues()
        {
            // Arrange
            // stageId=1 のみが有効なマスターデータを構築し、9001 は存在しない
            var memoryDb = BuildMemoryDatabase(validStageId: 1);
            _masterDataService.MemoryDatabase.Returns(memoryDb);

            // 1 回目: 不正 stageId=9001 → 拒否
            // 2 回目: キャンセル → ループ終了
            var request1 = CreateSessionRequest(sessionName: "match-1", stageId: 9001);
            using var cts = new CancellationTokenSource();
            int callCount = 0;
            _listener.TryDequeueSessionRequest(out Arg.Any<UnityServerSessionRequest>()).Returns(ci =>
            {
                callCount++;
                if (callCount == 1)
                {
                    ci[0] = request1;
                    return true;
                }

                // 2 回目でキャンセルしてループを終了させる
                cts.Cancel();
                return false;
            });

            var loop = CreateLoop();

            // Act
            try
            {
                await loop.StartAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 正常終了
            }

            // Assert: Fusion セッションは作られていない
            await _networkConnector.DidNotReceive().StartServerAsync(Arg.Any<int>());

            // CompletionSource は false で完了している
            var result = await request1.CompletionSource.Task;
            Assert.That(result, Is.False, "不正 stageId は false で拒否されるべき");
        }

        /// <summary>
        /// StartServerAsync が例外をスローした場合、
        /// CompletionSource が false で完了し、DisconnectAsync と SetSessionIdle が呼ばれ、
        /// NotifySessionEnded は呼ばれず、ループは継続する。
        /// </summary>
        [Test]
        public async Task StartServerAsyncThrows_CleanupAndContinues()
        {
            // Arrange
            // Session aborted の Debug.LogError を想定内エラーとして宣言
            LogAssert.Expect(LogType.Error, new Regex(@"\[SurvivorServerGameLoop\] Session aborted.*Fusion 起動失敗"));

            var memoryDb = BuildMemoryDatabase(validStageId: 1);
            _masterDataService.MemoryDatabase.Returns(memoryDb);

            var request1 = CreateSessionRequest(sessionName: "match-1", stageId: 1);
            _networkConnector.StartServerAsync(Arg.Any<int>())
                .Returns(UniTask.FromException(new InvalidOperationException("Fusion 起動失敗")));

            using var cts = new CancellationTokenSource();
            int dequeueCount = 0;
            _listener.TryDequeueSessionRequest(out Arg.Any<UnityServerSessionRequest>()).Returns(ci =>
            {
                dequeueCount++;
                if (dequeueCount == 1)
                {
                    ci[0] = request1;
                    return true;
                }

                cts.Cancel();
                return false;
            });

            var loop = CreateLoop();

            // Act
            try
            {
                await loop.StartAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 正常終了
            }

            // Assert: CompletionSource は false
            var result = await request1.CompletionSource.Task;
            Assert.That(result, Is.False, "StartServerAsync 失敗時は false で応答すべき");

            // クリーンアップが呼ばれている
            await _networkConnector.Received(1).DisconnectAsync();
            _listener.Received(1).SetSessionIdle();

            // AcceptedByServer=false なので NotifySessionEnded は呼ばれない
            await _registry.DidNotReceive().NotifySessionEndedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// TransitionAsync（シーン遷移）が例外をスローした場合（Step 4 以降の失敗）、
        /// CompletionSource はすでに true で完了しており、
        /// DisconnectAsync・SetSessionIdle・NotifySessionEnded が全て呼ばれてループが継続する。
        /// </summary>
        [Test]
        public async Task SceneTransitionThrows_NotifiesAndContinues()
        {
            // Arrange
            // Session aborted の Debug.LogError を想定内エラーとして宣言
            LogAssert.Expect(LogType.Error, new Regex(@"\[SurvivorServerGameLoop\] Session aborted.*Stage master not found: 9001"));

            var memoryDb = BuildMemoryDatabase(validStageId: 1);
            _masterDataService.MemoryDatabase.Returns(memoryDb);

            var request1 = CreateSessionRequest(sessionName: "match-1", stageId: 1);

            // AllPlayersReady を即座に発火するように Subscribe を設定
            _allPlayersReadySub.Subscribe(Arg.Any<IMessageHandler<SurvivorSignals.Session.AllPlayersReady>>())
                .Returns(ci =>
                {
                    // Subscribe した直後にハンドラを呼び出す（即座に Ready 状態）
                    var handler = ci.Arg<IMessageHandler<SurvivorSignals.Session.AllPlayersReady>>();
                    handler.Handle(new SurvivorSignals.Session.AllPlayersReady());
                    return Substitute.For<IDisposable>();
                });

            // TransitionAsync がシーン遷移例外をスローする
            _sceneService.TransitionAsync<SurvivorNetworkStageScene>()
                .Returns(UniTask.FromException(new InvalidOperationException("Stage master not found: 9001")));

            _registry.NotifySessionEndedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(true));

            using var cts = new CancellationTokenSource();
            int dequeueCount = 0;
            _listener.TryDequeueSessionRequest(out Arg.Any<UnityServerSessionRequest>()).Returns(ci =>
            {
                dequeueCount++;
                if (dequeueCount == 1)
                {
                    ci[0] = request1;
                    return true;
                }

                cts.Cancel();
                return false;
            });

            var loop = CreateLoop();

            // Act
            try
            {
                await loop.StartAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 正常終了
            }

            // Assert: CompletionSource は true（Step 4 は成功済み）
            var result = await request1.CompletionSource.Task;
            Assert.That(result, Is.True, "StartServerAsync 成功後の例外では CompletionSource は true のまま");

            // クリーンアップが全て呼ばれている
            await _networkConnector.Received(1).DisconnectAsync();
            _listener.Received(1).SetSessionIdle();
            await _registry.Received(1).NotifySessionEndedAsync("match-1", CancellationToken.None);
        }

        /// <summary>
        /// NotifySessionEndedAsync が例外をスローした場合、
        /// 例外が握り潰されてループが継続する。
        /// </summary>
        [Test]
        public async Task NotifySessionEndedThrows_SwallowedAndContinues()
        {
            // Arrange
            // Cleanup の Debug.LogError を想定内エラーとして宣言（catch 内で握り潰される想定）
            LogAssert.Expect(LogType.Error, new Regex(@"\[Cleanup\] NotifySessionEndedAsync failed.*Game\.Server 接続失敗"));

            var memoryDb = BuildMemoryDatabase(validStageId: 1);
            _masterDataService.MemoryDatabase.Returns(memoryDb);

            var request1 = CreateSessionRequest(sessionName: "match-1", stageId: 1);

            // AllPlayersReady を即座に発火
            _allPlayersReadySub.Subscribe(Arg.Any<IMessageHandler<SurvivorSignals.Session.AllPlayersReady>>())
                .Returns(ci =>
                {
                    var handler = ci.Arg<IMessageHandler<SurvivorSignals.Session.AllPlayersReady>>();
                    handler.Handle(new SurvivorSignals.Session.AllPlayersReady());
                    return Substitute.For<IDisposable>();
                });

            // AllPlayersDisconnected を即座に発火
            _allPlayersDisconnectedSub.Subscribe(Arg.Any<IMessageHandler<SurvivorSignals.Session.AllPlayersDisconnected>>())
                .Returns(ci =>
                {
                    var handler = ci.Arg<IMessageHandler<SurvivorSignals.Session.AllPlayersDisconnected>>();
                    handler.Handle(new SurvivorSignals.Session.AllPlayersDisconnected());
                    return Substitute.For<IDisposable>();
                });

            // NotifySessionEnded が HTTP 例外をスローする（await 時に例外が発生する Task を返す）
            _registry.NotifySessionEndedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<bool>(new Exception("Game.Server 接続失敗")));

            using var cts = new CancellationTokenSource();
            int dequeueCount = 0;
            _listener.TryDequeueSessionRequest(out Arg.Any<UnityServerSessionRequest>()).Returns(ci =>
            {
                dequeueCount++;
                if (dequeueCount == 1)
                {
                    ci[0] = request1;
                    return true;
                }

                cts.Cancel();
                return false;
            });

            var loop = CreateLoop();

            // Act: 例外が飛ばずに正常終了（OperationCanceledException のみ）
            Exception caughtException = null;
            try
            {
                await loop.StartAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 正常終了
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // Assert: NotifySessionEnded の例外はループ外に伝播しない
            Assert.That(caughtException, Is.Null, "NotifySessionEnded の例外はループ外に伝播してはならない");
        }

        /// <summary>
        /// CancellationToken が発火した場合、WaitForSessionRequest での OperationCanceledException が
        /// ループを正常終了させる。
        /// </summary>
        [Test]
        public async Task CancellationRequested_LoopExits()
        {
            // Arrange
            var memoryDb = BuildMemoryDatabase(validStageId: 1);
            _masterDataService.MemoryDatabase.Returns(memoryDb);

            using var cts = new CancellationTokenSource();

            // リクエストをキューに積まず、即座にキャンセルする
            _listener.TryDequeueSessionRequest(out Arg.Any<UnityServerSessionRequest>()).Returns(false);
            cts.CancelAfter(50); // 50ms 後にキャンセル

            var loop = CreateLoop();
            Exception caughtException = null;

            // Act
            try
            {
                await loop.StartAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 期待される例外
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // Assert: OperationCanceledException 以外は飛ばない
            Assert.That(caughtException, Is.Null, "キャンセル時は OperationCanceledException のみが発生するべき");
        }

        // ---------------------------------------------------------------
        // ヘルパーメソッド
        // ---------------------------------------------------------------

        /// <summary>
        /// <see cref="SurvivorServerGameLoop"/> インスタンスを作成し、
        /// private [Inject] フィールドにリフレクションでモックを注入する。
        /// </summary>
        private SurvivorServerGameLoop CreateLoop()
        {
            var loop = new SurvivorServerGameLoop();

            SetField(loop, "_sceneService", _sceneService);
            SetField(loop, "_saveService", _saveService);
            SetField(loop, "_masterDataService", _masterDataService);
            SetField(loop, "_runnerService", _runnerService);
            SetField(loop, "_networkConnector", _networkConnector);
            SetField(loop, "_sessionConfig", _sessionConfig);
            SetField(loop, "_listener", _listener);
            SetField(loop, "_registry", _registry);
            SetField(loop, "_bootstrap", _bootstrap);
            SetField(loop, "_allPlayersReadySub", _allPlayersReadySub);
            SetField(loop, "_allPlayersDisconnectedSub", _allPlayersDisconnectedSub);

            return loop;
        }

        /// <summary>
        /// リフレクションで private フィールドに値をセットする。
        /// </summary>
        private static void SetField(object target, string fieldName, object value)
        {
            var field = typeof(SurvivorServerGameLoop).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"フィールド '{fieldName}' が見つからない");
            field!.SetValue(target, value);
        }

        /// <summary>
        /// テスト用のセッションリクエストを作成する。
        /// </summary>
        private static UnityServerSessionRequest CreateSessionRequest(string sessionName, int stageId, int playerCount = 1)
        {
            return new UnityServerSessionRequest
            {
                SessionName = sessionName,
                StageId = stageId,
                PlayerCount = playerCount,
            };
        }

        /// <summary>
        /// <see cref="UnityServerBootstrap"/> のモックを作成する。
        /// WaitForStartupAsync と LoadMasterDataAsync は即座に完了する。
        /// </summary>
        private static UnityServerBootstrap CreateBootstrapMock()
        {
            // UnityServerBootstrap は sealed class で constructor に依存があるため
            // NSubstitute でモックできない。代わりにリフレクションで直接インスタンスを作り、
            // WaitForStartupAsync の内部 UniTaskCompletionSource を完了済み状態にする
            var configProvider = new UnityServerConfigProvider();
            var listenerMock = Substitute.For<IUnityServerHttpListener>();
            var registryMock = Substitute.For<IUnityServerRegistryApiClient>();
            var sessionConfigMock = Substitute.For<IGameSessionConfig>();

            var bootstrap = new UnityServerBootstrap(configProvider, listenerMock, registryMock, sessionConfigMock);

            // _startupComplete フィールドを完了済み状態にする
            var startupField = typeof(UnityServerBootstrap).GetField(
                "_startupComplete", BindingFlags.NonPublic | BindingFlags.Instance);
            if (startupField?.GetValue(bootstrap) is UniTaskCompletionSource tcs)
            {
                tcs.TrySetResult();
            }

            return bootstrap;
        }

        /// <summary>
        /// テスト用の <see cref="MemoryDatabase"/> を構築する。
        /// <paramref name="validStageId"/> のみが有効なステージとして登録される。
        /// </summary>
        private static MemoryDatabase BuildMemoryDatabase(int validStageId)
        {
            var formatterResolver = CompositeResolver.Create(
                MasterMemoryResolver.Instance,
                StandardResolver.Instance);
            var builder = new DatabaseBuilder(formatterResolver);

            builder.Append(new[]
            {
                new SurvivorStageMaster { Id = validStageId, Name = "TestStage", TimeLimit = 60, Difficulty = 1 }
            });

            // SurvivorStageModel が参照する可能性があるテーブルも最低限追加
            builder.Append(new[]
            {
                new SurvivorPlayerMaster { Id = 1, Name = "TestPlayer", StartingWeaponId = 1 }
            });
            builder.Append(new[]
            {
                new SurvivorPlayerLevelMaster
                {
                    PlayerId = 1, Level = 1,
                    MaxHp = 100, RequiredExp = 100,
                    DamageBonus = 0, WeaponChoiceCount = 3
                }
            });

            var binary = builder.Build();
            return new MemoryDatabase(binary, formatterResolver: formatterResolver);
        }
    }
}
