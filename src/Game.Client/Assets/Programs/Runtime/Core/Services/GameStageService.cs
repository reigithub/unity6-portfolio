using System.Collections.Generic;
using System.Linq;
using Game.Contents.Scenes;
using MessagePipe;

namespace Game.Core.Services
{
    public class GameStageService : IGameStageService
    {
        private readonly Dictionary<int, GameStageResultData> _gameStageResults = new();

        public bool TryAddResult(GameStageResultData result)
        {
            return _gameStageResults.TryAdd(result.StageId, result);
        }

        public GameStageTotalResultData CreateTotalResult()
        {
            return new GameStageTotalResultData
            {
                StageResults = _gameStageResults.Values.ToArray()
            };
        }

        public void Shutdown()
        {
            _gameStageResults.Clear();
        }
    }

    // Request/Responseじゃなくてもよいが実験的に入れてみた、が多分MessageBrokerでいい
    public class GameStageRequestHandler :
        IRequestHandler<GameStageResultData, bool>,
        IRequestHandler<GameStageTotalResultRequest, GameStageTotalResultData>
    {
        private GameServiceReference<GameStageService> _gameStageService;
        private GameStageService GameStageService => _gameStageService.Reference;

        public bool Invoke(GameStageResultData result)
        {
            return GameStageService.TryAddResult(result);
        }

        public GameStageTotalResultData Invoke(GameStageTotalResultRequest request)
        {
            return GameStageService.CreateTotalResult();
        }
    }
}