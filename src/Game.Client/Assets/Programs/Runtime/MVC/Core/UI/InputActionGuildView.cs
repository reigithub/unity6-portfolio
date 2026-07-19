using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core.UI
{
    public class InputActionGuildView : MonoBehaviour
    {
        [SerializeField] private InputActionView[] _inputActionViews;

        public void Initialize()
        {
            foreach (var inputActionView in _inputActionViews)
                inputActionView.Initialize();
        }

        public void SetInputActions(params InputAction[] inputActions)
        {
            foreach (var inputActionView in _inputActionViews)
                inputActionView.gameObject.SetActive(inputActions.Contains(inputActionView.InputAction));
        }
    }
}
