using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core.UI
{
    public class AutoScrollItemReporter : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public AutoScrollRect Owner { get; set; }

        public void OnSelect(BaseEventData eventData)
        {
            if (Owner == null) return;
            Owner.OnItemSelected(transform);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            Deselect();
        }

        public void OnDisable()
        {
            Deselect();
        }

        private void Deselect()
        {
            if (Owner == null) return;
            Owner.OnItemDeselected(transform);
        }
    }
}
