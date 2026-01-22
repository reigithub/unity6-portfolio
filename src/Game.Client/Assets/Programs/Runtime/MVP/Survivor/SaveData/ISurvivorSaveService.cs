using Cysharp.Threading.Tasks;

namespace Game.MVP.Survivor.SaveData
{
    /// <summary>
    /// Survivorセーブデータサービスインターフェース
    /// </summary>
    public interface ISurvivorSaveService
    {
        #region 基本操作

        /// <summary>現在のセーブデータ（読み取り専用）</summary>
        SurvivorSaveData Data { get; }

        /// <summary>データが読み込み済みか</summary>
        bool IsLoaded { get; }

        /// <summary>未保存の変更があるか</summary>
        bool IsDirty { get; }

        /// <summary>セーブデータ読み込み</summary>
        UniTask LoadAsync();

        /// <summary>セーブデータ保存</summary>
        UniTask SaveAsync();

        /// <summary>変更がある場合のみ保存</summary>
        UniTask SaveIfDirtyAsync();

        /// <summary>セーブデータを削除（リセット）</summary>
        UniTask DeleteAsync();

        #endregion

        #region ステージ記録

        /// <summary>ステージクリア記録を更新</summary>
        /// <param name="isTimeUp">時間切れでクリアしたか（時間切れの場合は強制1星）</param>
        /// <param name="hpRatio">最終HP割合（0.0〜1.0）</param>
        void RecordStageClear(int stageId, int score, float clearTime, int kills, bool isVictory, bool isTimeUp = false, float hpRatio = 1f);

        /// <summary>ステージをアンロック</summary>
        void UnlockStage(int stageId);

        /// <summary>ステージがアンロック済みか確認</summary>
        bool IsStageUnlocked(int stageId);

        /// <summary>ステージのクリア記録を取得</summary>
        SurvivorStageClearRecord GetStageRecord(int stageId);

        /// <summary>ステージのクリア記録を削除</summary>
        void DeleteStageRecord(int stageId);

        #endregion

        #region セッション管理

        /// <summary>現在のステージセッション</summary>
        SurvivorStageSession CurrentSession { get; }

        /// <summary>アクティブなセッションが存在するか</summary>
        bool HasActiveSession { get; }

        /// <summary>
        /// 新しいセッションを開始
        /// </summary>
        /// <param name="stageId">ステージID</param>
        /// <param name="playerId">プレイヤーID</param>
        /// <param name="stageGroupId">ステージグループID（0 = 単発）</param>
        void StartSession(int stageId, int playerId, int stageGroupId = 0);

        /// <summary>
        /// セッションの状態を更新（自動保存用）
        /// </summary>
        void UpdateSession(
            int currentWave,
            float elapsedTime,
            int currentHp,
            int experience,
            int level,
            int score,
            int totalKills);

        /// <summary>
        /// 現在のステージ結果を記録（クリア/ゲームオーバー時）
        /// </summary>
        /// <param name="isTimeUp">時間切れでクリアしたか（時間切れの場合は強制1星）</param>
        /// <param name="hpRatio">最終HP割合（0.0〜1.0）</param>
        void CompleteCurrentStage(int score, int kills, float clearTime, bool isVictory, bool isTimeUp = false, float hpRatio = 1f);

        /// <summary>
        /// グループ内の次のステージに進む
        /// </summary>
        /// <param name="nextStageId">次のステージID</param>
        void AdvanceToNextStage(int nextStageId);

        /// <summary>
        /// セッションを終了（リザルト画面終了後）
        /// </summary>
        void EndSession();

        #endregion

        #region プレイヤー設定

        /// <summary>選択中のプレイヤーIDを設定</summary>
        void SetSelectedPlayerId(int playerId);

        /// <summary>プレイ時間を加算</summary>
        void AddPlayTime(float seconds);

        #endregion
    }
}
