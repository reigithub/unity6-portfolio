using System;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.DI;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Models;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Services;
using Game.Shared.Bootstrap;
using Game.Shared.Services;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorメインステージシーン（Presenter）
    /// StateMachineでゲームループを管理
    /// </summary>
    public partial class SurvivorStageScene : GamePrefabScene<SurvivorStageScene, SurvivorStageSceneComponent>, IGameSceneScope
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly ISurvivorSaveService _saveService;
        [Inject] private readonly ISurvivorGameRunner _gameRunner;
        [Inject] private readonly IAddressableAssetService _addressableService;

        private SurvivorStageModel _stageModel;
        private SurvivorStageWaveManager _waveManager;
        private SceneInstance? _stageSceneInstance;

        protected override string AssetPathOrAddress => "SurvivorStageScene";

        #region IGameSceneScope

        public IObjectResolver ScopedResolver { get; set; }

        public void ConfigureScope(IContainerBuilder builder)
        {
            // シーンスコープのモデルを登録（Terminate時にCompositeDisposable経由で自動破棄）
            builder.Register<SurvivorStageModel>(Lifetime.Scoped);
            builder.Register<SurvivorStageWaveManager>(Lifetime.Scoped);
        }

        #endregion

        public override async UniTask Startup()
        {
            await base.Startup();

            // セッションからステージ情報を取得
            var session = _saveService.CurrentSession;
            if (session == null)
            {
                Debug.LogError("[SurvivorStageScene] No active session found!");
                return;
            }

            // IGameSceneScopeのスコープから取得して初期化
            // [Inject]によりIMasterDataServiceは自動インジェクションされる
            _stageModel = ScopedResolver.Resolve<SurvivorStageModel>();
            _stageModel.Initialize(session.PlayerId, session.StageId);

            _waveManager = ScopedResolver.Resolve<SurvivorStageWaveManager>();
            _waveManager.Initialize(session.StageId);

            // インゲームフィールドをロード
            await LoadUnitySceneAsync();

            BuildStateMachine();
            SubscribeEvents();
            BindModelToView();

            SceneComponent.Initialize(_stageModel);
        }

        private async UniTask LoadUnitySceneAsync()
        {
            // ステージ環境シーンをAdditiveでロード
            var stageAssetName = _stageModel.StageMaster?.AssetName;
            if (!string.IsNullOrEmpty(stageAssetName))
            {
                _stageSceneInstance = await _addressableService.LoadSceneAsync(stageAssetName);
                Debug.Log($"[SurvivorStageScene] Loaded stage environment: {stageAssetName}");

                // ステージシーンに固有のスカイボックスがあれば適用
                var skybox = GameSceneHelper.GetComponentInChildren<Skybox>(_stageSceneInstance.Value.Scene);
                if (skybox != null && skybox.material != null)
                {
                    _gameRunner.GameRootController?.SetSkyboxMaterial(skybox.material);
                    Debug.Log($"[SurvivorStageScene] Applied stage skybox: {skybox.material.name}");
                }
            }
        }

        private void SubscribeEvents()
        {
            SceneComponent.OnPauseClicked
                .Subscribe(_ => _pauseRequested = true)
                .AddTo(Disposables);

            // プレイヤーダメージをモデルに反映
            if (SceneComponent.PlayerController != null)
            {
                SceneComponent.PlayerController.OnDamaged
                    .Subscribe(damage => _stageModel.TakeDamage(damage))
                    .AddTo(Disposables);
            }

            if (SceneComponent.EnemySpawner != null)
            {
                SceneComponent.EnemySpawner.OnEnemyKilled
                    .Subscribe(_ => _stageModel.AddKill())
                    .AddTo(Disposables);
            }

            if (SceneComponent.ExperienceOrbSpawner != null)
            {
                SceneComponent.ExperienceOrbSpawner.OnExperienceCollected
                    .Subscribe(exp => _stageModel.AddExperience(exp))
                    .AddTo(Disposables);
            }

            _stageModel.Level
                .Skip(1)
                .Subscribe(_ => _levelUpRequested = true)
                .AddTo(Disposables);

            SceneComponent.UpdateAsObservable()
                .Subscribe(_ => _stateMachine?.Update())
                .AddTo(Disposables);

            // 自動保存のセットアップ
            SetupAutoSave();
        }

        private void SetupAutoSave()
        {
            // 30秒ごとにセッションを保存
            Observable.Interval(TimeSpan.FromSeconds(30))
                .Subscribe(_ => SaveCurrentSession())
                .AddTo(Disposables);

            // アプリ中断時（バックグラウンド移行時）に保存
            SceneComponent.OnApplicationPauseObservable
                .Where(paused => paused)
                .Subscribe(_ => SaveCurrentSession())
                .AddTo(Disposables);

            // アプリ終了時に保存
            SceneComponent.OnApplicationQuitObservable
                .Subscribe(_ => SaveCurrentSession())
                .AddTo(Disposables);
        }

        private void SaveCurrentSession()
        {
            if (_stageModel == null || _saveService?.CurrentSession == null) return;

            // ゲームオーバーや勝利後は保存しない
            if (_stageModel.IsDead || _saveService.CurrentSession.IsCompleted) return;

            _saveService.UpdateSession(
                currentWave: _waveManager?.CurrentWave.CurrentValue ?? 1,
                elapsedTime: _stageModel.GameTime.Value,
                currentHp: _stageModel.CurrentHp.Value,
                experience: _stageModel.Experience.Value,
                level: _stageModel.Level.Value,
                score: _stageModel.Score.Value,
                totalKills: _stageModel.TotalKills.Value
            );

            _saveService.SaveAsync().Forget();
        }

        private void BindModelToView()
        {
            // HP
            _stageModel.CurrentHp
                .CombineLatest(_stageModel.MaxHp, (current, max) => (current, max))
                .Subscribe(hp => SceneComponent.UpdateHP(hp.current, hp.max))
                .AddTo(Disposables);

            // 経験値
            _stageModel.Experience
                .CombineLatest(_stageModel.ExperienceToNextLevel, (current, max) => (current, max))
                .Subscribe(exp => SceneComponent.UpdateExperience(exp.current, exp.max))
                .AddTo(Disposables);

            // レベル
            _stageModel.Level
                .Subscribe(level => SceneComponent.UpdateLevel(level))
                .AddTo(Disposables);

            // キル数
            _stageModel.TotalKills
                .Subscribe(kills => SceneComponent.UpdateKills(kills))
                .AddTo(Disposables);

            // ウェーブ
            _waveManager.CurrentWave
                .Subscribe(wave =>
                {
                    _stageModel.CurrentWave.Value = wave;
                    SceneComponent.UpdateWave(wave);
                })
                .AddTo(Disposables);
        }

        public override async UniTask Ready()
        {
            // 基底クラスでグローバルフェードイン
            await base.Ready();

            await UniTask.Delay(500);

            _stateMachine.Update();
        }

        public override async UniTask Terminate()
        {
            ApplicationEvents.ResumeTime();

            var score = _stageModel?.Score.Value ?? 0;
            var kills = _stageModel?.TotalKills.Value ?? 0;
            var clearTime = _stageModel?.GameTime.Value ?? 0f;
            var isVictory = _stageModel != null && !_stageModel.IsDead;

            // セッションにステージ結果を記録（リザルト画面用＆永続化）
            _saveService?.CompleteCurrentStage(score, kills, clearTime, isVictory);

            // プレイ時間を加算
            _saveService?.AddPlayTime(clearTime);

            // 非同期で保存（完了を待たない）
            _saveService?.SaveAsync().Forget();

            // スカイボックスをデフォルトに戻す
            _gameRunner.GameRootController?.ResetSkyboxMaterial();

            // ステージ環境シーンをアンロード
            if (_stageSceneInstance.HasValue)
            {
                await _addressableService.UnloadSceneAsync(_stageSceneInstance.Value);
                _stageSceneInstance = null;
                Debug.Log("[SurvivorStageScene] Unloaded stage environment");
            }

            // Note: ScopedResolver（_stageModel, _waveManager含む）はTerminateCore後に自動Dispose

            await base.Terminate();
        }
    }
}