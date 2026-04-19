using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Tests.PlayMode.Performance
{
    /// <summary>
    /// PlayMode パフォーマンスベンチ共通基底。
    /// ログ出力・targetFrameRate / vSync 設定を揃え、テスト間の環境差を排除する。
    /// </summary>
    public abstract class PlayModeBenchmarkTestBase
    {
        protected StringBuilder LogBuilder;
        protected string LogFilePath;

        // 複数 Performance fixture 間の相互汚染を防ぐため、最外 fixture で 1 度だけ save / restore する。
        // 個々 fixture ごとに save すると、他 fixture が既に変更後の値を読み取り、
        // 最後の TearDown が -1 / 0 のまま放置され、PlayerMovementTests 等の後続テストが壊れる。
        private static int s_fixtureNestLevel;
        private static int s_savedTargetFrameRate;
        private static int s_savedVSyncCount;

        protected bool IsBatchMode => Application.isBatchMode;

        [OneTimeSetUp]
        public void BenchOneTimeSetUp()
        {
            var logDir = Path.Combine(Application.dataPath, "..", "Logs", "PerformanceTests");
            Directory.CreateDirectory(logDir);
            LogFilePath = Path.Combine(logDir,
                $"{GetType().Name}_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            // Warmup ノイズ排除: 最初の fixture でのみオリジナル値を保存し、-1 / 0 に設定
            if (s_fixtureNestLevel == 0)
            {
                s_savedTargetFrameRate = Application.targetFrameRate;
                s_savedVSyncCount = QualitySettings.vSyncCount;
                Application.targetFrameRate = -1;
                QualitySettings.vSyncCount = 0;
            }
            s_fixtureNestLevel++;
        }

        [OneTimeTearDown]
        public void BenchOneTimeTearDown()
        {
            s_fixtureNestLevel--;
            // 最後の fixture でのみオリジナル値に戻す（後続 PlayMode テストへの残留回避）
            if (s_fixtureNestLevel <= 0)
            {
                s_fixtureNestLevel = 0;
                Application.targetFrameRate = s_savedTargetFrameRate;
                QualitySettings.vSyncCount = s_savedVSyncCount;
            }
        }

        [SetUp]
        public void BenchSetUp()
        {
            LogBuilder = new StringBuilder();
        }

        [TearDown]
        public void BenchTearDown()
        {
            if (LogBuilder != null && LogBuilder.Length > 0)
            {
                var content = LogBuilder.ToString();
                Debug.Log(content);
                File.AppendAllText(LogFilePath, content + "\n");
            }
        }
    }
}
