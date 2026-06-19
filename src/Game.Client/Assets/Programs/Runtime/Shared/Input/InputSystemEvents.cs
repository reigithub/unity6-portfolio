using System;
using R3;
using UnityEngine.InputSystem;

namespace Game.Shared.Input
{
    public static class InputSystemEvents
    {
        public static Observable<(InputDevice device, InputDeviceChange deviceChange)> OnDeviceChanged
            => Observable.FromEvent<Action<InputDevice, InputDeviceChange>, (InputDevice, InputDeviceChange)>(
                h => (a, b) => h((a, b)),
                h => InputSystem.onDeviceChange += h,
                h => InputSystem.onDeviceChange -= h);
    }
}
