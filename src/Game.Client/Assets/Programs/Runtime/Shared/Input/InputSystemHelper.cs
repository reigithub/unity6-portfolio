using System;
using UnityEngine.UI;

namespace Game.Shared.Input
{
    public static class InputSystemHelper
    {
        public static Selectable[] GetAllSelectables(Selectable[] selectables = null)
        {
            Selectable[] allSelectables = selectables ?? Array.Empty<Selectable>();
            if (allSelectables.Length > 0) return allSelectables;

            // if (Selectable.allSelectableCount > 0)
            //     allSelectables = new Selectable[Selectable.allSelectableCount];

            int count = Selectable.AllSelectablesNoAlloc(allSelectables);
            if (count > 0) return allSelectables;

            allSelectables = Selectable.allSelectablesArray;
            return allSelectables;
        }
    }
}
