using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Core.UI;
using Game.Horror.Services.Interfaces;
using Game.MVC.Core.Scenes;
using Game.Shared.Interfaces;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services.Interfaces;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Dialogs
{
    /// <summary>
    /// アイテム詳細ダイアログのビュー。名称・説明・（武器なら）SPECS を表示し、
    /// 3D モデルを持つアイテムはプレビュー回転体を、持たないアイテム（キーアイテム等）はフォールバックアイコンを表示する。
    /// </summary>
    public class HorrorItemDetailDialogComponent : GameSceneComponent
    {
        [SerializeField] private HorrorItemPreviewView _previewView;
        [SerializeField] private HorrorWeaponSpecsView _specsView;
        [SerializeField] private RawImage _previewImage;
        [SerializeField] private Image _fallbackIcon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private InputActionGuildView _inputActionGuide;

        private ILocalizationService _localizationService;
        private IHorrorIconService _iconService;
        private IInputSystemService _inputService;
        private IObjectInfo _info;

        /// <summary>
        /// 対象アイテムの情報を反映し、SPECS・プレビュー（またはフォールバックアイコン）・操作ガイドを初期化する。
        /// </summary>
        /// <param name="info">表示対象アイテムの情報。</param>
        public async UniTask InitializeAsync(IObjectInfo info)
        {
            _localizationService = GameServiceManager.Resolve<ILocalizationService>();
            _iconService = GameServiceManager.Resolve<IHorrorIconService>();
            _inputService = GameServiceManager.Resolve<IInputSystemService>();
            _info = info;

            SetText();
            _localizationService.OnLocaleChanged
                .Subscribe(_ => SetText())
                .AddTo(Disposables);

            if (_info is HorrorWeaponMaster weapon)
            {
                _specsView.Initialize();
                _specsView.SetWeapon(weapon);
            }
            else
            {
                // SPECS 欄は武器のみ。アイテム側にスペック相当データが無いため
                _specsView.Hide();
            }

            string modelAssetName = string.Empty;
            Vector3 previewRotation = Vector3.zero;

            switch (_info)
            {
                case HorrorItemMaster i:
                    modelAssetName = i.ModelAssetName;
                    previewRotation = new Vector3(i.PreviewRotationX, i.PreviewRotationY, i.PreviewRotationZ);
                    break;
                case HorrorWeaponMaster w:
                    modelAssetName = w.ModelAssetName;
                    previewRotation = new Vector3(w.PreviewRotationX, w.PreviewRotationY, w.PreviewRotationZ);
                    break;
            }

            _inputActionGuide.Initialize();

            if (string.IsNullOrEmpty(modelAssetName))
            {
                // 3D モデルを持たないアイテム（キーアイテム等）はプレビューリグを起動させない
                _previewImage.gameObject.SetActive(false);
                _previewView.gameObject.SetActive(false);

                Sprite sprite = null;
                if (!string.IsNullOrEmpty(_info.IconAssetName))
                    sprite = _iconService.GetSprite(_info.IconAssetName);

                _fallbackIcon.gameObject.SetActive(true);
                _fallbackIcon.sprite = sprite;
                _fallbackIcon.enabled = sprite != null;

                _inputActionGuide.SetInputActions(_inputService.UI.Cancel);
            }
            else
            {
                _fallbackIcon.gameObject.SetActive(false);

                _inputActionGuide.SetInputActions(
                    _inputService.UI.PointDelta,
                    _inputService.UI.Previous,
                    _inputService.UI.Next,
                    _inputService.UI.ScrollWheel,
                    _inputService.UI.Previous2,
                    _inputService.UI.Next2,
                    _inputService.UI.Reset,
                    _inputService.UI.Cancel);

                await _previewView.InitializeAsync(modelAssetName, _previewImage, previewRotation);
            }
        }

        /// <summary>3D プレビューの回転・ズームを初期状態へ戻す（Dialog の Reset 入力から呼ぶ）。</summary>
        public void ResetPreview()
            => _previewView.ResetView();

        public override UniTask Terminate()
        {
            // PreviewRig は InitializeAsync で Canvas 外（ワールド退避位置）へ切り離されるため、
            // ダイアログの GameObject 階層破棄と連動しない。ここで明示的に破棄する
            if (_previewView != null) Destroy(_previewView.gameObject);

            return base.Terminate();
        }

        private void SetText()
        {
            _nameText.text = _localizationService.GetStringByPropTexts(_info.Name);
            _descriptionText.text = _localizationService.GetStringByPropTexts(_info.Description);
        }
    }
}
