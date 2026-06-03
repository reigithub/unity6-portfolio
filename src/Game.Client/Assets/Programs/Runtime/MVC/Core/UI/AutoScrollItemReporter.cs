using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core.UI
{
    public class AutoScrollItemReporter : MonoBehaviour, ISelectHandler
    {
        public AutoScrollRect Owner { get; set; }

        public void OnSelect(BaseEventData eventData)
        {
            if (Owner == null) return;
            Owner.OnItemSelected(transform);
        }
    }
}
