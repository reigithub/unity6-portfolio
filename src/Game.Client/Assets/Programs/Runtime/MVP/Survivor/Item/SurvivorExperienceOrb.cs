using System;
using UnityEngine;

namespace Game.MVP.Survivor.Item
{
    /// <summary>
    /// 経験値オーブ（後方互換性用エイリアス）
    /// 新規実装ではSurvivorItemを使用してください
    /// </summary>
    [Obsolete("Use SurvivorItem instead. This class is kept for backward compatibility.")]
    public class SurvivorExperienceOrb : SurvivorItem
    {
        // SurvivorItemを継承し、経験値専用の初期化を行う
        private void Awake()
        {
            // 経験値タイプとして初期化
            if (ItemType == SurvivorItemType.None)
            {
                InitializeAsExperience(ExperienceValue > 0 ? ExperienceValue : 5);
            }
        }
    }
}
