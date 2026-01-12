using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Game.Core;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Editor.Tests
{
    [TestFixture]
    public class StateMachinePerformanceTests
    {
        private const int DefaultIterationCount = 100000;
        private const int WarmupIterations = 1000;
        private const int TransitionsPerUpdate = 10;

        // Profilerマーカー
        private static readonly ProfilerMarker UpdateMarker = new("StateMachine.Update");
        private static readonly ProfilerMarker TransitionMarker = new("StateMachine.Transition");
        private static readonly ProfilerMarker FullCycleMarker = new("StateMachine.FullCycle");

        // ログ出力用
        private StringBuilder _logBuilder;
        private string _currentTestName;
        private DateTime _testStartTime;

        #region Complex Context

        private class ComplexGameContext
        {
            // プレイヤー情報
            public PlayerData Player { get; } = new();

            // インベントリシステム
            public InventorySystem Inventory { get; } = new();

            // ゲーム統計
            public GameStatistics Statistics { get; } = new();

            // イベントキュー
            public Queue<GameEvent> EventQueue { get; } = new();

            // 状態遷移カウンター（整合性検証用）
            public TransitionCounter TransitionCounter { get; } = new();

            // ランダム生成器（決定論的テスト用）
            public System.Random Random { get; }

            public ComplexGameContext(int seed = 12345)
            {
                Random = new System.Random(seed);
            }

            public void Reset()
            {
                Player.Reset();
                Inventory.Clear();
                Statistics.Reset();
                EventQueue.Clear();
                TransitionCounter.Reset();
            }
        }

        private class PlayerData
        {
            public string Name { get; set; } = "TestPlayer";
            public int Level { get; set; } = 1;
            public float Health { get; set; } = 100f;
            public float MaxHealth { get; set; } = 100f;
            public float Mana { get; set; } = 50f;
            public float MaxMana { get; set; } = 50f;
            public Vector3 Position { get; set; } = Vector3.zero;
            public Quaternion Rotation { get; set; } = Quaternion.identity;
            public float Experience { get; set; }
            public int Gold { get; set; }
            public List<string> ActiveBuffs { get; } = new();

            public Dictionary<string, float> Stats { get; } = new()
            {
                { "Strength", 10f },
                { "Agility", 10f },
                { "Intelligence", 10f },
                { "Vitality", 10f }
            };

            public void Reset()
            {
                Level = 1;
                Health = MaxHealth = 100f;
                Mana = MaxMana = 50f;
                Position = Vector3.zero;
                Rotation = Quaternion.identity;
                Experience = 0;
                Gold = 0;
                ActiveBuffs.Clear();
            }

            public void TakeDamage(float damage)
            {
                Health = Mathf.Max(0, Health - damage);
            }

            public void Heal(float amount)
            {
                Health = Mathf.Min(MaxHealth, Health + amount);
            }

            public void GainExperience(float exp)
            {
                Experience += exp;
                while (Experience >= Level * 100)
                {
                    Experience -= Level * 100;
                    Level++;
                    MaxHealth += 10;
                    MaxMana += 5;
                    Health = MaxHealth;
                    Mana = MaxMana;
                }
            }
        }

        private class InventorySystem
        {
            public List<Item> Items { get; } = new();
            public int MaxCapacity { get; set; } = 100;
            public float TotalWeight { get; private set; }

            public bool AddItem(Item item)
            {
                if (Items.Count >= MaxCapacity) return false;
                Items.Add(item);
                TotalWeight += item.Weight;
                return true;
            }

            public bool RemoveItem(string itemId)
            {
                var item = Items.Find(i => i.Id == itemId);
                if (item == null) return false;
                Items.Remove(item);
                TotalWeight -= item.Weight;
                return true;
            }

            public void Clear()
            {
                Items.Clear();
                TotalWeight = 0;
            }
        }

        private class Item
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public float Weight { get; set; }
            public int Value { get; set; }
            public ItemType Type { get; set; }
        }

        private enum ItemType
        {
            Weapon,
            Armor,
            Consumable,
            Material,
            Quest
        }

        private class GameStatistics
        {
            public int TotalUpdates { get; set; }
            public int TotalTransitions { get; set; }
            public int TotalDamageDealt { get; set; }
            public int TotalDamageTaken { get; set; }
            public int TotalHealing { get; set; }
            public int EnemiesDefeated { get; set; }
            public int ItemsCollected { get; set; }
            public Dictionary<string, int> StateVisitCount { get; } = new();

            public void Reset()
            {
                TotalUpdates = 0;
                TotalTransitions = 0;
                TotalDamageDealt = 0;
                TotalDamageTaken = 0;
                TotalHealing = 0;
                EnemiesDefeated = 0;
                ItemsCollected = 0;
                StateVisitCount.Clear();
            }

            public void RecordStateVisit(string stateName)
            {
                if (!StateVisitCount.ContainsKey(stateName))
                    StateVisitCount[stateName] = 0;
                StateVisitCount[stateName]++;
            }
        }

        private class GameEvent
        {
            public string Type { get; set; }
            public object Data { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private class TransitionCounter
        {
            public int EnterCount { get; set; }
            public int ExitCount { get; set; }
            public int UpdateCount { get; set; }
            public string LastState { get; set; }
            public List<string> TransitionHistory { get; } = new();

            // 遷移時間計測用
            public int SuccessCount { get; set; }
            public int FailureCount { get; set; }
            public List<double> TransitionTimesMs { get; } = new();
            public double MaxTransitionTimeMs { get; set; }

            public void Reset()
            {
                EnterCount = 0;
                ExitCount = 0;
                UpdateCount = 0;
                LastState = null;
                TransitionHistory.Clear();
                SuccessCount = 0;
                FailureCount = 0;
                TransitionTimesMs.Clear();
                MaxTransitionTimeMs = 0;
            }

            public void RecordTransitionResult(bool success, double elapsedMs)
            {
                if (success)
                {
                    SuccessCount++;
                    TransitionTimesMs.Add(elapsedMs);
                    if (elapsedMs > MaxTransitionTimeMs)
                    {
                        MaxTransitionTimeMs = elapsedMs;
                    }
                }
                else
                {
                    FailureCount++;
                }
            }

            public double GetAverageTransitionTimeMs()
            {
                if (TransitionTimesMs.Count == 0) return 0;
                double sum = 0;
                foreach (var time in TransitionTimesMs)
                {
                    sum += time;
                }

                return sum / TransitionTimesMs.Count;
            }

            public double GetFailureRate()
            {
                int total = SuccessCount + FailureCount;
                if (total == 0) return 0;
                return (double)FailureCount / total * 100.0;
            }

            /// <summary>
            /// 指定したパーセンタイルの遷移時間を取得（0.0-1.0）
            /// </summary>
            public double GetPercentileTransitionTimeMs(double percentile)
            {
                if (TransitionTimesMs.Count == 0) return 0;

                // ソート済みリストを作成（元のリストを変更しない）
                var sortedTimes = new List<double>(TransitionTimesMs);
                sortedTimes.Sort();

                int index = (int)(sortedTimes.Count * percentile);
                index = Math.Min(index, sortedTimes.Count - 1);
                return sortedTimes[index];
            }

            public void RecordEnter(string stateName)
            {
                EnterCount++;
                LastState = stateName;
                TransitionHistory.Add($"Enter:{stateName}");
            }

            public void RecordExit(string stateName)
            {
                ExitCount++;
                TransitionHistory.Add($"Exit:{stateName}");
            }

            public void RecordUpdate(string stateName)
            {
                UpdateCount++;
            }
        }

        #endregion

        #region Game Events

        private enum GameStateEvent
        {
            StartGame,
            EnterCombat,
            ExitCombat,
            Victory,
            Defeat,
            OpenInventory,
            CloseInventory,
            OpenMenu,
            CloseMenu,
            Interact,
            Rest,
            Travel,
            TriggerEvent,
            Complete
        }

        #endregion

        #region States

        private abstract class GameStateBase : State<ComplexGameContext, GameStateEvent>
        {
            protected string StateName => GetType().Name;

            public override void Enter()
            {
                Context.TransitionCounter.RecordEnter(StateName);
                Context.Statistics.RecordStateVisit(StateName);
            }

            public override void Update()
            {
                Context.TransitionCounter.RecordUpdate(StateName);
                Context.Statistics.TotalUpdates++;
            }

            public override void Exit()
            {
                Context.TransitionCounter.RecordExit(StateName);
            }

            protected void PerformComplexCalculation()
            {
                // 複雑な計算をシミュレート
                var player = Context.Player;
                var stats = player.Stats;

                float totalStat = 0;
                foreach (var stat in stats.Values)
                {
                    totalStat += stat;
                }

                var power = totalStat * player.Level * 0.1f;
                var defense = stats["Vitality"] * 2 + player.Level;

                // 位置更新
                var direction = new Vector3(
                    (float)(Context.Random.NextDouble() - 0.5) * 2,
                    0,
                    (float)(Context.Random.NextDouble() - 0.5) * 2
                ).normalized;

                player.Position += direction * 0.1f;
            }
        }

        private class IdleState : GameStateBase
        {
            public override void Update()
            {
                base.Update();
                PerformComplexCalculation();

                // アイドル中はマナ回復
                Context.Player.Mana = Mathf.Min(Context.Player.MaxMana, Context.Player.Mana + 0.1f);
            }
        }

        private class CombatState : GameStateBase
        {
            private int _combatTurns;

            public override void Enter()
            {
                base.Enter();
                _combatTurns = 0;
            }

            public override void Update()
            {
                base.Update();
                PerformComplexCalculation();

                _combatTurns++;

                // 戦闘シミュレーション
                var damage = Context.Random.Next(5, 20);
                Context.Player.TakeDamage(damage);
                Context.Statistics.TotalDamageTaken += damage;

                var dealDamage = Context.Random.Next(10, 30);
                Context.Statistics.TotalDamageDealt += dealDamage;

                // バフ処理
                if (_combatTurns % 5 == 0 && Context.Player.ActiveBuffs.Count < 5)
                {
                    Context.Player.ActiveBuffs.Add($"CombatBuff_{_combatTurns}");
                }
            }

            public override void Exit()
            {
                base.Exit();
                Context.Player.ActiveBuffs.RemoveAll(b => b.StartsWith("CombatBuff"));
            }
        }

        private class VictoryState : GameStateBase
        {
            public override void Enter()
            {
                base.Enter();
                Context.Statistics.EnemiesDefeated++;
                Context.Player.GainExperience(50);
                Context.Player.Gold += Context.Random.Next(10, 100);

                // 戦利品追加
                var item = new Item
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"Loot_{Context.Statistics.ItemsCollected}",
                    Weight = (float)Context.Random.NextDouble() * 5,
                    Value = Context.Random.Next(1, 50),
                    Type = (ItemType)Context.Random.Next(0, 5)
                };

                if (Context.Inventory.AddItem(item))
                {
                    Context.Statistics.ItemsCollected++;
                }
            }

            public override void Update()
            {
                base.Update();
                PerformComplexCalculation();
            }
        }

        private class DefeatState : GameStateBase
        {
            public override void Enter()
            {
                base.Enter();
                Context.Player.Health = Context.Player.MaxHealth * 0.5f;
                Context.Player.Gold = (int)(Context.Player.Gold * 0.9f);
            }

            public override void Update()
            {
                base.Update();
            }
        }

        private class InventoryState : GameStateBase
        {
            public override void Update()
            {
                base.Update();

                // インベントリ操作シミュレーション
                var items = Context.Inventory.Items;
                if (items.Count > 0)
                {
                    // 重量計算
                    float totalWeight = 0;
                    int totalValue = 0;
                    foreach (var item in items)
                    {
                        totalWeight += item.Weight;
                        totalValue += item.Value;
                    }
                }
            }
        }

        private class MenuState : GameStateBase
        {
            public override void Update()
            {
                base.Update();
                // メニュー操作シミュレーション
            }
        }

        private class RestState : GameStateBase
        {
            public override void Update()
            {
                base.Update();

                // 休憩中は回復
                var healing = 5f;
                Context.Player.Heal(healing);
                Context.Statistics.TotalHealing += (int)healing;
                Context.Player.Mana = Mathf.Min(Context.Player.MaxMana, Context.Player.Mana + 2f);
            }
        }

        private class TravelState : GameStateBase
        {
            public override void Update()
            {
                base.Update();
                PerformComplexCalculation();

                // 移動中のイベント発生
                if (Context.Random.NextDouble() < 0.1)
                {
                    Context.EventQueue.Enqueue(new GameEvent
                    {
                        Type = "RandomEncounter",
                        Timestamp = DateTime.Now
                    });
                }
            }
        }

        private class EventState : GameStateBase
        {
            public override void Update()
            {
                base.Update();

                // イベント処理
                while (Context.EventQueue.Count > 0)
                {
                    var evt = Context.EventQueue.Dequeue();
                    ProcessEvent(evt);
                }
            }

            private void ProcessEvent(GameEvent evt)
            {
                switch (evt.Type)
                {
                    case "RandomEncounter":
                        Context.Player.TakeDamage(10);
                        Context.Statistics.TotalDamageTaken += 10;
                        break;
                }
            }
        }

        #endregion

        #region Setup/TearDown

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _logBuilder = new StringBuilder();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            WriteLogFile();
            _logBuilder?.Clear();
            _logBuilder = null;
        }

        [SetUp]
        public void SetUp()
        {
            _currentTestName = TestContext.CurrentContext.Test.Name;
            _testStartTime = DateTime.Now;
            LogHeader();
        }

        [TearDown]
        public void TearDown()
        {
            LogFooter();
        }

        #endregion

        #region Log Helper Methods

        private void Log(string message)
        {
            Debug.Log(message);
            _logBuilder?.AppendLine(message);
        }

        private void LogLine(string message)
        {
            _logBuilder?.AppendLine(message);
        }

        private void LogHeader()
        {
            LogLine("================================================================================");
            LogLine($"StateMachinePerformanceTests --- {_currentTestName}");
            LogLine("================================================================================");
            LogLine($"テスト名: {_currentTestName}");
            LogLine($"開始時刻: {_testStartTime:yyyy-MM-dd HH:mm:ss}");
            LogLine($"イテレーション数: {DefaultIterationCount}");
            LogLine($"遷移/Update: {TransitionsPerUpdate}");
            LogLine("================================================================================");
            LogLine("");
        }

        private void LogFooter()
        {
            var endTime = DateTime.Now;
            var duration = endTime - _testStartTime;
            LogLine("");
            LogLine("--------------------------------------------------------------------------------");
            LogLine($"終了時刻: {endTime:yyyy-MM-dd HH:mm:ss}");
            LogLine($"実行時間: {duration.TotalSeconds:F2} 秒");
            LogLine("================================================================================");
            LogLine("");
        }

        private void WriteLogFile()
        {
            if (_logBuilder == null || _logBuilder.Length == 0) return;

            try
            {
                string logDirectory = $"{Application.dataPath}/Programs/Editor/Tests/Logs";
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                string filePath = $"{logDirectory}/StateMachinePerformanceTests_{timestamp}.log";

                File.WriteAllText(filePath, _logBuilder.ToString(), Encoding.UTF8);
                Debug.Log($"ログファイル出力完了: {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"ログファイル出力エラー: {ex.Message}");
            }
        }

        #endregion

        #region Performance Tests

        [Test]
        public void Performance_HighVolumeUpdate_MeasureThroughput()
        {
            var context = new ComplexGameContext();
            var stateMachine = CreateGameStateMachine(context);

            // ウォームアップ
            for (int i = 0; i < WarmupIterations; i++)
            {
                stateMachine.Update();
            }

            context.Reset();

            // GC安定化
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var stopwatch = new Stopwatch();
            long startMemory = GC.GetTotalMemory(true);
            int startGcCount = GC.CollectionCount(0);

            stopwatch.Start();

            using (UpdateMarker.Auto())
            {
                for (int i = 0; i < DefaultIterationCount; i++)
                {
                    stateMachine.Update();
                }
            }

            stopwatch.Stop();

            long endMemory = GC.GetTotalMemory(false);
            int endGcCount = GC.CollectionCount(0);

            double totalMs = stopwatch.Elapsed.TotalMilliseconds;
            double opsPerSecond = DefaultIterationCount / (totalMs / 1000.0);
            long memoryAllocated = endMemory - startMemory;
            int gcCollections = endGcCount - startGcCount;

            Log("=== Update スループット測定 ===");
            Log($"イテレーション数: {DefaultIterationCount:N0}");
            Log($"総時間: {totalMs:F3} ms");
            Log($"平均時間/Update: {totalMs / DefaultIterationCount:F6} ms");
            Log($"スループット: {opsPerSecond:N0} ops/sec");
            Log($"メモリアロケーション: {memoryAllocated:N0} bytes");
            Log($"GC発生回数: {gcCollections}");
            Log($"総Update回数: {context.Statistics.TotalUpdates:N0}");

            Assert.That(opsPerSecond, Is.GreaterThan(10000), "スループットが10,000 ops/sec未満です");
        }

        [Test]
        public void Performance_RapidTransitions_MeasureThroughput()
        {
            var context = new ComplexGameContext();
            var stateMachine = CreateGameStateMachine(context);

            // 初期化
            stateMachine.Update();

            // ウォームアップ
            for (int i = 0; i < WarmupIterations; i++)
            {
                ExecuteTransitionCycle(stateMachine);
            }

            context.Reset();
            stateMachine.Update();

            // GC安定化
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var stopwatch = new Stopwatch();
            int transitionCount = 0;
            long startMemory = GC.GetTotalMemory(true);

            stopwatch.Start();

            using (TransitionMarker.Auto())
            {
                for (int i = 0; i < DefaultIterationCount; i++)
                {
                    transitionCount += ExecuteTransitionCycle(stateMachine);
                }
            }

            stopwatch.Stop();

            long endMemory = GC.GetTotalMemory(false);

            double totalMs = stopwatch.Elapsed.TotalMilliseconds;
            double transitionsPerSecond = transitionCount / (totalMs / 1000.0);
            long memoryAllocated = endMemory - startMemory;

            Log("=== 遷移スループット測定 ===");
            Log($"イテレーション数: {DefaultIterationCount:N0}");
            Log($"総遷移回数: {transitionCount:N0}");
            Log($"総時間: {totalMs:F3} ms");
            Log($"遷移スループット: {transitionsPerSecond:N0} transitions/sec");
            Log($"平均時間/遷移: {totalMs / transitionCount:F6} ms");
            Log($"メモリアロケーション: {memoryAllocated:N0} bytes");

            Assert.That(transitionsPerSecond, Is.GreaterThan(50000), "遷移スループットが50,000/sec未満です");
        }

        [Test]
        public void Performance_FullCycle_WithComplexContext()
        {
            var context = new ComplexGameContext();
            var stateMachine = CreateGameStateMachine(context);

            // GC安定化
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var stopwatch = new Stopwatch();
            long startMemory = GC.GetTotalMemory(true);
            int totalTransitions = 0;

            stopwatch.Start();

            using (FullCycleMarker.Auto())
            {
                for (int i = 0; i < DefaultIterationCount; i++)
                {
                    // Update実行
                    stateMachine.Update();

                    // 定期的に遷移
                    if (i % 10 == 0)
                    {
                        totalTransitions += ExecuteTransitionCycle(stateMachine);
                    }

                    // FixedUpdate/LateUpdate
                    if (i % 2 == 0)
                    {
                        stateMachine.FixedUpdate();
                    }

                    stateMachine.LateUpdate();
                }
            }

            stopwatch.Stop();

            long endMemory = GC.GetTotalMemory(false);

            double totalMs = stopwatch.Elapsed.TotalMilliseconds;
            double opsPerSecond = DefaultIterationCount / (totalMs / 1000.0);
            long memoryAllocated = endMemory - startMemory;

            Log("=== フルサイクルパフォーマンス ===");
            Log($"イテレーション数: {DefaultIterationCount:N0}");
            Log($"総時間: {totalMs:F3} ms");
            Log($"スループット: {opsPerSecond:N0} cycles/sec");
            Log($"総遷移回数: {totalTransitions:N0}");
            Log($"メモリアロケーション: {memoryAllocated:N0} bytes");
            Log("");
            Log("[コンテキスト統計]");
            Log($"  プレイヤーレベル: {context.Player.Level}");
            Log($"  総経験値獲得: {context.Player.Experience:F0}");
            Log($"  所持金: {context.Player.Gold}");
            Log($"  インベントリアイテム数: {context.Inventory.Items.Count}");
            Log($"  総ダメージ受: {context.Statistics.TotalDamageTaken}");
            Log($"  総ダメージ与: {context.Statistics.TotalDamageDealt}");
            Log($"  総回復量: {context.Statistics.TotalHealing}");
            Log($"  敵撃破数: {context.Statistics.EnemiesDefeated}");

            Assert.That(opsPerSecond, Is.GreaterThan(5000), "フルサイクルスループットが5,000/sec未満です");
        }

        [Test]
        public void Integrity_MassiveTransitions_VerifyStateConsistency()
        {
            var context = new ComplexGameContext();
            var stateMachine = CreateGameStateMachine(context);

            int totalTransitions = 0;
            int expectedEnterCount = 1; // 初期状態のEnter

            // 初期化
            stateMachine.Update();

            Log("=== 状態遷移整合性検証 ===");
            Log($"イテレーション数: {DefaultIterationCount:N0}");

            for (int i = 0; i < DefaultIterationCount; i++)
            {
                stateMachine.Update();

                // ランダムな遷移を実行
                if (context.Random.NextDouble() < 0.3)
                {
                    int transitionsThisCycle = ExecuteRandomTransitions(stateMachine, context.Random);
                    totalTransitions += transitionsThisCycle;
                    expectedEnterCount += transitionsThisCycle;
                }

                // 定期的に整合性チェック
                if (i % 10000 == 0 && i > 0)
                {
                    VerifyIntegrity(context, i);
                }
            }

            // 最終整合性検証
            Log("");
            Log("[最終整合性検証]");
            Log($"  総遷移回数: {totalTransitions:N0}");
            Log($"  Enter回数: {context.TransitionCounter.EnterCount}");
            Log($"  Exit回数: {context.TransitionCounter.ExitCount}");
            Log($"  Update回数: {context.TransitionCounter.UpdateCount}");
            Log($"  最終ステート: {context.TransitionCounter.LastState}");

            // Enter回数 = Exit回数 + 1 (最後のステートはExitしていない)
            Assert.That(context.TransitionCounter.EnterCount,
                Is.EqualTo(context.TransitionCounter.ExitCount + 1),
                "Enter/Exit回数の整合性エラー");

            // 遷移履歴の整合性検証
            var history = context.TransitionCounter.TransitionHistory;
            int enterInHistory = 0;
            int exitInHistory = 0;
            foreach (var entry in history)
            {
                if (entry.StartsWith("Enter:")) enterInHistory++;
                else if (entry.StartsWith("Exit:")) exitInHistory++;
            }

            Assert.That(enterInHistory, Is.EqualTo(context.TransitionCounter.EnterCount),
                "履歴のEnter回数が不一致");
            Assert.That(exitInHistory, Is.EqualTo(context.TransitionCounter.ExitCount),
                "履歴のExit回数が不一致");

            Log("[状態訪問回数]");
            foreach (var kvp in context.Statistics.StateVisitCount)
            {
                Log($"  {kvp.Key}: {kvp.Value:N0}");
            }

            Log("");
            Log("整合性検証: OK");

            Assert.Pass($"整合性検証完了 - 総遷移回数: {totalTransitions:N0}");
        }

        [Test]
        public void Performance_Comparison_SimpleVsComplexContext()
        {
            const int iterations = 50000;

            // シンプルコンテキストでのテスト
            var simpleContext = new SimpleContext();
            var simpleStateMachine = CreateSimpleStateMachine(simpleContext);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var simpleStopwatch = new Stopwatch();
            long simpleStartMemory = GC.GetTotalMemory(true);

            simpleStopwatch.Start();
            for (int i = 0; i < iterations; i++)
            {
                simpleStateMachine.Update();
                if (i % 10 == 0)
                {
                    var result = simpleStateMachine.Transition(SimpleEvent.Next);
                    if (result == StateEventResult.Succeeded)
                    {
                        simpleStateMachine.Update();
                    }
                    else if (result == StateEventResult.Waiting)
                    {
                        while (simpleStateMachine.IsProcessing())
                        {
                            simpleStateMachine.Update();
                        }
                    }
                }
            }

            simpleStopwatch.Stop();

            long simpleEndMemory = GC.GetTotalMemory(false);
            double simpleMs = simpleStopwatch.Elapsed.TotalMilliseconds;
            long simpleMemory = simpleEndMemory - simpleStartMemory;

            // 複雑コンテキストでのテスト
            var complexContext = new ComplexGameContext();
            var complexStateMachine = CreateGameStateMachine(complexContext);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var complexStopwatch = new Stopwatch();
            long complexStartMemory = GC.GetTotalMemory(true);

            complexStopwatch.Start();
            for (int i = 0; i < iterations; i++)
            {
                complexStateMachine.Update();
                if (i % 10 == 0)
                {
                    ExecuteTransitionCycle(complexStateMachine);
                }
            }

            complexStopwatch.Stop();

            long complexEndMemory = GC.GetTotalMemory(false);
            double complexMs = complexStopwatch.Elapsed.TotalMilliseconds;
            long complexMemory = complexEndMemory - complexStartMemory;

            Log("=== シンプル vs 複雑コンテキスト比較 ===");
            Log($"イテレーション数: {iterations:N0}");
            Log("");
            Log("[シンプルコンテキスト]");
            Log($"  時間: {simpleMs:F3} ms");
            Log($"  スループット: {iterations / (simpleMs / 1000.0):N0} ops/sec");
            Log($"  メモリ: {simpleMemory:N0} bytes");
            Log("");
            Log("[複雑コンテキスト]");
            Log($"  時間: {complexMs:F3} ms");
            Log($"  スループット: {iterations / (complexMs / 1000.0):N0} ops/sec");
            Log($"  メモリ: {complexMemory:N0} bytes");
            Log("");
            Log("[オーバーヘッド]");
            Log($"  時間倍率: {complexMs / simpleMs:F2}x");
            Log($"  メモリ倍率: {(double)complexMemory / simpleMemory:F2}x");

            Assert.Pass("比較テスト完了");
        }

        [Test]
        public void Performance_GCPressure_UnderHeavyLoad()
        {
            var context = new ComplexGameContext();
            var stateMachine = CreateGameStateMachine(context);

            // ウォームアップ
            for (int i = 0; i < WarmupIterations; i++)
            {
                stateMachine.Update();
                ExecuteTransitionCycle(stateMachine);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            int gen0Before = GC.CollectionCount(0);
            int gen1Before = GC.CollectionCount(1);
            int gen2Before = GC.CollectionCount(2);
            long memoryBefore = GC.GetTotalMemory(true);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < DefaultIterationCount; i++)
            {
                stateMachine.Update();

                if (i % 5 == 0)
                {
                    ExecuteTransitionCycle(stateMachine);
                }

                // インベントリ操作（アロケーション発生源）
                if (i % 100 == 0)
                {
                    context.Inventory.AddItem(new Item
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = $"Item_{i}",
                        Weight = 1.0f,
                        Value = 10,
                        Type = ItemType.Material
                    });
                }

                // 定期的にインベントリクリア（メモリ解放）
                if (i % 1000 == 0 && context.Inventory.Items.Count > 50)
                {
                    context.Inventory.Clear();
                }
            }

            stopwatch.Stop();

            int gen0After = GC.CollectionCount(0);
            int gen1After = GC.CollectionCount(1);
            int gen2After = GC.CollectionCount(2);
            long memoryAfter = GC.GetTotalMemory(false);

            Log("=== GCプレッシャー測定 ===");
            Log($"イテレーション数: {DefaultIterationCount:N0}");
            Log($"総時間: {stopwatch.Elapsed.TotalMilliseconds:F3} ms");
            Log("");
            Log("[GCコレクション]");
            Log($"  Gen0: {gen0After - gen0Before}");
            Log($"  Gen1: {gen1After - gen1Before}");
            Log($"  Gen2: {gen2After - gen2Before}");
            Log("");
            Log("[メモリ]");
            Log($"  開始時: {memoryBefore:N0} bytes");
            Log($"  終了時: {memoryAfter:N0} bytes");
            Log($"  差分: {memoryAfter - memoryBefore:N0} bytes");

            Assert.That(gen2After - gen2Before, Is.LessThanOrEqualTo(5),
                "Gen2 GCが多発しています");
        }

        [Test]
        public void Performance_TransitionTiming_MeasureAverageAndMax()
        {
            var context = new ComplexGameContext();
            var stateMachine = CreateGameStateMachine(context);
            var transitionStopwatch = new Stopwatch();

            // 初期化
            stateMachine.Update();

            // ウォームアップ
            for (int i = 0; i < WarmupIterations; i++)
            {
                ExecuteTransitionCycleWithTiming(stateMachine, context, transitionStopwatch);
            }

            context.Reset();
            context.TransitionCounter.Reset();
            stateMachine.Update();

            // GC安定化
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Log("=== 状態遷移時間・失敗率測定 ===");
            Log($"イテレーション数: {DefaultIterationCount:N0}");

            var totalStopwatch = new Stopwatch();
            totalStopwatch.Start();

            for (int i = 0; i < DefaultIterationCount; i++)
            {
                stateMachine.Update();

                // ランダムな遷移を時間計測付きで実行
                ExecuteRandomTransitionsWithTiming(stateMachine, context, transitionStopwatch);
            }

            totalStopwatch.Stop();

            var counter = context.TransitionCounter;
            double avgTimeMs = counter.GetAverageTransitionTimeMs();
            double maxTimeMs = counter.MaxTransitionTimeMs;
            double failureRate = counter.GetFailureRate();
            int totalAttempts = counter.SuccessCount + counter.FailureCount;

            Log("");
            Log("[遷移時間統計]");
            Log($"  総遷移試行回数: {totalAttempts:N0}");
            Log($"  成功回数: {counter.SuccessCount:N0}");
            Log($"  失敗回数: {counter.FailureCount:N0}");
            Log($"  失敗率: {failureRate:F2}%");
            Log("");
            Log("[遷移時間]");
            Log($"  平均遷移時間: {avgTimeMs:F6} ms");
            Log($"  最大遷移時間: {maxTimeMs:F6} ms");
            Log($"  平均遷移時間: {avgTimeMs * 1000:F3} μs");
            Log($"  最大遷移時間: {maxTimeMs * 1000:F3} μs");
            Log("");
            Log("[総合統計]");
            Log($"  総実行時間: {totalStopwatch.Elapsed.TotalMilliseconds:F3} ms");
            Log($"  遷移スループット: {counter.SuccessCount / (totalStopwatch.Elapsed.TotalMilliseconds / 1000.0):N0} transitions/sec");

            // パーセンタイル計算
            if (counter.TransitionTimesMs.Count > 0)
            {
                var sortedTimes = new List<double>(counter.TransitionTimesMs);
                sortedTimes.Sort();

                int p50Index = (int)(sortedTimes.Count * 0.50);
                int p90Index = (int)(sortedTimes.Count * 0.90);
                int p99Index = (int)(sortedTimes.Count * 0.99);

                Log("");
                Log("[パーセンタイル]");
                Log($"  P50 (中央値): {sortedTimes[p50Index]:F6} ms ({sortedTimes[p50Index] * 1000:F3} μs)");
                Log($"  P90: {sortedTimes[Math.Min(p90Index, sortedTimes.Count - 1)]:F6} ms ({sortedTimes[Math.Min(p90Index, sortedTimes.Count - 1)] * 1000:F3} μs)");
                Log($"  P99: {sortedTimes[Math.Min(p99Index, sortedTimes.Count - 1)]:F6} ms ({sortedTimes[Math.Min(p99Index, sortedTimes.Count - 1)] * 1000:F3} μs)");
            }

            Assert.That(failureRate, Is.LessThan(100), "全ての遷移が失敗しています");
            Assert.Pass($"平均遷移時間: {avgTimeMs * 1000:F3} μs, 最大遷移時間: {maxTimeMs * 1000:F3} μs, 失敗率: {failureRate:F2}%");
        }

        [Test]
        public void Performance_TransitionFailureAnalysis()
        {
            var context = new ComplexGameContext();
            var stateMachine = CreateGameStateMachine(context);
            var transitionStopwatch = new Stopwatch();

            // 初期化
            stateMachine.Update();

            context.TransitionCounter.Reset();

            // 失敗を意図的に多く発生させるテスト
            // 現在の状態から遷移できないイベントを多く送る
            var allEvents = (GameStateEvent[])Enum.GetValues(typeof(GameStateEvent));

            Log("=== 遷移失敗分析 ===");
            Log($"イテレーション数: {DefaultIterationCount:N0}");

            var successByEvent = new Dictionary<GameStateEvent, int>();
            var waitingByEvent = new Dictionary<GameStateEvent, int>();
            var failedByEvent = new Dictionary<GameStateEvent, int>();
            foreach (var evt in allEvents)
            {
                successByEvent[evt] = 0;
                waitingByEvent[evt] = 0;
                failedByEvent[evt] = 0;
            }

            var totalStopwatch = new Stopwatch();
            totalStopwatch.Start();

            for (int i = 0; i < DefaultIterationCount; i++)
            {
                stateMachine.Update();

                // ランダムなイベントを送信（失敗を含む）
                var evt = allEvents[context.Random.Next(allEvents.Length)];

                transitionStopwatch.Restart();
                var result = stateMachine.Transition(evt);
                transitionStopwatch.Stop();

                double elapsedMs = transitionStopwatch.Elapsed.TotalMilliseconds;

                switch (result)
                {
                    case StateEventResult.Succeeded:
                        context.TransitionCounter.RecordTransitionResult(true, elapsedMs);
                        successByEvent[evt]++;
                        stateMachine.Update();
                        break;

                    case StateEventResult.Waiting:
                        context.TransitionCounter.RecordTransitionResult(true, elapsedMs);
                        waitingByEvent[evt]++;
                        // 待機中はUpdateを繰り返し実行
                        while (stateMachine.IsProcessing())
                        {
                            stateMachine.Update();
                        }

                        break;

                    case StateEventResult.Failed:
                        context.TransitionCounter.RecordTransitionResult(false, elapsedMs);
                        failedByEvent[evt]++;
                        // 失敗時はIdleStateへ復帰
                        RecoverToIdleState(stateMachine);
                        break;
                }
            }

            totalStopwatch.Stop();

            var counter = context.TransitionCounter;
            double avgTimeMs = counter.GetAverageTransitionTimeMs();
            double maxTimeMs = counter.MaxTransitionTimeMs;
            double failureRate = counter.GetFailureRate();

            int totalSuccess = 0;
            int totalWaiting = 0;
            int totalFailed = 0;
            foreach (var evt in allEvents)
            {
                totalSuccess += successByEvent[evt];
                totalWaiting += waitingByEvent[evt];
                totalFailed += failedByEvent[evt];
            }

            Log("");
            Log("[全体統計]");
            Log($"  総遷移試行回数: {counter.SuccessCount + counter.FailureCount:N0}");
            Log($"  成功回数 (Success): {totalSuccess:N0}");
            Log($"  待機回数 (Waiting): {totalWaiting:N0}");
            Log($"  失敗回数 (Failed): {totalFailed:N0}");
            Log($"  失敗率: {failureRate:F2}%");
            Log($"  平均遷移時間: {avgTimeMs * 1000:F3} μs");
            Log($"  最大遷移時間: {maxTimeMs * 1000:F3} μs");
            Log("");
            Log("[イベント別結果]");
            Log($"  {"イベント",-20}  {"Success",8}  {"Waiting",8}  {"Failed",8}  {"失敗率",8}");
            Log($"  {new string('-', 64)}");

            foreach (var evt in allEvents)
            {
                int success = successByEvent[evt];
                int waiting = waitingByEvent[evt];
                int failed = failedByEvent[evt];
                int total = success + waiting + failed;
                double rate = total > 0 ? (double)failed / total * 100.0 : 0;
                Log($"  {evt,-20}: {success,8:N0}  {waiting,8:N0}  {failed,8:N0}  {rate,7:F1}%");
            }

            Assert.Pass($"遷移失敗率: {failureRate:F2}%, 総失敗数: {totalFailed:N0}");
        }

        [Test]
        public void Performance_TransitionTiming_SuccessfulTransitions()
        {
            var context = new ComplexGameContext();
            var stateMachine = CreateGameStateMachine(context);
            var transitionStopwatch = new Stopwatch();

            // 初期化
            stateMachine.Update();

            // ウォームアップ
            for (int i = 0; i < WarmupIterations; i++)
            {
                ExecuteTransitionCycleWithTiming(stateMachine, context, transitionStopwatch);
            }

            context.Reset();
            context.TransitionCounter.Reset();
            stateMachine.Update();

            // GC安定化
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Log("=== 成功遷移の時間計測 ===");
            Log($"イテレーション数: {DefaultIterationCount:N0}");
            Log("遷移パターン: Idle -> Combat -> Victory -> Idle (決定論的)");

            var totalStopwatch = new Stopwatch();
            totalStopwatch.Start();

            int totalTransitions = 0;
            for (int i = 0; i < DefaultIterationCount; i++)
            {
                totalTransitions += ExecuteTransitionCycleWithTiming(stateMachine, context, transitionStopwatch);
            }

            totalStopwatch.Stop();

            var counter = context.TransitionCounter;
            double avgTimeMs = counter.GetAverageTransitionTimeMs();
            double maxTimeMs = counter.MaxTransitionTimeMs;
            double failureRate = counter.GetFailureRate();

            Log("");
            Log("[遷移統計]");
            Log($"  総遷移試行回数: {counter.SuccessCount + counter.FailureCount:N0}");
            Log($"  成功回数: {counter.SuccessCount:N0}");
            Log($"  失敗回数: {counter.FailureCount:N0}");
            Log($"  成功率: {100.0 - failureRate:F2}%");
            Log($"  実際の遷移回数: {totalTransitions:N0}");
            Log("");
            Log("[遷移時間]");
            Log($"  平均遷移時間: {avgTimeMs:F6} ms");
            Log($"  最大遷移時間: {maxTimeMs:F6} ms");
            Log($"  平均遷移時間: {avgTimeMs * 1000:F3} μs");
            Log($"  最大遷移時間: {maxTimeMs * 1000:F3} μs");
            Log("");
            Log("[総合統計]");
            Log($"  総実行時間: {totalStopwatch.Elapsed.TotalMilliseconds:F3} ms");
            Log($"  遷移スループット: {counter.SuccessCount / (totalStopwatch.Elapsed.TotalMilliseconds / 1000.0):N0} transitions/sec");

            // パーセンタイル計算
            if (counter.TransitionTimesMs.Count > 0)
            {
                var sortedTimes = new List<double>(counter.TransitionTimesMs);
                sortedTimes.Sort();

                int p50Index = (int)(sortedTimes.Count * 0.50);
                int p90Index = (int)(sortedTimes.Count * 0.90);
                int p99Index = (int)(sortedTimes.Count * 0.99);

                Log("");
                Log("[パーセンタイル]");
                Log($"  P50 (中央値): {sortedTimes[p50Index]:F6} ms ({sortedTimes[p50Index] * 1000:F3} μs)");
                Log($"  P90: {sortedTimes[Math.Min(p90Index, sortedTimes.Count - 1)]:F6} ms ({sortedTimes[Math.Min(p90Index, sortedTimes.Count - 1)] * 1000:F3} μs)");
                Log($"  P99: {sortedTimes[Math.Min(p99Index, sortedTimes.Count - 1)]:F6} ms ({sortedTimes[Math.Min(p99Index, sortedTimes.Count - 1)] * 1000:F3} μs)");
            }

            // 成功前提のテストなので、失敗率は0%に近いはず
            Assert.That(failureRate, Is.LessThan(1.0), "成功前提のテストで失敗が発生しています");
            Assert.Pass($"平均遷移時間: {avgTimeMs * 1000:F3} μs, 最大遷移時間: {maxTimeMs * 1000:F3} μs, 成功率: {100.0 - failureRate:F2}%");
        }

        [Test]
        public void Performance_TransitionSuccessAnalysis()
        {
            var context = new ComplexGameContext();
            var stateMachine = CreateGameStateMachine(context);
            var transitionStopwatch = new Stopwatch();

            // 初期化
            stateMachine.Update();

            context.TransitionCounter.Reset();

            // 決定論的な遷移パターンを使用
            // Idle -> Combat -> Victory -> Idle のサイクルを繰り返す
            var transitionPatterns = new[]
            {
                GameStateEvent.EnterCombat, // Idle -> Combat
                GameStateEvent.Victory,     // Combat -> Victory
                GameStateEvent.Complete     // Victory -> Idle
            };

            Log("=== 成功遷移の詳細分析 ===");
            Log($"イテレーション数: {DefaultIterationCount:N0}");
            Log("遷移パターン: Idle -> Combat -> Victory -> Idle (決定論的)");

            var successByEvent = new Dictionary<GameStateEvent, int>();
            var waitingByEvent = new Dictionary<GameStateEvent, int>();
            var failedByEvent = new Dictionary<GameStateEvent, int>();
            var timesByEvent = new Dictionary<GameStateEvent, List<double>>();

            foreach (var evt in transitionPatterns)
            {
                successByEvent[evt] = 0;
                waitingByEvent[evt] = 0;
                failedByEvent[evt] = 0;
                timesByEvent[evt] = new List<double>();
            }

            var totalStopwatch = new Stopwatch();
            totalStopwatch.Start();

            for (int i = 0; i < DefaultIterationCount; i++)
            {
                foreach (var evt in transitionPatterns)
                {
                    transitionStopwatch.Restart();
                    var result = stateMachine.Transition(evt);
                    transitionStopwatch.Stop();

                    double elapsedMs = transitionStopwatch.Elapsed.TotalMilliseconds;

                    switch (result)
                    {
                        case StateEventResult.Succeeded:
                            context.TransitionCounter.RecordTransitionResult(true, elapsedMs);
                            successByEvent[evt]++;
                            timesByEvent[evt].Add(elapsedMs);
                            stateMachine.Update();
                            break;

                        case StateEventResult.Waiting:
                            context.TransitionCounter.RecordTransitionResult(true, elapsedMs);
                            waitingByEvent[evt]++;
                            timesByEvent[evt].Add(elapsedMs);
                            while (stateMachine.IsProcessing())
                            {
                                stateMachine.Update();
                            }

                            break;

                        case StateEventResult.Failed:
                            context.TransitionCounter.RecordTransitionResult(false, elapsedMs);
                            failedByEvent[evt]++;
                            // 失敗時はIdleへ復帰して継続
                            RecoverToIdleState(stateMachine);
                            // パターンの最初からやり直し
                            break;
                    }
                }
            }

            totalStopwatch.Stop();

            var counter = context.TransitionCounter;
            double avgTimeMs = counter.GetAverageTransitionTimeMs();
            double maxTimeMs = counter.MaxTransitionTimeMs;
            double failureRate = counter.GetFailureRate();

            int totalSuccess = 0;
            int totalWaiting = 0;
            int totalFailed = 0;
            foreach (var evt in transitionPatterns)
            {
                totalSuccess += successByEvent[evt];
                totalWaiting += waitingByEvent[evt];
                totalFailed += failedByEvent[evt];
            }

            Log("");
            Log("[全体統計]");
            Log($"  総遷移試行回数: {counter.SuccessCount + counter.FailureCount:N0}");
            Log($"  成功回数 (Success): {totalSuccess:N0}");
            Log($"  待機回数 (Waiting): {totalWaiting:N0}");
            Log($"  失敗回数 (Failed): {totalFailed:N0}");
            Log($"  成功率: {100.0 - failureRate:F2}%");
            Log($"  平均遷移時間: {avgTimeMs * 1000:F3} μs");
            Log($"  最大遷移時間: {maxTimeMs * 1000:F3} μs");
            Log("");
            Log("[イベント別結果]");
            Log($"  {"イベント",-20}  {"Success",8}  {"Waiting",8}  {"Failed",8}  {"平均時間(μs)",12}  {"最大時間(μs)",12}");
            Log($"  {new string('-', 80)}");

            foreach (var evt in transitionPatterns)
            {
                int success = successByEvent[evt];
                int waiting = waitingByEvent[evt];
                int failed = failedByEvent[evt];

                double avgTime = 0;
                double maxTime = 0;
                if (timesByEvent[evt].Count > 0)
                {
                    double sum = 0;
                    foreach (var t in timesByEvent[evt])
                    {
                        sum += t;
                        if (t > maxTime) maxTime = t;
                    }

                    avgTime = sum / timesByEvent[evt].Count;
                }

                Log($"  {evt,-20}: {success,8:N0}  {waiting,8:N0}  {failed,8:N0}  {avgTime * 1000,12:F3}  {maxTime * 1000,12:F3}");
            }

            Log("");
            Log("[総合パフォーマンス]");
            Log($"  総実行時間: {totalStopwatch.Elapsed.TotalMilliseconds:F3} ms");
            Log($"  遷移スループット: {counter.SuccessCount / (totalStopwatch.Elapsed.TotalMilliseconds / 1000.0):N0} transitions/sec");
            Log($"  サイクルスループット: {DefaultIterationCount / (totalStopwatch.Elapsed.TotalMilliseconds / 1000.0):N0} cycles/sec");

            // 成功前提のテストなので、失敗率は0%に近いはず
            Assert.That(failureRate, Is.LessThan(1.0), "成功前提のテストで失敗が発生しています");
            Assert.Pass($"成功率: {100.0 - failureRate:F2}%, 平均遷移時間: {avgTimeMs * 1000:F3} μs");
        }

        #endregion

        #region Helper Methods

        private StateMachine<ComplexGameContext, GameStateEvent> CreateGameStateMachine(ComplexGameContext context)
        {
            var stateMachine = new StateMachine<ComplexGameContext, GameStateEvent>(context);

            // 遷移ルール設定
            stateMachine.AddTransition<IdleState, CombatState>(GameStateEvent.EnterCombat);
            stateMachine.AddTransition<IdleState, InventoryState>(GameStateEvent.OpenInventory);
            stateMachine.AddTransition<IdleState, MenuState>(GameStateEvent.OpenMenu);
            stateMachine.AddTransition<IdleState, RestState>(GameStateEvent.Rest);
            stateMachine.AddTransition<IdleState, TravelState>(GameStateEvent.Travel);

            stateMachine.AddTransition<CombatState, VictoryState>(GameStateEvent.Victory);
            stateMachine.AddTransition<CombatState, DefeatState>(GameStateEvent.Defeat);
            stateMachine.AddTransition<CombatState, IdleState>(GameStateEvent.ExitCombat);

            stateMachine.AddTransition<VictoryState, IdleState>(GameStateEvent.Complete);
            stateMachine.AddTransition<DefeatState, IdleState>(GameStateEvent.Complete);

            stateMachine.AddTransition<InventoryState, IdleState>(GameStateEvent.CloseInventory);
            stateMachine.AddTransition<MenuState, IdleState>(GameStateEvent.CloseMenu);
            stateMachine.AddTransition<RestState, IdleState>(GameStateEvent.Complete);

            stateMachine.AddTransition<TravelState, IdleState>(GameStateEvent.Complete);
            stateMachine.AddTransition<TravelState, CombatState>(GameStateEvent.EnterCombat);
            stateMachine.AddTransition<TravelState, EventState>(GameStateEvent.TriggerEvent);

            stateMachine.AddTransition<EventState, IdleState>(GameStateEvent.Complete);
            stateMachine.AddTransition<EventState, CombatState>(GameStateEvent.EnterCombat);

            stateMachine.SetInitState<IdleState>();

            return stateMachine;
        }

        private int ExecuteTransitionCycle(StateMachine<ComplexGameContext, GameStateEvent> stateMachine)
        {
            int transitions = 0;

            // Idle -> Combat -> Victory -> Idle
            transitions += TryTransition(stateMachine, GameStateEvent.EnterCombat);
            transitions += TryTransition(stateMachine, GameStateEvent.Victory);
            transitions += TryTransition(stateMachine, GameStateEvent.Complete);

            return transitions;
        }

        private int TryTransition(
            StateMachine<ComplexGameContext, GameStateEvent> stateMachine,
            GameStateEvent evt)
        {
            var result = stateMachine.Transition(evt);

            switch (result)
            {
                case StateEventResult.Succeeded:
                    stateMachine.Update();
                    return 1;

                case StateEventResult.Waiting:
                    // 待機中の場合はUpdateを繰り返し実行
                    while (stateMachine.IsProcessing())
                    {
                        stateMachine.Update();
                    }

                    return 1;

                case StateEventResult.Failed:
                default:
                    return 0;
            }
        }

        private int ExecuteTransitionCycleWithTiming(
            StateMachine<ComplexGameContext, GameStateEvent> stateMachine,
            ComplexGameContext context,
            Stopwatch stopwatch)
        {
            int transitions = 0;
            var counter = context.TransitionCounter;

            // Idle -> Combat -> Victory -> Idle
            transitions += TryTransitionWithTiming(stateMachine, context, GameStateEvent.EnterCombat, stopwatch);
            transitions += TryTransitionWithTiming(stateMachine, context, GameStateEvent.Victory, stopwatch);
            transitions += TryTransitionWithTiming(stateMachine, context, GameStateEvent.Complete, stopwatch);

            return transitions;
        }

        private int TryTransitionWithTiming(
            StateMachine<ComplexGameContext, GameStateEvent> stateMachine,
            ComplexGameContext context,
            GameStateEvent evt,
            Stopwatch stopwatch)
        {
            var counter = context.TransitionCounter;

            stopwatch.Restart();
            var result = stateMachine.Transition(evt);
            stopwatch.Stop();

            double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            switch (result)
            {
                case StateEventResult.Succeeded:
                    counter.RecordTransitionResult(true, elapsedMs);
                    stateMachine.Update();
                    return 1;

                case StateEventResult.Waiting:
                    counter.RecordTransitionResult(true, elapsedMs);
                    // 待機中の場合はUpdateを繰り返し実行
                    while (stateMachine.IsProcessing())
                    {
                        stateMachine.Update();
                    }

                    return 1;

                case StateEventResult.Failed:
                default:
                    counter.RecordTransitionResult(false, elapsedMs);
                    return 0;
            }
        }

        private void RecoverToIdleState(StateMachine<ComplexGameContext, GameStateEvent> stateMachine)
        {
            // 現在の状態から遷移可能なイベントを試行してIdleへ戻る
            var recoveryEvents = new[]
            {
                GameStateEvent.Complete,
                GameStateEvent.CloseInventory,
                GameStateEvent.CloseMenu,
                GameStateEvent.ExitCombat
            };

            foreach (var evt in recoveryEvents)
            {
                var result = stateMachine.Transition(evt);
                if (result == StateEventResult.Succeeded)
                {
                    stateMachine.Update();
                    if (stateMachine.IsCurrentState<IdleState>())
                    {
                        return;
                    }
                }
                else if (result == StateEventResult.Waiting)
                {
                    while (stateMachine.IsProcessing())
                    {
                        stateMachine.Update();
                    }

                    if (stateMachine.IsCurrentState<IdleState>())
                    {
                        return;
                    }
                }
            }
        }

        private int ExecuteRandomTransitions(
            StateMachine<ComplexGameContext, GameStateEvent> stateMachine,
            System.Random random)
        {
            var events = new[]
            {
                GameStateEvent.EnterCombat,
                GameStateEvent.Victory,
                GameStateEvent.Complete,
                GameStateEvent.OpenInventory,
                GameStateEvent.CloseInventory,
                GameStateEvent.Rest
            };

            int transitions = 0;
            int attempts = random.Next(1, 5);

            for (int i = 0; i < attempts; i++)
            {
                var evt = events[random.Next(events.Length)];
                var result = stateMachine.Transition(evt);

                switch (result)
                {
                    case StateEventResult.Succeeded:
                        stateMachine.Update();
                        transitions++;
                        break;

                    case StateEventResult.Waiting:
                        while (stateMachine.IsProcessing())
                        {
                            stateMachine.Update();
                        }

                        transitions++;
                        break;

                    case StateEventResult.Failed:
                        // 失敗時はIdleStateへ復帰
                        RecoverToIdleState(stateMachine);
                        break;
                }
            }

            return transitions;
        }

        private int ExecuteRandomTransitionsWithTiming(
            StateMachine<ComplexGameContext, GameStateEvent> stateMachine,
            ComplexGameContext context,
            Stopwatch stopwatch)
        {
            var events = new[]
            {
                GameStateEvent.EnterCombat,
                GameStateEvent.Victory,
                GameStateEvent.Complete,
                GameStateEvent.OpenInventory,
                GameStateEvent.CloseInventory,
                GameStateEvent.Rest
            };

            int transitions = 0;
            int attempts = context.Random.Next(1, 5);

            for (int i = 0; i < attempts; i++)
            {
                var evt = events[context.Random.Next(events.Length)];
                transitions += TryTransitionWithTiming(stateMachine, context, evt, stopwatch);
            }

            return transitions;
        }

        private void VerifyIntegrity(ComplexGameContext context, int iteration)
        {
            var counter = context.TransitionCounter;

            // Enter回数 >= Exit回数 (常に成立するはず)
            if (counter.EnterCount < counter.ExitCount)
            {
                throw new InvalidOperationException(
                    $"整合性エラー @ iteration {iteration}: Enter({counter.EnterCount}) < Exit({counter.ExitCount})");
            }

            // Enter回数 = Exit回数 + 1 (現在のステートが1つ)
            if (counter.EnterCount != counter.ExitCount + 1)
            {
                throw new InvalidOperationException(
                    $"整合性エラー @ iteration {iteration}: Enter({counter.EnterCount}) != Exit({counter.ExitCount}) + 1");
            }
        }

        #endregion

        #region Simple Context for Comparison

        private class SimpleContext
        {
            public int Value { get; set; }
        }

        private enum SimpleEvent
        {
            Next
        }

        private class SimpleStateA : State<SimpleContext, SimpleEvent>
        {
            public override void Update()
            {
                Context.Value++;
            }
        }

        private class SimpleStateB : State<SimpleContext, SimpleEvent>
        {
            public override void Update()
            {
                Context.Value++;
            }
        }

        private StateMachine<SimpleContext, SimpleEvent> CreateSimpleStateMachine(SimpleContext context)
        {
            var stateMachine = new StateMachine<SimpleContext, SimpleEvent>(context);
            stateMachine.AddTransition<SimpleStateA, SimpleStateB>(SimpleEvent.Next);
            stateMachine.AddTransition<SimpleStateB, SimpleStateA>(SimpleEvent.Next);
            stateMachine.SetInitState<SimpleStateA>();
            return stateMachine;
        }

        #endregion

        #region Old StateMachine States for Comparison

        private abstract class OldGameStateBase : OldState<ComplexGameContext, GameStateEvent>
        {
            protected string StateName => GetType().Name;

            public override void Enter()
            {
                Context.TransitionCounter.RecordEnter(StateName);
                Context.Statistics.RecordStateVisit(StateName);
            }

            public override void Update()
            {
                Context.TransitionCounter.RecordUpdate(StateName);
                Context.Statistics.TotalUpdates++;
            }

            public override void Exit()
            {
                Context.TransitionCounter.RecordExit(StateName);
            }

            protected void PerformComplexCalculation()
            {
                // 複雑な計算をシミュレート（新StateMachineと同じ処理）
                var player = Context.Player;
                var stats = player.Stats;

                float totalStat = 0;
                foreach (var stat in stats.Values)
                {
                    totalStat += stat;
                }

                var power = totalStat * player.Level * 0.1f;
                var defense = stats["Vitality"] * 2 + player.Level;

                // 位置更新
                var direction = new Vector3(
                    (float)(Context.Random.NextDouble() - 0.5) * 2,
                    0,
                    (float)(Context.Random.NextDouble() - 0.5) * 2
                ).normalized;

                player.Position += direction * 0.1f;
            }
        }

        private class OldIdleState : OldGameStateBase
        {
            public override void Update()
            {
                base.Update();
                PerformComplexCalculation();

                // アイドル中はマナ回復
                Context.Player.Mana = Mathf.Min(Context.Player.MaxMana, Context.Player.Mana + 0.1f);
            }
        }

        private class OldCombatState : OldGameStateBase
        {
            private int _combatTurns;

            public override void Enter()
            {
                base.Enter();
                _combatTurns = 0;
            }

            public override void Update()
            {
                base.Update();
                PerformComplexCalculation();

                _combatTurns++;

                // 戦闘シミュレーション
                var damage = Context.Random.Next(5, 20);
                Context.Player.TakeDamage(damage);
                Context.Statistics.TotalDamageTaken += damage;

                var dealDamage = Context.Random.Next(10, 30);
                Context.Statistics.TotalDamageDealt += dealDamage;

                // バフ処理
                if (_combatTurns % 5 == 0 && Context.Player.ActiveBuffs.Count < 5)
                {
                    Context.Player.ActiveBuffs.Add($"CombatBuff_{_combatTurns}");
                }
            }

            public override void Exit()
            {
                base.Exit();
                Context.Player.ActiveBuffs.RemoveAll(b => b.StartsWith("CombatBuff"));
            }
        }

        private class OldVictoryState : OldGameStateBase
        {
            public override void Enter()
            {
                base.Enter();
                Context.Statistics.EnemiesDefeated++;
                Context.Player.GainExperience(50);
                Context.Player.Gold += Context.Random.Next(10, 100);

                // 戦利品追加
                var item = new Item
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"Loot_{Context.Statistics.ItemsCollected}",
                    Weight = (float)Context.Random.NextDouble() * 5,
                    Value = Context.Random.Next(1, 50),
                    Type = (ItemType)Context.Random.Next(0, 5)
                };

                if (Context.Inventory.AddItem(item))
                {
                    Context.Statistics.ItemsCollected++;
                }
            }

            public override void Update()
            {
                base.Update();
                PerformComplexCalculation();
            }
        }

        private class OldDefeatState : OldGameStateBase
        {
            public override void Enter()
            {
                base.Enter();
                Context.Player.Health = Context.Player.MaxHealth * 0.5f;
                Context.Player.Gold = (int)(Context.Player.Gold * 0.9f);
            }

            public override void Update()
            {
                base.Update();
            }
        }

        private class OldInventoryState : OldGameStateBase
        {
            public override void Update()
            {
                base.Update();

                // インベントリ操作シミュレーション
                var items = Context.Inventory.Items;
                if (items.Count > 0)
                {
                    // 重量計算
                    float totalWeight = 0;
                    int totalValue = 0;
                    foreach (var item in items)
                    {
                        totalWeight += item.Weight;
                        totalValue += item.Value;
                    }
                }
            }
        }

        private class OldMenuState : OldGameStateBase
        {
            public override void Update()
            {
                base.Update();
                // メニュー操作シミュレーション
            }
        }

        private class OldRestState : OldGameStateBase
        {
            public override void Update()
            {
                base.Update();

                // 休憩中は回復
                var healing = 5f;
                Context.Player.Heal(healing);
                Context.Statistics.TotalHealing += (int)healing;
                Context.Player.Mana = Mathf.Min(Context.Player.MaxMana, Context.Player.Mana + 2f);
            }
        }

        private class OldTravelState : OldGameStateBase
        {
            public override void Update()
            {
                base.Update();
                PerformComplexCalculation();

                // 移動中のイベント発生
                if (Context.Random.NextDouble() < 0.1)
                {
                    Context.EventQueue.Enqueue(new GameEvent
                    {
                        Type = "RandomEncounter",
                        Timestamp = DateTime.Now
                    });
                }
            }
        }

        private class OldEventState : OldGameStateBase
        {
            public override void Update()
            {
                base.Update();

                // イベント処理
                while (Context.EventQueue.Count > 0)
                {
                    var evt = Context.EventQueue.Dequeue();
                    ProcessEvent(evt);
                }
            }

            private void ProcessEvent(GameEvent evt)
            {
                switch (evt.Type)
                {
                    case "RandomEncounter":
                        Context.Player.TakeDamage(10);
                        Context.Statistics.TotalDamageTaken += 10;
                        break;
                }
            }
        }

        private OldStateMachine<ComplexGameContext, GameStateEvent> CreateOldGameStateMachine(ComplexGameContext context)
        {
            var stateMachine = new OldStateMachine<ComplexGameContext, GameStateEvent>(context);

            // 遷移ルール設定（新StateMachineと同じ構成）
            stateMachine.AddTransition<OldIdleState, OldCombatState>(GameStateEvent.EnterCombat);
            stateMachine.AddTransition<OldIdleState, OldInventoryState>(GameStateEvent.OpenInventory);
            stateMachine.AddTransition<OldIdleState, OldMenuState>(GameStateEvent.OpenMenu);
            stateMachine.AddTransition<OldIdleState, OldRestState>(GameStateEvent.Rest);
            stateMachine.AddTransition<OldIdleState, OldTravelState>(GameStateEvent.Travel);

            stateMachine.AddTransition<OldCombatState, OldVictoryState>(GameStateEvent.Victory);
            stateMachine.AddTransition<OldCombatState, OldDefeatState>(GameStateEvent.Defeat);
            stateMachine.AddTransition<OldCombatState, OldIdleState>(GameStateEvent.ExitCombat);

            stateMachine.AddTransition<OldVictoryState, OldIdleState>(GameStateEvent.Complete);
            stateMachine.AddTransition<OldDefeatState, OldIdleState>(GameStateEvent.Complete);

            stateMachine.AddTransition<OldInventoryState, OldIdleState>(GameStateEvent.CloseInventory);
            stateMachine.AddTransition<OldMenuState, OldIdleState>(GameStateEvent.CloseMenu);
            stateMachine.AddTransition<OldRestState, OldIdleState>(GameStateEvent.Complete);

            stateMachine.AddTransition<OldTravelState, OldIdleState>(GameStateEvent.Complete);
            stateMachine.AddTransition<OldTravelState, OldCombatState>(GameStateEvent.EnterCombat);
            stateMachine.AddTransition<OldTravelState, OldEventState>(GameStateEvent.TriggerEvent);

            stateMachine.AddTransition<OldEventState, OldIdleState>(GameStateEvent.Complete);
            stateMachine.AddTransition<OldEventState, OldCombatState>(GameStateEvent.EnterCombat);

            stateMachine.SetInitState<OldIdleState>();

            return stateMachine;
        }

        #endregion

        #region Old vs New StateMachine Comparison Tests

        [Test]
        public void Comparison_OldVsNew_TransitionTiming()
        {
            const int iterations = 50000;
            var transitionStopwatch = new Stopwatch();

            Log("# 旧StateMachine vs 新StateMachine 遷移時間比較");
            Log("");
            Log($"- イテレーション数: {iterations:N0}");
            Log("");

            // ===== 両方のStateMachineを作成 =====
            var oldContext = new ComplexGameContext(seed: 12345);
            var oldStateMachine = CreateOldGameStateMachine(oldContext);

            var newContext = new ComplexGameContext(seed: 12345);
            var newStateMachine = CreateGameStateMachine(newContext);

            // ===== 両方をウォームアップ（JITコンパイル完了させる） =====
            oldStateMachine.Update();
            for (int i = 0; i < WarmupIterations; i++)
            {
                ExecuteOldTransitionCycleWithTiming(oldStateMachine, oldContext, transitionStopwatch);
            }

            newStateMachine.Update();
            for (int i = 0; i < WarmupIterations; i++)
            {
                ExecuteTransitionCycleWithTiming(newStateMachine, newContext, transitionStopwatch);
            }

            // ===== 旧StateMachineの計測 =====
            oldContext.Reset();
            oldContext.TransitionCounter.Reset();
            oldStateMachine.Update();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var oldStopwatch = new Stopwatch();
            long oldStartMemory = GC.GetTotalMemory(true);
            int oldStartGcCount = GC.CollectionCount(0);

            oldStopwatch.Start();
            for (int i = 0; i < iterations; i++)
            {
                ExecuteOldTransitionCycleWithTiming(oldStateMachine, oldContext, transitionStopwatch);
            }
            oldStopwatch.Stop();

            long oldEndMemory = GC.GetTotalMemory(false);
            int oldEndGcCount = GC.CollectionCount(0);

            double oldTotalMs = oldStopwatch.Elapsed.TotalMilliseconds;
            double oldAvgTransitionTimeMs = oldContext.TransitionCounter.GetAverageTransitionTimeMs();
            double oldMaxTransitionTimeMs = oldContext.TransitionCounter.MaxTransitionTimeMs;
            int oldSuccessCount = oldContext.TransitionCounter.SuccessCount;
            long oldMemoryAllocated = oldEndMemory - oldStartMemory;
            int oldGcCount = oldEndGcCount - oldStartGcCount;

            // ===== 新StateMachineの計測 =====
            newContext.Reset();
            newContext.TransitionCounter.Reset();
            newStateMachine.Update();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var newStopwatch = new Stopwatch();
            long newStartMemory = GC.GetTotalMemory(true);
            int newStartGcCount = GC.CollectionCount(0);

            newStopwatch.Start();
            for (int i = 0; i < iterations; i++)
            {
                ExecuteTransitionCycleWithTiming(newStateMachine, newContext, transitionStopwatch);
            }
            newStopwatch.Stop();

            long newEndMemory = GC.GetTotalMemory(false);
            int newEndGcCount = GC.CollectionCount(0);

            double newTotalMs = newStopwatch.Elapsed.TotalMilliseconds;
            double newAvgTransitionTimeMs = newContext.TransitionCounter.GetAverageTransitionTimeMs();
            double newMaxTransitionTimeMs = newContext.TransitionCounter.MaxTransitionTimeMs;
            int newSuccessCount = newContext.TransitionCounter.SuccessCount;
            long newMemoryAllocated = newEndMemory - newStartMemory;
            int newGcCount = newEndGcCount - newStartGcCount;

            // 結果出力 (Markdown形式)
            var memoryImprovement = oldMemoryAllocated > 0 && newMemoryAllocated > 0
                ? $"{((double)oldMemoryAllocated / newMemoryAllocated):F2}x"
                : "N/A";

            Log("## 遷移タイミング比較結果");
            Log("");
            Log("| 項目 | 旧StateMachine | 新StateMachine | 改善率 |");
            Log("|:-----|---------------:|---------------:|-------:|");
            Log($"| 総実行時間 (ms) | {oldTotalMs:F3} | {newTotalMs:F3} | {(oldTotalMs / newTotalMs):F2}x |");
            Log($"| 平均遷移時間 (μs) | {oldAvgTransitionTimeMs * 1000:F3} | {newAvgTransitionTimeMs * 1000:F3} | {(oldAvgTransitionTimeMs / newAvgTransitionTimeMs):F2}x |");
            Log($"| 最大遷移時間 (μs) | {oldMaxTransitionTimeMs * 1000:F3} | {newMaxTransitionTimeMs * 1000:F3} | {(oldMaxTransitionTimeMs / newMaxTransitionTimeMs):F2}x |");
            Log($"| 遷移スループット (ops/s) | {oldSuccessCount / (oldTotalMs / 1000.0):N0} | {newSuccessCount / (newTotalMs / 1000.0):N0} | {((newSuccessCount / (newTotalMs / 1000.0)) / (oldSuccessCount / (oldTotalMs / 1000.0))):F2}x |");
            Log($"| メモリ (bytes) | {oldMemoryAllocated:N0} | {newMemoryAllocated:N0} | {memoryImprovement} |");
            Log($"| GC発生回数 | {oldGcCount} | {newGcCount} | {oldGcCount - newGcCount} |");

            Assert.Pass($"新StateMachineは旧StateMachineより {(oldTotalMs / newTotalMs):F2}x 高速");
        }

        [Test]
        public void Comparison_OldVsNew_UpdateThroughput()
        {
            const int iterations = 100000;

            Log("# 旧StateMachine vs 新StateMachine Update スループット比較");
            Log("");
            Log($"- イテレーション数: {iterations:N0}");
            Log("");

            // ===== 両方のStateMachineを作成 =====
            var oldContext = new ComplexGameContext(seed: 12345);
            var oldStateMachine = CreateOldGameStateMachine(oldContext);

            var newContext = new ComplexGameContext(seed: 12345);
            var newStateMachine = CreateGameStateMachine(newContext);

            // ===== 両方をウォームアップ（JITコンパイル完了させる） =====
            for (int i = 0; i < WarmupIterations; i++)
            {
                oldStateMachine.Update();
            }

            for (int i = 0; i < WarmupIterations; i++)
            {
                newStateMachine.Update();
            }

            // ===== 旧StateMachineの計測 =====
            oldContext.Reset();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var oldStopwatch = new Stopwatch();
            long oldStartMemory = GC.GetTotalMemory(true);
            int oldStartGcCount = GC.CollectionCount(0);

            oldStopwatch.Start();
            for (int i = 0; i < iterations; i++)
            {
                oldStateMachine.Update();
            }
            oldStopwatch.Stop();

            long oldEndMemory = GC.GetTotalMemory(false);
            int oldEndGcCount = GC.CollectionCount(0);

            double oldTotalMs = oldStopwatch.Elapsed.TotalMilliseconds;
            double oldOpsPerSec = iterations / (oldTotalMs / 1000.0);
            long oldMemoryAllocated = oldEndMemory - oldStartMemory;
            int oldGcCount = oldEndGcCount - oldStartGcCount;

            // ===== 新StateMachineの計測 =====
            newContext.Reset();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var newStopwatch = new Stopwatch();
            long newStartMemory = GC.GetTotalMemory(true);
            int newStartGcCount = GC.CollectionCount(0);

            newStopwatch.Start();
            for (int i = 0; i < iterations; i++)
            {
                newStateMachine.Update();
            }
            newStopwatch.Stop();

            long newEndMemory = GC.GetTotalMemory(false);
            int newEndGcCount = GC.CollectionCount(0);

            double newTotalMs = newStopwatch.Elapsed.TotalMilliseconds;
            double newOpsPerSec = iterations / (newTotalMs / 1000.0);
            long newMemoryAllocated = newEndMemory - newStartMemory;
            int newGcCount = newEndGcCount - newStartGcCount;

            // 結果出力 (Markdown形式)
            var memoryImprovement = oldMemoryAllocated > 0 && newMemoryAllocated > 0
                ? $"{((double)oldMemoryAllocated / newMemoryAllocated):F2}x"
                : "N/A";

            Log("## Updateスループット比較結果");
            Log("");
            Log("| 項目 | 旧StateMachine | 新StateMachine | 改善率 |");
            Log("|:-----|---------------:|---------------:|-------:|");
            Log($"| 総実行時間 (ms) | {oldTotalMs:F3} | {newTotalMs:F3} | {(oldTotalMs / newTotalMs):F2}x |");
            Log($"| 平均時間/Update (ms) | {oldTotalMs / iterations:F6} | {newTotalMs / iterations:F6} | {(oldTotalMs / newTotalMs):F2}x |");
            Log($"| スループット (ops/s) | {oldOpsPerSec:N0} | {newOpsPerSec:N0} | {(newOpsPerSec / oldOpsPerSec):F2}x |");
            Log($"| メモリ (bytes) | {oldMemoryAllocated:N0} | {newMemoryAllocated:N0} | {memoryImprovement} |");
            Log($"| GC発生回数 | {oldGcCount} | {newGcCount} | {oldGcCount - newGcCount} |");

            Assert.That(newOpsPerSec, Is.GreaterThanOrEqualTo(oldOpsPerSec * 0.9),
                "新StateMachineのスループットが旧StateMachineより10%以上低下しています");
            Assert.Pass($"新StateMachineは旧StateMachineより {(newOpsPerSec / oldOpsPerSec):F2}x スループット向上");
        }

        [Test]
        public void Comparison_OldVsNew_MemoryAllocation()
        {
            const int iterations = 10000;

            Log("# 旧StateMachine vs 新StateMachine メモリアロケーション比較");
            Log("");
            Log($"- イテレーション数: {iterations:N0}");
            Log("");

            // ===== 両方のStateMachineを作成 =====
            var oldContext = new ComplexGameContext(seed: 12345);
            var oldStateMachine = CreateOldGameStateMachine(oldContext);

            var newContext = new ComplexGameContext(seed: 12345);
            var newStateMachine = CreateGameStateMachine(newContext);

            // ===== 両方をウォームアップ（JITコンパイル完了させる） =====
            oldStateMachine.Update();
            for (int i = 0; i < 100; i++)
            {
                ExecuteOldTransitionCycle(oldStateMachine);
            }

            newStateMachine.Update();
            for (int i = 0; i < 100; i++)
            {
                ExecuteTransitionCycle(newStateMachine);
            }

            // ===== 旧StateMachineの計測 =====
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            int oldGen0Before = GC.CollectionCount(0);
            int oldGen1Before = GC.CollectionCount(1);
            int oldGen2Before = GC.CollectionCount(2);
            long oldMemoryBefore = GC.GetTotalMemory(true);

            for (int i = 0; i < iterations; i++)
            {
                oldStateMachine.Update();
                ExecuteOldTransitionCycle(oldStateMachine);
            }

            int oldGen0After = GC.CollectionCount(0);
            int oldGen1After = GC.CollectionCount(1);
            int oldGen2After = GC.CollectionCount(2);
            long oldMemoryAfter = GC.GetTotalMemory(false);

            long oldMemoryAllocated = oldMemoryAfter - oldMemoryBefore;
            int oldGen0Collections = oldGen0After - oldGen0Before;
            int oldGen1Collections = oldGen1After - oldGen1Before;
            int oldGen2Collections = oldGen2After - oldGen2Before;

            // ===== 新StateMachineの計測 =====
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            int newGen0Before = GC.CollectionCount(0);
            int newGen1Before = GC.CollectionCount(1);
            int newGen2Before = GC.CollectionCount(2);
            long newMemoryBefore = GC.GetTotalMemory(true);

            for (int i = 0; i < iterations; i++)
            {
                newStateMachine.Update();
                ExecuteTransitionCycle(newStateMachine);
            }

            int newGen0After = GC.CollectionCount(0);
            int newGen1After = GC.CollectionCount(1);
            int newGen2After = GC.CollectionCount(2);
            long newMemoryAfter = GC.GetTotalMemory(false);

            long newMemoryAllocated = newMemoryAfter - newMemoryBefore;
            int newGen0Collections = newGen0After - newGen0Before;
            int newGen1Collections = newGen1After - newGen1Before;
            int newGen2Collections = newGen2After - newGen2Before;

            // 結果出力 (Markdown形式)
            string memoryImprovement;
            if (oldMemoryAllocated > 0 && newMemoryAllocated > 0)
            {
                memoryImprovement = $"{((double)oldMemoryAllocated / newMemoryAllocated):F2}x";
            }
            else if (oldMemoryAllocated > 0 && newMemoryAllocated <= 0)
            {
                memoryImprovement = "∞ (アロケーションなし)";
            }
            else
            {
                memoryImprovement = "N/A";
            }

            Log("## メモリアロケーション比較結果");
            Log("");
            Log("| 項目 | 旧StateMachine | 新StateMachine | 改善率 |");
            Log("|:-----|---------------:|---------------:|-------:|");
            Log($"| メモリ (bytes) | {oldMemoryAllocated:N0} | {newMemoryAllocated:N0} | {memoryImprovement} |");
            Log($"| バイト/イテレーション | {(double)oldMemoryAllocated / iterations:F2} | {(double)newMemoryAllocated / iterations:F2} | {memoryImprovement} |");
            Log($"| GC Gen0 | {oldGen0Collections} | {newGen0Collections} | {oldGen0Collections - newGen0Collections} |");
            Log($"| GC Gen1 | {oldGen1Collections} | {newGen1Collections} | {oldGen1Collections - newGen1Collections} |");
            Log($"| GC Gen2 | {oldGen2Collections} | {newGen2Collections} | {oldGen2Collections - newGen2Collections} |");

            // 新StateMachineは旧StateMachine以下のメモリアロケーションであること
            Assert.That(newMemoryAllocated, Is.LessThanOrEqualTo(oldMemoryAllocated + 1000),
                "新StateMachineのメモリアロケーションが旧StateMachineより大幅に増加しています");
            Assert.Pass($"メモリアロケーション: 旧={oldMemoryAllocated:N0} bytes, 新={newMemoryAllocated:N0} bytes");
        }

        [Test]
        public void Comparison_OldVsNew_ComprehensiveBenchmark()
        {
            const int iterations = 30000;
            // 最大遷移時間計測用のリストを事前に確保（List拡張によるGCを防止）
            const int expectedTransitions = iterations / 10 * 3; // 約9000回の遷移

            Log("# 旧StateMachine vs 新StateMachine 総合ベンチマーク");
            Log("");
            Log($"- イテレーション数: {iterations:N0}");
            Log("");

            var transitionStopwatch = new Stopwatch();

            // ===== 両方のStateMachineを作成 =====
            var oldContext = new ComplexGameContext(seed: 12345);
            var oldStateMachine = CreateOldGameStateMachine(oldContext);

            var newContext = new ComplexGameContext(seed: 12345);
            var newStateMachine = CreateGameStateMachine(newContext);

            // ===== 両方をウォームアップ（JITコンパイル完了させる） =====
            oldStateMachine.Update();
            for (int i = 0; i < WarmupIterations; i++)
            {
                oldStateMachine.Update();
                ExecuteOldTransitionCycle(oldStateMachine);
            }

            newStateMachine.Update();
            for (int i = 0; i < WarmupIterations; i++)
            {
                newStateMachine.Update();
                ExecuteTransitionCycle(newStateMachine);
            }

            // ===== 旧StateMachineの計測 =====
            oldContext.Reset();
            oldContext.TransitionCounter.Reset();
            oldContext.TransitionCounter.TransitionTimesMs.Capacity = expectedTransitions;
            oldStateMachine.Update();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            int oldGen0Before = GC.CollectionCount(0);
            long oldMemoryBefore = GC.GetTotalMemory(true);

            var oldStopwatch = new Stopwatch();
            oldStopwatch.Start();

            int oldTotalTransitions = 0;
            for (int i = 0; i < iterations; i++)
            {
                oldStateMachine.Update();

                if (i % 10 == 0)
                {
                    oldTotalTransitions += ExecuteOldTransitionCycleWithTiming(oldStateMachine, oldContext, transitionStopwatch);
                }

                if (i % 2 == 0)
                {
                    oldStateMachine.FixedUpdate();
                }
                oldStateMachine.LateUpdate();
            }

            oldStopwatch.Stop();

            int oldGen0After = GC.CollectionCount(0);
            long oldMemoryAfter = GC.GetTotalMemory(false);

            double oldTotalMs = oldStopwatch.Elapsed.TotalMilliseconds;
            double oldAvgTransitionTimeMs = oldContext.TransitionCounter.GetAverageTransitionTimeMs();
            double oldP99TransitionTimeMs = oldContext.TransitionCounter.GetPercentileTransitionTimeMs(0.99);
            double oldMaxTransitionTimeMs = oldContext.TransitionCounter.MaxTransitionTimeMs;
            double oldThroughput = iterations / (oldTotalMs / 1000.0);
            double oldTransitionThroughput = oldContext.TransitionCounter.SuccessCount / (oldTotalMs / 1000.0);
            long oldMemoryAllocated = oldMemoryAfter - oldMemoryBefore;
            int oldGcCount = oldGen0After - oldGen0Before;

            // ===== 新StateMachineの計測 =====
            newContext.Reset();
            newContext.TransitionCounter.Reset();
            newContext.TransitionCounter.TransitionTimesMs.Capacity = expectedTransitions;
            newStateMachine.Update();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            int newGen0Before = GC.CollectionCount(0);
            long newMemoryBefore = GC.GetTotalMemory(true);

            var newStopwatch = new Stopwatch();
            newStopwatch.Start();

            int newTotalTransitions = 0;
            for (int i = 0; i < iterations; i++)
            {
                newStateMachine.Update();

                if (i % 10 == 0)
                {
                    newTotalTransitions += ExecuteTransitionCycleWithTiming(newStateMachine, newContext, transitionStopwatch);
                }

                if (i % 2 == 0)
                {
                    newStateMachine.FixedUpdate();
                }
                newStateMachine.LateUpdate();
            }

            newStopwatch.Stop();

            int newGen0After = GC.CollectionCount(0);
            long newMemoryAfter = GC.GetTotalMemory(false);

            double newTotalMs = newStopwatch.Elapsed.TotalMilliseconds;
            double newAvgTransitionTimeMs = newContext.TransitionCounter.GetAverageTransitionTimeMs();
            double newP99TransitionTimeMs = newContext.TransitionCounter.GetPercentileTransitionTimeMs(0.99);
            double newMaxTransitionTimeMs = newContext.TransitionCounter.MaxTransitionTimeMs;
            double newThroughput = iterations / (newTotalMs / 1000.0);
            double newTransitionThroughput = newContext.TransitionCounter.SuccessCount / (newTotalMs / 1000.0);
            long newMemoryAllocated = newMemoryAfter - newMemoryBefore;
            int newGcCount = newGen0After - newGen0Before;

            // ===== 結果出力 (Markdown形式) =====
            Log("## 総合ベンチマーク結果");
            Log("");
            Log("| 項目 | 旧StateMachine | 新StateMachine | 改善率 |");
            Log("|:-----|---------------:|---------------:|-------:|");
            Log($"| 総実行時間 (ms) | {oldTotalMs:F3} | {newTotalMs:F3} | {(oldTotalMs / newTotalMs):F2}x |");
            Log($"| 平均遷移時間 (μs) | {oldAvgTransitionTimeMs * 1000:F3} | {newAvgTransitionTimeMs * 1000:F3} | {(oldAvgTransitionTimeMs / newAvgTransitionTimeMs):F2}x |");
            Log($"| P99遷移時間 (μs) | {oldP99TransitionTimeMs * 1000:F3} | {newP99TransitionTimeMs * 1000:F3} | {(oldP99TransitionTimeMs / newP99TransitionTimeMs):F2}x |");
            Log($"| 最大遷移時間 (μs) | {oldMaxTransitionTimeMs * 1000:F3} | {newMaxTransitionTimeMs * 1000:F3} | {(oldMaxTransitionTimeMs / newMaxTransitionTimeMs):F2}x |");
            Log($"| スループット (ops/s) | {oldThroughput:N0} | {newThroughput:N0} | {(newThroughput / oldThroughput):F2}x |");
            Log($"| 遷移/秒 | {oldTransitionThroughput:N0} | {newTransitionThroughput:N0} | {(newTransitionThroughput / oldTransitionThroughput):F2}x |");
            Log($"| メモリ (bytes) | {oldMemoryAllocated:N0} | {newMemoryAllocated:N0} | {(oldMemoryAllocated > 0 && newMemoryAllocated > 0 ? ((double)oldMemoryAllocated / newMemoryAllocated).ToString("F2") : "N/A")}x |");
            Log($"| GC発生回数 | {oldGcCount:N0} | {newGcCount:N0} | {(oldGcCount - newGcCount):N0} |");

            Log("");
            Log("### 詳細");
            Log($"- 遷移回数: 旧={oldTotalTransitions:N0}, 新={newTotalTransitions:N0}");
            Log($"- 成功遷移数: 旧={oldContext.TransitionCounter.SuccessCount:N0}, 新={newContext.TransitionCounter.SuccessCount:N0}");

            Assert.Pass($"総合ベンチマーク完了 - {(oldTotalMs / newTotalMs):F2}x 高速化");
        }

        #endregion

        #region Old StateMachine Helper Methods

        private int ExecuteOldTransitionCycle(OldStateMachine<ComplexGameContext, GameStateEvent> stateMachine)
        {
            int transitions = 0;

            // Idle -> Combat -> Victory -> Idle
            transitions += TryOldTransition(stateMachine, GameStateEvent.EnterCombat);
            transitions += TryOldTransition(stateMachine, GameStateEvent.Victory);
            transitions += TryOldTransition(stateMachine, GameStateEvent.Complete);

            return transitions;
        }

        private int TryOldTransition(
            OldStateMachine<ComplexGameContext, GameStateEvent> stateMachine,
            GameStateEvent evt)
        {
            var result = stateMachine.Transition(evt);

            switch (result)
            {
                case OldStateEventResult.Succeeded:
                    stateMachine.Update();
                    return 1;

                case OldStateEventResult.Waiting:
                    while (stateMachine.IsProcessing())
                    {
                        stateMachine.Update();
                    }
                    return 1;

                case OldStateEventResult.Failed:
                default:
                    return 0;
            }
        }

        private int ExecuteOldTransitionCycleWithTiming(
            OldStateMachine<ComplexGameContext, GameStateEvent> stateMachine,
            ComplexGameContext context,
            Stopwatch stopwatch)
        {
            int transitions = 0;

            // Idle -> Combat -> Victory -> Idle
            transitions += TryOldTransitionWithTiming(stateMachine, context, GameStateEvent.EnterCombat, stopwatch);
            transitions += TryOldTransitionWithTiming(stateMachine, context, GameStateEvent.Victory, stopwatch);
            transitions += TryOldTransitionWithTiming(stateMachine, context, GameStateEvent.Complete, stopwatch);

            return transitions;
        }

        private int TryOldTransitionWithTiming(
            OldStateMachine<ComplexGameContext, GameStateEvent> stateMachine,
            ComplexGameContext context,
            GameStateEvent evt,
            Stopwatch stopwatch)
        {
            var counter = context.TransitionCounter;

            stopwatch.Restart();
            var result = stateMachine.Transition(evt);
            stopwatch.Stop();

            double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            switch (result)
            {
                case OldStateEventResult.Succeeded:
                    counter.RecordTransitionResult(true, elapsedMs);
                    stateMachine.Update();
                    return 1;

                case OldStateEventResult.Waiting:
                    counter.RecordTransitionResult(true, elapsedMs);
                    while (stateMachine.IsProcessing())
                    {
                        stateMachine.Update();
                    }
                    return 1;

                case OldStateEventResult.Failed:
                default:
                    counter.RecordTransitionResult(false, elapsedMs);
                    return 0;
            }
        }

        #endregion
    }
}