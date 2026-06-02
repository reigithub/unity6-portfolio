using System.Collections.Generic;
using Game.Core.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core.UI
{
    [RequireComponent(typeof(ScrollRect))]
    public class AutoScrollRect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float _scrollSpeed = 10f;
        private bool _isMouseOver = false;

        private List<Selectable> m_Selectables = new List<Selectable>();
        private ScrollRect m_ScrollRect;

        private Vector2 m_NextScrollPosition = Vector2.up;

        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        void OnEnable()
        {
            if (m_ScrollRect)
            {
                m_ScrollRect.content.GetComponentsInChildren(m_Selectables);
            }
        }

        private void Awake()
        {
            m_ScrollRect = GetComponent<ScrollRect>();
        }

        private void Start()
        {
            if (m_ScrollRect)
            {
                m_ScrollRect.content.GetComponentsInChildren(m_Selectables);
            }
            ScrollToSelected(true);
        }

        private void Update()
        {
            // Scroll via input.
            InputScroll();

            if (!_isMouseOver)
            {
                // Lerp scrolling code.
                m_ScrollRect.normalizedPosition = Vector2.Lerp(m_ScrollRect.normalizedPosition, m_NextScrollPosition, _scrollSpeed * Time.deltaTime);
            }
            else
            {
                m_NextScrollPosition = m_ScrollRect.normalizedPosition;
            }
        }

        private void InputScroll()
        {
            if (m_Selectables.Count > 0)
            {
                if (InputService.UI.Navigate.WasPressedThisFrame())
                {
                    ScrollToSelected(false);
                }
            }
        }

        private void ScrollToSelected(bool quickScroll)
        {
            int selectedIndex = -1;
            Selectable selectedElement = EventSystem.current.currentSelectedGameObject ? EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>() : null;

            if (selectedElement)
            {
                selectedIndex = m_Selectables.IndexOf(selectedElement);
            }
            if (selectedIndex > -1)
            {
                if (quickScroll)
                {
                    m_ScrollRect.normalizedPosition = new Vector2(0, 1 - (selectedIndex / ((float)m_Selectables.Count - 1)));
                    m_NextScrollPosition = m_ScrollRect.normalizedPosition;
                }
                else
                {
                    m_NextScrollPosition = new Vector2(0, 1 - (selectedIndex / ((float)m_Selectables.Count - 1)));
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isMouseOver = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isMouseOver = false;
            // ScrollToSelected(false);
        }
    }
}
