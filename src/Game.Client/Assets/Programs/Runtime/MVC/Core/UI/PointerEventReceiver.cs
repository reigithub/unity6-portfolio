using Game.Core.Services;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core.UI
{
    public class PointerEventReceiver : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        public void OnPointerEnter(PointerEventData eventData)
        {
            InputService.SetSelectedGameObject(gameObject);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            InputService.SetSelectedGameObject(null);
        }
    }
}
