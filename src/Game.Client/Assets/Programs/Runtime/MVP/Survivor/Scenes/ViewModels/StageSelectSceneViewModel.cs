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
                    // TODO(アンロック機構の再設計): アンロック状態はサーバー (PostgreSQL) で管理すべきだが、
                    // 現状ローカルセーブデータが源泉になっており、クライアント起点の不正操作で状態が壊れる
                    // 構造的不具合がある。現状のアンロック機構は障害でしかないため、サーバー側と同期する
                    // 正しい実装が入るまで全ステージを常にアンロック扱いとする。
                    IsUnlocked = true,
                    Record = saveData.StageRecords.GetValueOrDefault(stage.Id)
                })
                .ToList();
        }
    }
}
