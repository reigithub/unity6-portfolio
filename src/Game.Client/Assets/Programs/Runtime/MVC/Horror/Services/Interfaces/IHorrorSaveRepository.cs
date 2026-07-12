using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Horror.SaveData;
using Game.Shared.SaveData;

namespace Game.Horror.Services.Interfaces
{
    public interface IHorrorSaveRepository : ISaveRepository<HorrorSaveData>
    {
        /// <summary>現在アクティブなスロット番号。</summary>
        int CurrentSlot { get; }

        /// <summary>
        /// 全スロットのメタ情報を走査して取得する。現在ロード中の <see cref="ISaveRepository{TData}.Data"/> は変更しない。
        /// </summary>
        UniTask<IReadOnlyList<HorrorSaveSlotInfo>> LoadSlotInfosAsync();

        /// <summary>
        /// 指定スロットへ保存する。範囲外のスロット番号は保存を行わない。
        /// スロットメタ（スロット番号・保存日時・セーブポイント Id）は保存直前に刻印される。
        /// </summary>
        /// <param name="slotNumber">保存先スロット番号（1〜スロット数上限）。</param>
        UniTask SaveToSlotAsync(int slotNumber);
    }
}
