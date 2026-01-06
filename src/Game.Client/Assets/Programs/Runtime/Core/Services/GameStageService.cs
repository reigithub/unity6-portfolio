using System.Collections.Generic;
using System.Linq;
using Game.Contents.Scenes;
using MessagePipe;
using UnityEngine;

namespace Game.Core.Services
{
    public class GameStageService : GameService
    {
        private GameObject _gameStageService;
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

        protected internal override void Shutdown()
        {
            _gameStageResults.Clear();
            base.Shutdown();
        }

        protected internal override bool AllowResidentOnMemory => true;
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