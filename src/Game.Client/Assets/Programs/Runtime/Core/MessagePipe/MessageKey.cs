namespace Game.Core.MessagePipe
{
    public partial struct MessageKey
    {
        private struct Offset
        {
            public const int System = 0;
            public const int Game = 100;
            public const int Player = 200;

            public const int Sample = 90000;
        }

        public struct Sample
        {
            public const int AddScore = Offset.Sample + 0;
            public const int EnemyCollied = Offset.Sample + 1;
        }

        public struct Game
        {
            public const int GameStart = Offset.Game + 0;
        }

        public struct Player
        {
            public const int SpawnPlayer = Offset.Player + 0;
        }
    }
}