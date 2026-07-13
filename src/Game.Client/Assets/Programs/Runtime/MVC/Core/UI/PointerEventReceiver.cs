using Game.Core.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core.UI
{
    public class PointerEventReceiver : MonoBehaviour
        , IPointerEnterHandler, IPointerExitHandler
        , IPointerClickHandler
        , IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Selectable _selectable;

        private IInputSystemService _inputService;

        public void OnPointerEnter(PointerEventData eventData) => OnSelect();

        public void OnPointerExit(PointerEventData eventData) { }

        public void OnPointerClick(PointerEventData eventData) => OnPress(eventData);

        public void OnPointerDown(PointerEventData eventData) => OnPress(eventData);

        public void OnPointerUp(PointerEventData eventData) { }

        private void OnPress(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            OnSelect();
        }

        private void OnSelect()
        {
            _inputService ??= GameServiceManager.Resolve<IInputSystemService>();
            _inputService.SetSelectedGameObject(_selectable.gameObject);
        }

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
