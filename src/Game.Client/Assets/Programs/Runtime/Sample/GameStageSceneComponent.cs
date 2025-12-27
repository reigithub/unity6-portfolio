using System.Threading.Tasks;
using Game.Core.Scenes;
using UnityEngine;

namespace Sample
{
    public class GameStageSceneComponent : GameSceneComponent
    {
        [SerializeField] private PlayerStart _playerStart;

        public async Task Initialize(PlayerStart playerStart)
        {
            _playerStart = playerStart;
            await _playerStart.LoadPlayerAsync();
        }
    }
}