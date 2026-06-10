using UnityEngine.UI;

namespace Game.Shared.Input
{
    public static class InputSystemExtensions
    {
        public static bool IsSelectable(this Selectable selectable)
            => selectable.IsInteractable() && selectable.navigation.mode != Navigation.Mode.None;
    }
}
