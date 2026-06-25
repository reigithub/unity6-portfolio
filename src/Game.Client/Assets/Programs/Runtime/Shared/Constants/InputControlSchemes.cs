namespace Game.Shared.Constants
{
    public static class InputControlSchemes
    {
        public const string DefaultControlScheme = KeyboardAndMouse;
        public const string KeyboardAndMouse = "Keyboard&Mouse";
        public const string Gamepad = "Gamepad";
        public const string Touch = "Touch";
        public const string Joystick = "Joystick";
        public const string XR = "XR";
    }

    public static class InputConstants
    {
        /// <summary>キーリバインド待機の自動キャンセルまでの秒数。</summary>
        public const float RebindTimeoutSeconds = 3f;
    }
}
