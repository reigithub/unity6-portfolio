using Game.MVP.Survivor.Scenes;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.MVP.Survivor.UI
{
    /// <summary>
    /// ステージ選択アイテムのビュー
    /// </summary>
    public class StageSelectItemView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _stageNameText;
        [SerializeField] private TextMeshProUGUI _stageNumberText;
        [SerializeField] private GameObject _lockedIcon;
        [SerializeField] private GameObject _clearedIcon;
        [SerializeField] private GameObject[] _starIcons;
        [SerializeField] private Image _difficultyIcon;
        [SerializeField] private Image _thumbnailImage;

        [Header("Colors")]
        [SerializeField] private Color _unlockedColor = Color.white;
        [SerializeField] private Color _lockedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField] private Color _clearedColor = new Color(0.8f, 1f, 0.8f, 1f);

        private readonly Subject<Unit> _onClicked = new();
        private StageSelectItemData _data;

        public Observable<Unit> OnClicked => _onClicked;
        public StageSelectItemData Data => _data;

        private void Awake()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(() => _onClicked.OnNext(Unit.Default));
            }
        }

        private void OnDestroy()
        {
            _onClicked.Dispose();
        }

        public void Setup(StageSelectItemData data)
        {
            _data = data;

            // ステージ名
            if (_stageNameText != null)
            {
                _stageNameText.text = data.IsUnlocked ? data.StageName : "???";
            }

            // ステージ番号
            if (_stageNumberText != null)
            {
                _stageNumberText.text = data.StageId.ToString();
            }

            // ロックアイコン
            if (_lockedIcon != null)
            {
                _lockedIcon.SetActive(!data.IsUnlocked);
            }

            // クリアアイコン
            if (_clearedIcon != null)
            {
                _clearedIcon.SetActive(data.IsCleared);
            }

            // 星評価
            if (_starIcons != null)
            {
                for (int i = 0; i < _starIcons.Length; i++)
                {
                    if (_starIcons[i] != null)
                    {
                        _starIcons[i].SetActive(data.IsUnlocked && i < data.StarRating);
                    }
                }
            }

            // 背景色
            if (_button != null)
            {
                var colors = _button.colors;
                if (!data.IsUnlocked)
                {
                    colors.normalColor = _lockedColor;
                }
                else if (data.IsCleared)
                {
                    colors.normalColor = _clearedColor;
                }
                else
                {
                    colors.normalColor = _unlockedColor;
                }
                _button.colors = colors;
            }

            // 難易度アイコン（オプション）
            if (_difficultyIcon != null)
            {
                _difficultyIcon.gameObject.SetActive(data.IsUnlocked);
                // 難易度に応じて色を変更するなど
            }

            // ボタンのインタラクション
            if (_button != null)
            {
                _button.interactable = data.IsUnlocked;
            }
        }
    }
}
