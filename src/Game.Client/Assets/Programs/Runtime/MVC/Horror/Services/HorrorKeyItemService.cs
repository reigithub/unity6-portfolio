using System;
using System.Collections.Generic;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;

namespace Game.Horror.Services
{
    public class HorrorKeyItemService : IHorrorKeyItemService
    {
        private readonly IHorrorSaveRepository _repository;

        public IReadOnlyList<HorrorKeyItemData> KeyItems => _repository.Data?.KeyItem?.KeyItems ?? _emptyItems;
        private readonly IReadOnlyList<HorrorKeyItemData> _emptyItems = Array.Empty<HorrorKeyItemData>();

        public HorrorKeyItemService(IHorrorSaveRepository repository)
        {
            _repository = repository;
        }

        public bool TryAdd(ObjectCategory category, int id, int addCount)
        {
            var data = _repository.Data?.KeyItem;
            if (data == null || addCount <= 0)
                return false;

            if (TryGet(data, category, id, out _))
            {
                return false;
            }
            else
            {
                data.KeyItems.Add(new HorrorKeyItemData
                {
                    ObjectCategory = category,
                    Id = id
                });
            }

            _repository.MarkDirty();
            return true;
        }

        private static bool TryGet(HorrorKeyItemSaveData data, ObjectCategory category, int id, out HorrorKeyItemData item)
        {
            foreach (var keyItem in data.KeyItems)
            {
                if (keyItem.ObjectCategory == category && keyItem.Id == id)
                {
                    item = keyItem;
                    return true;
                }
            }

            item = null;
            return false;
        }

        public bool HasObject(ObjectCategory category, int id)
        {
            var data = _repository.Data?.KeyItem;
            return data != null && TryGet(data, category, id, out _);
        }
    }
}
