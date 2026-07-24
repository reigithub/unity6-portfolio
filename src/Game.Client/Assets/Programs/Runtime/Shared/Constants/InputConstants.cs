namespace Game.Shared.Constants
{
    public static class InputConstants
    {
        /// <summary>入力判定の閾値（magnitude）</summary>
        public const float InputThreshold = 0.1f;

        /// <summary>キーリバインド待機の自動キャンセルまでの秒数。</summary>
        public const float RebindingTimeoutSeconds = 3f;
    }

    public static class InputActionMaps
    {
        public const string Player = "Player";
        public const string UI = "UI";
    }

    public static class InputControlSchemes
    {
        public const string DefaultControlScheme = KeyboardAndMouse;
        public const string KeyboardAndMouse = "Keyboard&Mouse";
        public const string Gamepad = "Gamepad";
        public const string Touch = "Touch";
        public const string Joystick = "Joystick";
        public const string XR = "XR";
    }
}
