using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Dialogs;
using Game.Horror.Enemy;
using Game.Horror.Interaction;
using Game.Horror.Player;
using Game.Horror.Services;
using Game.Horror.Services.Interfaces;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Extensions;
using Game.Shared.Scenes;
using R3;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Horror.Scenes
{
    public class HorrorStageScene : GamePrefabScene<HorrorStageScene, HorrorStageSceneComponent>
    {
        protected override string AssetPathOrAddress => "HorrorStageScene";

        private GameSceneService _sceneService;
        private InputSystemService _inputService;
        private HorrorOptionSaveService _optionSaveService;
        private IHorrorPlayerService _playerService;

        private SceneInstance _stageSceneInstance;
        private HorrorPlayerStart _playerStart;
        private HorrorEnemyStart[] _enemyStarts;

        public override UniTask PreInitialize()
        {
            _sceneService = GameServiceManager.Get<GameSceneService>();
            _inputService = GameServiceManager.Get<InputSystemService>();
            _optionSaveService = GameServiceManager.Resolve<HorrorOptionSaveService>();
            _playerService = GameServiceManager.Resolve<IHorrorPlayerService>();
            return base.PreInitialize();
        }

        public override async UniTask Startup()
        {
            await LoadUnitySceneAsync();
            var player = await LoadPlayerAsync();
            await LoadEnemiesAsync(player);

            _inputService.UI.Menu.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .SubscribeAwait(async (_, _) => await ShowPauseDialogAsync())
                .AddTo(Disposables);

            _inputService.UI.Inventory.OnPerformedAsObservable()
                .Where(_ => State.IsProcessing())
                .SubscribeAwait(async (_, _) => await HorrorInventoryDialog.RunAsync())
                .AddTo(Disposables);

            await base.Startup();
        }

        public override UniTask Ready()
        {
            ApplicationEvents.ResumeTime();
            return base.Ready();
        }

        public override async UniTask Terminate()
        {
            UnloadEnemies();
            UnloadPlayer();
            await UnloadUnitySceneAsync();
            await base.Terminate();
        }

        private async UniTask LoadUnitySceneAsync()
        {
            Physics.simulationMode = SimulationMode.FixedUpdate;
            _stageSceneInstance = await AssetService.LoadSceneAsync("Abandoned_Asylum");
            SceneManager.SetActiveScene(_stageSceneInstance.Scene);
        }

        private async UniTask UnloadUnitySceneAsync()
        {
            await AssetService.UnloadSceneAsync(_stageSceneInstance);
            _stageSceneInstance = default;
        }

        private async UniTask<GameObject> LoadPlayerAsync()
        {
            _playerStart = GameSceneHelper.GetComponentInChildren<HorrorPlayerStart>(_stageSceneInstance.Scene);
            if (_playerStart == null)
                return null;

            var player = await _playerStart.LoadPlayerAsync();
            player.Initialize(_optionSaveService.Data);
            ApplyRespawnPosition(player);
            _optionSaveService.OnSaved
                .Subscribe(data => player.ApplyOptions(data))
                .AddTo(Disposables);
            return player.gameObject;
        }

        private void UnloadPlayer()
        {
            if (_playerStart != null)
                _playerStart.UnloadPlayer();
        }

        /// <summary>
        /// 記録済みセーブポイントがあれば、そのリスポーン位置・向き（Yaw のみ）から開始する。
        /// 未記録・シーン内に該当 Id なし・RespawnPoint 未配線は HorrorPlayerStart の位置のまま（フォールバック）。
        /// シーンロード完了時点で Awake は完了済みで、判定は SerializeField のみに依存するため Start を待つ必要はない。
        /// </summary>
        private void ApplyRespawnPosition(HorrorPlayerController player)
        {
            var savepointId = _playerService.LastSavepointId;
            if (savepointId == 0)
                return;

            var savePoints = GameSceneHelper.GetComponentsInChildren<HorrorSavepointInteractable>(_stageSceneInstance.Scene);
            var savepoint = System.Array.Find(savePoints, s => s.InteractionId == savepointId);
            if (savepoint == null)
            {
                Debug.LogWarning($"[{nameof(HorrorStageScene)}] Savepoint (InteractionId={savepointId}) がシーン内に見つからないため初期位置から開始します");
                return;
            }

            var respawnPoint = savepoint.RespawnPoint;
            if (respawnPoint == null)
                return; // 未配線は RespawnPoint 側で LogError 済み。初期位置フォールバック

            // プレイヤー本体は Yaw のみ持つ（Pitch はカメラ側）ため Yaw だけ反映する
            player.Teleport(respawnPoint.position, Quaternion.Euler(0f, respawnPoint.eulerAngles.y, 0f));
        }

        private async UniTask LoadEnemiesAsync(GameObject player)
        {
            if (player == null)
                return;

            _enemyStarts = GameSceneHelper.GetComponentsInChildren<HorrorEnemyStart>(_stageSceneInstance.Scene);
            foreach (var enemyStart in _enemyStarts)
            {
                await enemyStart.LoadEnemyAsync(player);
            }
        }

        private void UnloadEnemies()
        {
            if (_enemyStarts == null)
                return;

            foreach (var enemyStart in _enemyStarts)
            {
                if (enemyStart != null)
                    enemyStart.UnloadEnemy();
            }

            _enemyStarts = null;
        }

        private async UniTask ShowPauseDialogAsync()
        {
            var result = await HorrorPauseDialog.RunAsync();
            switch (result)
            {
                case PauseResult.Resume:
                {
                    break;
                }
                case PauseResult.ReturnToTitle:
                {
                    await _sceneService.TransitionAsync<HorrorTitleScene>();
                    break;
                }
                case PauseResult.Quit:
                {
                    ApplicationEvents.RequestShutdown();
                    break;
                }
            }
        }
    }
}
