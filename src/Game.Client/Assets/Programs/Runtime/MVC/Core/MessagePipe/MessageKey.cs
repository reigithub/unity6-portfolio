namespace Game.Core.MessagePipe
{
    public partial struct MessageKey
    {
        private struct Offset
        {
            public const int Player = 500;
            public const int UI = 600;
        }

        public struct Player
        {
            public const int PlayAnimation = Offset.Player + 0;
            public const int SpawnPlayer = Offset.Player + 1;

            public const int HudFadeIn = Offset.Player + 30;
            public const int HudFadeOut = Offset.Player + 31;
            public const int HpChanged = Offset.Player + 32;
            public const int Running = Offset.Player + 33;
            public const int StaminaChanged = Offset.Player + 34;
        }

        public struct UI
        {
            public const int Escape = Offset.UI + 0;
            public const int ScrollWheel = Offset.UI + 1;
        }
    }
}
