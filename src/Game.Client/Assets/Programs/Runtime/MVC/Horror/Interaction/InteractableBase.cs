using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Services;
using Game.Shared.Enums;
using Game.Shared.Scriptable.Database;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// フィールド配置インタラクト対象の共通基底。マスターデータ（<see cref="HorrorInteractionMaster"/>）の参照、
    /// 提示（ハイライト・プロンプト）の委譲、中心位置算出を担い、具象は動詞・効果・必要な可否判定だけを実装する。
    /// 依存解決は MVC の <see cref="GameServiceManager"/> 経由。
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [Tooltip("参照する HorrorInteractionMaster の Id")]
        [SerializeField] protected int _interactionId;

        [Tooltip("中心位置の上書き。未指定なら自身の transform.position を使う")]
        [SerializeField] private Transform _centerOverride;

        // WorldBounds 算出用のコライダー群（Awake で一度だけ取得し、毎回 .bounds で最新の world AABB を合成する）
        [SerializeField] private Collider[] _colliders;

        [Tooltip("アウトライン表現を担うコンポーネント")]
        [SerializeField] private InteractionOutlineHighlighter _highlighter;

        [Tooltip("対象位置に出すプロンプト表示")]
        [SerializeField] private InteractionPromptView _promptView;

        protected HorrorInteractionSaveService InteractionSaveService { get; private set; }
        protected HorrorInventorySaveService InventorySaveService { get; private set; }
        protected HorrorCheckpointSaveService CheckpointSaveService { get; private set; }

        private IScriptableDatabaseService _databaseService;
        protected ScriptableDatabase Database => _databaseService.Database;

        protected HorrorInteractionMaster Master { get; private set; }

        protected virtual void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        }

        protected virtual void Start()
        {
            InteractionSaveService = GameServiceManager.Resolve<HorrorInteractionSaveService>();
            InventorySaveService = GameServiceManager.Resolve<HorrorInventorySaveService>();
            CheckpointSaveService = GameServiceManager.Resolve<HorrorCheckpointSaveService>();

            _databaseService = GameServiceManager.Get<ScriptableDatabaseService>();
            if (_databaseService.Database.HorrorInteractionMasterTable.TryFindById(_interactionId, out var master))
            {
                Master = master;

                if (_promptView != null)
                    _promptView.Initialize(master);
            }
        }

        public Vector3 CenterPosition =>
            _centerOverride != null ? _centerOverride.position : transform.position;

        public Bounds WorldBounds
        {
            get
            {
                Bounds bounds = default;
                bool initialized = false;

                if (_colliders != null)
                {
                    for (int i = 0; i < _colliders.Length; i++)
                    {
                        var collider = _colliders[i];
                        if (collider == null || !collider.enabled) continue;

                        if (!initialized)
                        {
                            bounds = collider.bounds;
                            initialized = true;
                        }
                        else
                        {
                            bounds.Encapsulate(collider.bounds);
                        }
                    }
                }

                // コライダーが無ければ中心点の極小 bounds でフォールバック（面積を持たないが検出は成立しうる）
                return initialized ? bounds : new Bounds(CenterPosition, Vector3.zero);
            }
        }

        public virtual InteractionInputType InputType =>
            Master != null ? Master.InputType : InteractionInputType.Instant;

        public virtual float HoldSeconds => Master != null ? Master.HoldSeconds : 0f;

        public virtual bool WasInteracted() => InteractionSaveService.Contains(_interactionId);

        public virtual bool CanInteract() => true;

        public virtual void Interact()
        {
            InteractionSaveService.Add(Master);

            if (Master.CheckpointSave)
                CheckpointSaveService.SaveIfDirtyAsync().Forget();
        }

        public void SetInteractionState(InteractionState state, Camera viewCamera)
        {
            if (_highlighter != null)
                _highlighter.SetHighlighted(state == InteractionState.Actionable);

            if (_promptView != null)
                _promptView.SetState(state, viewCamera);
        }

        public void SetHoldProgress(float progress01)
        {
            if (_promptView != null)
                _promptView.SetHoldProgress(progress01);
        }

        protected virtual void OnDisable()
        {
            if (_promptView != null)
                _promptView.SetState(InteractionState.Hidden, null);
        }

        protected void SetInteractionToggle(bool isOn)
        {
            if (_promptView != null)
                _promptView.SetInteractionToggle(isOn);
        }

        /// <summary>インベントリに指定アイテムを1つ以上所持しているか。</summary>
        protected bool HasItem()
        {
            if (Master == null) return false;
            return InventorySaveService.HasItem(Master.RequiredItemId);
        }
    }
}
