namespace Game.Core.MessagePipe
{
    public partial struct MessageKey
    {
        private struct Offset
        {
            public const int System = 0;
            public const int Game = 100;
            public const int GameScene = 200;
            public const int GameStage = 300;
            public const int GameStageService = 400;
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

        public struct Game
        {
            public const int Ready = Offset.Game + 0;
            public const int Start = Offset.Game + 1;
            public const int Quit = Offset.Game + 2;
        }

        public struct GameScene
        {
            public const int TransitionEnter = Offset.GameScene + 0;
            public const int TransitionFinish = Offset.GameScene + 1;
        }

        public struct GameStage
        {
            public const int Ready = Offset.GameStage + 0;
            public const int Start = Offset.GameStage + 1;
            public const int Pause = Offset.GameStage + 2;
            public const int Resume = Offset.GameStage + 3;
            public const int Retry = Offset.GameStage + 4;
            public const int ReturnTitle = Offset.GameStage + 5;
            public const int Result = Offset.GameStage + 6;
            public const int Finish = Offset.GameStage + 7;
        }

        public struct GameStageService
        {
            public const int Startup = Offset.GameStageService + 0;
            public const int Shutdown = Offset.GameStageService + 1;
        }

        public struct Player
        {
            public const int PlayAnimation = Offset.Player + 0;
            public const int SpawnPlayer = Offset.Player + 1;
            public const int OnTriggerEnter = Offset.Player + 10;
            public const int OnCollisionEnter = Offset.Player + 20;
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