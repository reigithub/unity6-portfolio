using System.Collections.Generic;
using Game.Horror.SaveData;
using Game.Shared.Enums;
using Game.Shared.Services.Interfaces;

namespace Game.Horror.Services.Interfaces
{
    public interface IHorrorKeyItemService : IGameService
    {
        IReadOnlyList<HorrorKeyItemData> KeyItems { get; }

        bool TryAdd(ObjectCategory category, int id, int addCount);

        bool HasObject(ObjectCategory category, int id);
    }
}
