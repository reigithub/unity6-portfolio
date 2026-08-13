using System;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Dialogs;
using Game.Horror.Enemy;
using Game.Horror.Interaction;
using Game.Horror.Player;
using Game.Horror.Services.Interfaces;
using Game.Horror.WeaponEffect;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Extensions;
using Game.Shared.Scenes;
using Game.Shared.Services;
using R3;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Horror.Scenes
{
    public class HorrorStageScene : GamePrefabScene<HorrorStageScene, HorrorStageSceneComponent>
    {
        protected override string AssetPathOrAddress => "HorrorStageScene";

        private readonly IAddressableAssetService _assetService = GameServiceManager.Resolve<IAddressableAssetService>();
        private readonly IGameSceneService _sceneService = GameServiceManager.Resolve<IGameSceneService>();
        private readonly IInputSystemService _inputService = GameServiceManager.Resolve<IInputSystemService>();
        private readonly IAudioService _audioService = GameServiceManager.Resolve<IAudioService>();
        private readonly IScriptableDatabaseService _databaseService = GameServiceManager.Resolve<IScriptableDatabaseService>();
        private readonly IMessagePipeService _messagePipeService = GameServiceManager.Resolve<IMessagePipeService>();
        private readonly IHorrorSaveRepository _saveRepository = GameServiceManager.Resolve<IHorrorSaveRepository>();
        private readonly IHorrorOptionSaveRepository _optionSaveRepository = GameServiceManager.Resolve<IHorrorOptionSaveRepository>();
        private readonly IHorrorPlayerService _playerService = GameServiceManager.Resolve<IHorrorPlayerService>();

        private SceneInstance _stageSceneInstance;
        private HorrorPlayerStart _playerStart;
        private HorrorPlayerController _player;
        private HorrorEnemySpawner _enemySpawner;
        private HorrorEnemyDropSpawner _dropSpawner;
        private HorrorWeaponEffectSpawner _weaponEffectSpawner;

        public override async UniTask Startup()
        {
            await LoadUnitySceneAsync();
            await LoadWeaponEffectsAsync();
            var player = await LoadPlayerAsync();
            await LoadEnemiesAsync(player);
            await LoadDropsAsync(player);

            _inputService.Player.Menu.OnPerformedAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(0.1f))
                .Where(_ => State.IsProcessing())
                .SubscribeAwait(async (_, _) => await ShowPauseDialogAsync())
                .AddTo(Disposables);

            _inputService.Player.Inventory.OnPerformedAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(0.1f))
                .Where(_ => State.IsProcessing())
                .SubscribeAwait(async (_, _) => await ShowInventoryDialogAsync())
                .AddTo(Disposables);

            await base.Startup();
        }

        public override async UniTask Ready()
        {
            ApplicationEvents.ResumeTime();
            await _audioService.PlayBgmAsync("ha-undercurrent", SceneComponent.GetCancellationTokenOnDestroy());
            await base.Ready();
        }

        public override async UniTask Terminate()
        {
            UnloadDrops();
            UnloadEnemies();
            UnloadPlayer();
            UnloadWeaponEffects();
            await UnloadUnitySceneAsync();
            await base.Terminate();
        }

        private async UniTask LoadUnitySceneAsync()
        {
            Physics.simulationMode = SimulationMode.FixedUpdate;
            _stageSceneInstance = await _assetService.LoadSceneAsync("Abandoned_Asylum");
            SceneManager.SetActiveScene(_stageSceneInstance.Scene);
        }

        private async UniTask UnloadUnitySceneAsync()
        {
            await _assetService.UnloadSceneAsync(_stageSceneInstance);
            _stageSceneInstance = default;
        }

        /// <summary>
        /// 武器効果スポナー（投擲物・煙フィールドの生成基盤）を生成し、投擲物プレハブを事前ロードする。
        /// プレイヤー（投擲依頼元）とエネミー（Registry の読み手）より前に初期化する。
        /// </summary>
        private async UniTask LoadWeaponEffectsAsync()
        {
            _weaponEffectSpawner = new HorrorWeaponEffectSpawner(_assetService, _databaseService);
            await _weaponEffectSpawner.InitializeAsync();
        }

        private void UnloadWeaponEffects()
        {
            _weaponEffectSpawner?.Dispose();
            _weaponEffectSpawner = null;
        }

        private async UniTask<GameObject> LoadPlayerAsync()
        {
            _playerStart = GameSceneHelper.GetComponentInChildren<HorrorPlayerStart>(_stageSceneInstance.Scene);
            if (_playerStart == null)
                return null;

            if (!_playerService.ResolvePlayerMaster())
                return null;

            _player = await _playerStart.LoadPlayerAsync(_playerService.PlayerMaster);
            _player.Initialize(_optionSaveRepository.Data, _weaponEffectSpawner);
            ApplyRespawnPosition(_player);
            _optionSaveRepository.OnSaved
                .Subscribe(data => _player.ApplyOptions(data))
                .AddTo(Disposables);
            return _player.gameObject;
        }

        private void UnloadPlayer()
        {
            if (_playerStart != null)
                _playerStart.UnloadPlayer();
            _player = null;
        }

        /// <summary>
        /// 記録済みセーブポイントがあれば、そのリスポーン位置・向き（Yaw のみ）から開始する。
        /// 未記録・シーン内に該当 Id なし・RespawnPoint 未配線は HorrorPlayerStart の位置のまま（フォールバック）。
        /// シーンロード完了時点で Awake は完了済みで、判定は SerializeField のみに依存するため Start を待つ必要はない。
        /// </summary>
        private void ApplyRespawnPosition(HorrorPlayerController player)
        {
            var savepointId = _saveRepository.Data.SavepointId;
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

            // マーカーの検証（SpawnId 未設定/重複）と生成実行はスポナーが担う
            var enemyStarts = GameSceneHelper.GetComponentsInChildren<HorrorEnemyStart>(_stageSceneInstance.Scene);
            _enemySpawner = new HorrorEnemySpawner(
                _assetService, _databaseService, _messagePipeService,
                GameServiceManager.Resolve<IHorrorEnemyService>(),
                _weaponEffectSpawner.Registry);
            await _enemySpawner.InitializeAsync(player, enemyStarts);
        }

        private void UnloadEnemies()
        {
            _enemySpawner?.Dispose();
            _enemySpawner = null;
        }

        private async UniTask LoadDropsAsync(GameObject player)
        {
            // プレイヤー不在時はエネミーも不在（LoadEnemiesAsync と対称のガード）のため撃破ドロップも成立しない
            if (player == null)
                return;

            _dropSpawner = new HorrorEnemyDropSpawner(_assetService, _databaseService, _messagePipeService);
            await _dropSpawner.InitializeAsync();
        }

        private void UnloadDrops()
        {
            _dropSpawner?.Dispose();
            _dropSpawner = null;
        }

        private async UniTask ShowPauseDialogAsync()
        {
            var result = await HorrorPauseDialog.RunAsync();
            switch (result)
            {
                case HorrorPauseResult.Resume:
                {
                    break;
                }
                case HorrorPauseResult.ReturnToTitle:
                {
                    await _sceneService.TransitionAsync<HorrorTitleScene>();
                    break;
                }
                case HorrorPauseResult.Quit:
                {
                    ApplicationEvents.RequestShutdown();
                    break;
                }
            }
        }

        private async UniTask ShowInventoryDialogAsync()
        {
            var result = await HorrorInventoryDialog.RunAsync();
            if (_player != null)
            {
                if (result.HasUseRequest)
                    _player.RequestUseItem(result.UseCategory, result.UseId, result.UseSlotNo);

                if (result.HasEquipRequest)
                    _player.RequestEquip(result.EquipCategory, result.EquipId);
            }
        }
    }
}
