using System.Collections.Generic;
using System.Linq;
using Game.Client.MasterData;
using Game.MVP.Survivor.SaveData;

namespace Game.MVP.Survivor.Scenes.ViewModels
{
    /// <summary>
    /// ステージ選択シーンのUIロジック（テスト可能な純粋C#クラス）
    /// マスタデータ＋セーブデータ → 表示用データへの変換を担当
    /// </summary>
    public class StageSelectSceneViewModel
    {
        public List<StageSelectItemData> BuildStageItems(
            IEnumerable<SurvivorStageMaster> stages,
            SurvivorSaveData saveData)
        {
            return stages
                .OrderBy(s => s.Id)
                .Select(stage => new StageSelectItemData
                {
                    StageId = stage.Id,
                    StageName = stage.Name,
                    Description = stage.Description,
                    Difficulty = stage.Difficulty,
                    TimeLimit = stage.TimeLimit,
                    IsUnlocked = saveData.UnlockedStageIds.Contains(stage.Id),
                    Record = saveData.StageRecords.GetValueOrDefault(stage.Id)
                })
                .ToList();
        }
    }
}
