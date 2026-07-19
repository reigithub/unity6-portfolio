using Game.Shared.Enums;
using UnityEngine.InputSystem;

namespace Game.Shared.Input
{
    public static class InputSystemHelper
    {
        public static InputDeviceType GetInputDeviceType(string deviceLayoutName)
        {
            if (string.IsNullOrEmpty(deviceLayoutName)) return InputDeviceType.None;
            if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "KeyBoard")) return InputDeviceType.Keyboard;
            if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "Mouse")) return InputDeviceType.Mouse;
            if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "DualShockGamepad")) return InputDeviceType.PlayStation;
            if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "SwitchProControllerHID")) return InputDeviceType.NintendoSwitch;
            if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "XInputController")) return InputDeviceType.Xbox;
            return InputDeviceType.None;
        }
    }
}
