using UnityEngine.InputSystem;

namespace Game.Shared.Input
{
    public static class InputSystemHelper
    {
        public static string ResolveDeviceName(string deviceLayoutName)
        {
            if (string.IsNullOrEmpty(deviceLayoutName)) return string.Empty;
            if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "KeyBoard")) return "keyboard";
            if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "Mouse")) return "mouse";
            if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "DualShockGamepad")) return "ps";
            if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "SwitchProControllerHID")) return "switch";
            if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "XInputController")) return "xbox";
            return string.Empty;
        }
    }
}
