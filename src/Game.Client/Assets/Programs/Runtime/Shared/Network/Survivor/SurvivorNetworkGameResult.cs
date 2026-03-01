using Unity.Collections;
using Unity.Netcode;
using Game.Library.Shared.Dto;

namespace Game.Shared.Network.Survivor
{
    public struct SurvivorNetworkPlayerResult : INetworkSerializable
    {
        public FixedString64Bytes UserId;
        public int Score;
        public int TotalKills;
        public int Level;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref UserId);
            serializer.SerializeValue(ref Score);
            serializer.SerializeValue(ref TotalKills);
            serializer.SerializeValue(ref Level);
        }

        public static SurvivorNetworkPlayerResult FromDto(PlayerResultSnapshot dto)
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

    public struct SurvivorNetworkGameResult : INetworkSerializable
    {
        public bool IsVictory;
        public float ClearTime;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref IsVictory);
            serializer.SerializeValue(ref ClearTime);
        }

        public static SurvivorNetworkGameResult FromDto(GameResultSnapshot dto)
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
