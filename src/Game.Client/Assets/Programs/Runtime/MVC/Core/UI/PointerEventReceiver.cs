using Game.Core.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core.UI
{
    public class PointerEventReceiver : MonoBehaviour
        , IPointerEnterHandler, IPointerExitHandler
        , IPointerClickHandler
    {
        [SerializeField] private Selectable _selectable;

        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        public void OnPointerEnter(PointerEventData eventData) => OnSelected();
        public void OnPointerExit(PointerEventData eventData) => OnDeselected();
        public void OnPointerClick(PointerEventData eventData) => OnSelected();

        private void OnSelected() => InputService.SetSelectedGameObject(_selectable.gameObject);
        private void OnDeselected() => InputService.SetSelectedGameObject(null);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_selectable != null)
                return;

            if (gameObject.TryGetComponent<Selectable>(out var selectable))
                _selectable = selectable;
        }
#endif
    }
}
