using System;
using System.Collections.Generic;

namespace Game.Shared.Dto.Survivor
{
    // ============================================================
    // Request DTOs
    // ============================================================

    /// <summary>
    /// スコア送信リクエスト
    /// </summary>
    [Serializable]
    public class SubmitSurvivorScoreRequest
    {
        public int stageId;
        public int score;
        public float clearTime;
        public int waveReached;
        public int enemiesDefeated;
    }

    // ============================================================
    // Response DTOs
    // ============================================================

    /// <summary>
    /// スコア送信レスポンス
    /// </summary>
    [Serializable]
    public class SurvivorScoreSubmitResponse
    {
        public long scoreId;
        public bool isNewBest;
        public int currentRank;
    }

    /// <summary>
    /// ランキングレスポンス
    /// </summary>
    [Serializable]
    public class RankingResponse
    {
        public int stageId;
        public int totalCount;
        public List<RankingEntry> entries;
    }

    /// <summary>
    /// ランキングエントリ
    /// </summary>
    [Serializable]
    public class RankingEntry
    {
        public int rank;
        public string userId;
        public string userName;
        public int score;
        public float clearTime;
        public long recordedAt;
    }
}
