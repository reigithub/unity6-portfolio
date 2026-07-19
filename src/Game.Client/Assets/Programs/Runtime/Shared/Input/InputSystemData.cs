using UnityEngine.InputSystem;

namespace Game.Shared.Input
{
    public struct InputDeviceChangeInfo
    {
        public InputDevice Device { get; }
        public InputDeviceChange DeviceChange { get; }

        public InputDeviceChangeInfo(InputDevice device, InputDeviceChange deviceChange)
        {
            Device = device;
            DeviceChange = deviceChange;
        }
    }

    public struct InputDeviceControlPathInfo
    {
        public string DeviceLayoutName { get; init; }
        public string ControlPath { get; init; }
        public bool IsPartOfComposite { get; init; }
    }

    public struct InputBidingInfo
    {
        public int Index { get; init; }
        public bool IsPartOfComposite { get; init; }
    }
}
