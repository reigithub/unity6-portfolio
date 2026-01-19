namespace Game.Core.MessagePipe
{
    public partial struct MessageKey
    {
        private struct Offset
        {
            public const int System = 0;
            public const int GameScene = 200;
            public const int Player = 500;
            public const int UI = 600;
            public const int InputSystem = 700;
        }

        public struct System
        {
            public const int TimeScale = Offset.System + 0;
            public const int Cursor = Offset.System + 1;
            public const int DirectionalLight = Offset.System + 2;
            public const int Skybox = Offset.System + 3;
            public const int DefaultSkybox = Offset.System + 4;
        }

        public struct GameScene
        {
            public const int TransitionEnter = Offset.GameScene + 0;
            public const int TransitionFinish = Offset.GameScene + 1;
        }

        public struct Player
        {
            public const int PlayAnimation = Offset.Player + 0;
            public const int SpawnPlayer = Offset.Player + 1;
            public const int OnTriggerEnter = Offset.Player + 10;
            public const int OnCollisionEnter = Offset.Player + 20;

            // HUD Communication
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

        public struct InputSystem
        {
            public const int Escape = Offset.InputSystem + 0;
            public const int ScrollWheel = Offset.InputSystem + 1;
        }
    }
}