using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core.UI
{
    public class InputActionGuildView : MonoBehaviour
    {
        [SerializeField] private bool _initializeOnStart;
        [SerializeField] private InputActionView[] _inputActionViews;

        private bool _initialized;

        private void Start()
        {
            if (_initializeOnStart) Initialize();
        }

        public void Initialize()
        {
            if (_initialized) return;

            foreach (var inputActionView in _inputActionViews)
                inputActionView.Initialize();

            _initialized = true;
        }

        public void SetInputActions(params InputAction[] inputActions)
        {
            foreach (var inputActionView in _inputActionViews)
                inputActionView.gameObject.SetActive(inputActions.Contains(inputActionView.InputAction));
        }
    }
}
