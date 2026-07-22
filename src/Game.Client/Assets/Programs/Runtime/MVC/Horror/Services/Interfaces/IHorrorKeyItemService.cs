using System.Collections.Generic;
using Game.Horror.SaveData;
using Game.Shared.Enums;
using Game.Shared.Interfaces;
using Game.Shared.Services.Interfaces;

namespace Game.Horror.Services.Interfaces
{
    public interface IHorrorKeyItemService : IGameService
    {
        IReadOnlyList<HorrorKeyItemData> KeyItems { get; }

        bool TryAdd(IObjectInfo info, int addCount);

        bool HasItem(ObjectCategory category, int id);
    }
}
