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

    public struct InputBindingInfo
    {
        public string ControlScheme { get; init; }
        public string ActionMapName { get; init; }
        public string ActionName { get; init; }
        public string CompositePartName { get; init; }

        public string DisplayName { get; init; }

        public string DeviceLayoutName { get; init; }
        public string ControlPath { get; init; }

        public int BindingIndex { get; init; }
        public bool IsPartOfComposite { get; init; }
    }

    public struct InputBindingIndexInfo
    {
        public int Index { get; init; }
        public bool IsPartOfComposite { get; init; }
    }
}
