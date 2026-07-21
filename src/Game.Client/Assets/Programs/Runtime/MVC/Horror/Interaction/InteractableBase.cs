using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Dialogs;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.Scriptable.Database;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
using Game.Shared.Services.Interfaces;
using UnityEngine;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// フィールド配置インタラクト対象の共通基底。マスターデータ（<see cref="HorrorInteractionMaster"/>）の参照、
    /// 提示（ハイライト・プロンプト）の反映、中心位置算出を担い、具象は動詞・効果・必要な可否判定だけを実装する。
    /// プロンプト表示は中央プール（<see cref="InteractionPromptPool"/>）から <see cref="InteractionPromptView"/> を
    /// 表示中だけ貸し受け、Hidden 遷移時に即座に返却する（返却直後に参照を null 化し、他対象への再貸出時の
    /// 表示クロストークを防ぐ）。
    /// 依存解決は MVC の <see cref="GameServiceManager"/> 経由。
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [Tooltip("参照する HorrorInteractionMaster の Id")]
        [SerializeField] protected int _interactionId;

        [Tooltip("中心位置の上書き。未指定なら自身の transform.position を使う")]
        [SerializeField] private Transform _centerOverride;

        [Tooltip("プロンプト表示位置。未指定時は _centerOverride、それも未指定なら自身の transform を使う")]
        [SerializeField] private Transform _promptAnchor;

        // WorldBounds 算出用のコライダー群（Awake で一度だけ取得し、毎回 .bounds で最新の world AABB を合成する）
        [SerializeField] private Collider[] _colliders;

        [Tooltip("アウトライン表現を担うコンポーネント")]
        [SerializeField] private InteractionOutlineHighlighter _highlighter;

        private IHorrorInteractionService _interactionService;
        private IHorrorInventoryService _inventoryService;
        private ILocalizationService _localizationService;
        private IScriptableDatabaseService _databaseService;
        private IHorrorGameRootService _gameRootService;
        protected ScriptableDatabase Database => _databaseService.Database;

        protected HorrorInteractionMaster Master { get; private set; }

        // プール貸出中の View（未貸出は null）。返却直後は必ず null 化する（クロストーク防止の最重要不変条件）
        private InteractionPromptView _rentedView;

        // 再インタラクト表示（動詞切替）のキャッシュ。View 側は貸出のたびに作り直されるため、ここで保持し Bind 時に再適用する
        private bool _interactionToggle;

        public int InteractionId => _interactionId;

        // 中心位置のフォールバック正本。CenterPosition / PromptAnchor の両方がこの連鎖を共有する
        private Transform CenterTransform => _centerOverride != null ? _centerOverride : transform;

        private Transform PromptAnchor => _promptAnchor != null ? _promptAnchor : CenterTransform;

        protected virtual void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        }

        protected virtual void Start()
        {
            _interactionService = GameServiceManager.Resolve<IHorrorInteractionService>();
            _inventoryService = GameServiceManager.Resolve<IHorrorInventoryService>();
            _localizationService = GameServiceManager.Resolve<ILocalizationService>();
            _databaseService = GameServiceManager.Resolve<IScriptableDatabaseService>();
            _gameRootService = GameServiceManager.Resolve<IHorrorGameRootService>();

            if (_databaseService.Database.HorrorInteractionMasterTable.TryFindById(_interactionId, out var master))
            {
                Master = master;
            }
        }

        public Vector3 CenterPosition => CenterTransform.position;

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

        public virtual bool WasInteracted() => _interactionService.Contains(_interactionId);

        public virtual bool CanInteract() => true;

        public virtual void Interact() => _interactionService.Add(Master);

        public virtual InteractionTargetInfo GetInteractionTargetInfo()
        {
            return new InteractionTargetInfo();
        }

        public void SetInteractionState(InteractionState state, Camera viewCamera)
        {
            if (_highlighter != null)
                _highlighter.SetHighlighted(state == InteractionState.Actionable);

            if (Master == null) return; // マスター未解決の対象はプロンプトを貸し出さない

            bool shouldShow = state != InteractionState.Hidden;
            if (shouldShow)
            {
                if (_rentedView == null)
                {
                    _rentedView = _gameRootService.PromptPool.Rent();
                    _rentedView.Bind(Master, PromptAnchor, _interactionToggle);
                    _rentedView.SetTargetInfo(GetInteractionTargetInfo());
                }

                _rentedView.SetState(state, viewCamera);
            }
            else
            {
                ReturnRentedView();
            }
        }

        public void SetHoldProgress(float progress01)
        {
            if (_rentedView != null)
                _rentedView.SetHoldProgress(progress01);
        }

        public UniTask<bool> TryShowRejectionMessage()
        {
            if (Master == null || string.IsNullOrEmpty(Master.RejectionMessageLocalizeKey))
                return UniTask.FromResult(false);

            var message = _localizationService.GetStringByMessages(Master.RejectionMessageLocalizeKey);
            return HorrorMessageDialog.RunAsync(message);
        }

        // 無効化時、貸出中なら返却する（対象 Destroy 時の取り残し防止）
        protected virtual void OnDisable()
        {
            ReturnRentedView();
        }

        // 貸出中の View を返却する唯一の経路。返却直後の null 化（クロストーク防止の最重要不変条件）を
        // ここで一体化して担保する。返却経路を追加する場合も必ずこのメソッドを経由すること。
        private void ReturnRentedView()
        {
            if (_rentedView == null) return;

            _gameRootService.PromptPool.Return(_rentedView);
            _rentedView = null;
        }

        protected void SetInteractionToggle(bool isOn)
        {
            _interactionToggle = isOn;

            if (_rentedView != null)
                _rentedView.SetInteractionToggle(isOn);
        }

        /// <summary>インベントリに指定アイテムを1つ以上所持しているか。</summary>
        protected bool HasItem()
        {
            if (Master == null || Master.RequiredItemId == 0)
                return true;

            return _inventoryService.HasItem(InventorySlotType.Item, Master.RequiredItemId);
        }
    }
}
