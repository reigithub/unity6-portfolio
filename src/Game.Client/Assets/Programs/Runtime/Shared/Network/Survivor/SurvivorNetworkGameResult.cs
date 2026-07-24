using Unity.Collections;
using Game.Library.Shared.Dto;

namespace Game.Shared.Network.Survivor
{
    public struct SurvivorNetworkPlayerResult
    {
        public FixedString64Bytes UserId;
        public int Score;
        public int TotalKills;
        public int Level;

        public SurvivorNetworkPlayerResult FromDto(PlayerResultSnapshot dto)
        {
            return new SurvivorNetworkPlayerResult
            {
                UserId = new FixedString64Bytes(dto.UserId),
                Score = dto.Score,
                TotalKills = dto.TotalKills,
                Level = dto.Level,
            };
        }

        public PlayerResultSnapshot ToDto()
        {
            return new PlayerResultSnapshot
            {
                UserId = UserId.ToString(),
                Score = Score,
                TotalKills = TotalKills,
                Level = Level,
            };
        }
    }

    public struct SurvivorNetworkGameResult
    {
        public bool IsVictory;
        public float ClearTime;
        public int TotalKills;

        public SurvivorNetworkGameResult FromDto(GameResultSnapshot dto)
        {
            return new SurvivorNetworkGameResult
            {
                IsVictory = dto.IsVictory,
                ClearTime = dto.ClearTime,
            };
        }

        public GameResultSnapshot ToDto(PlayerResultSnapshot[] players)
        {
            return new GameResultSnapshot
            {
                IsVictory = IsVictory,
                ClearTime = ClearTime,
                Players = players,
            };
        }
    }
}
