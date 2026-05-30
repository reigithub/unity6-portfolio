using System;
using UnityEngine.UI;

namespace Game.Shared.Input
{
    public static class InputSystemHelper
    {
        public static Selectable[] GetAllSelectables()
        {
            Selectable[] allSelectables = Array.Empty<Selectable>();
            int allCount = Selectable.allSelectableCount;
            if (allCount > 0)
                allSelectables = new Selectable[allCount];
            else
                return allSelectables;

            int count = Selectable.AllSelectablesNoAlloc(allSelectables);
            if (count > 0) return allSelectables;

            allSelectables = Selectable.allSelectablesArray;
            return allSelectables;
        }
    }
}
