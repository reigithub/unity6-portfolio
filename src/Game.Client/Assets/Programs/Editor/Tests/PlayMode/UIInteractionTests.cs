using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// UI操作のPlayModeテスト
    /// ボタン、スライダー、入力フィールドの操作をテスト
    /// </summary>
    [TestFixture]
    public class UIInteractionTests
    {
        private GameObject _canvasObject;
        private Canvas _canvas;
        private EventSystem _eventSystem;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Canvas を作成
            _canvasObject = new GameObject("TestCanvas");
            _canvas = _canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvasObject.AddComponent<CanvasScaler>();
            _canvasObject.AddComponent<GraphicRaycaster>();

            // EventSystem を作成（InputSystem使用時はInputSystemUIInputModuleを使用）
            var eventSystemObj = new GameObject("EventSystem");
            _eventSystem = eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_canvasObject != null) Object.Destroy(_canvasObject);
            if (_eventSystem != null) Object.Destroy(_eventSystem.gameObject);

            yield return null;
        }

        /// <summary>
        /// ボタンクリックでイベントが発火することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Button_OnClick_InvokesCallback()
        {
            // Arrange
            var button = CreateButton("TestButton");
            bool clicked = false;
            button.onClick.AddListener(() => clicked = true);
            yield return null;

            // Act - プログラムからクリックをシミュレート
            button.onClick.Invoke();
            yield return null;

            // Assert
            Assert.IsTrue(clicked, "Button click callback should be invoked");
        }

        /// <summary>
        /// ボタンの無効化でクリックできないことを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Button_WhenDisabled_CannotClick()
        {
            // Arrange
            var button = CreateButton("DisabledButton");
            button.interactable = false;
            bool clicked = false;
            button.onClick.AddListener(() => clicked = true);
            yield return null;

            // Assert - interactable が false
            Assert.IsFalse(button.interactable, "Button should be non-interactable");

            // Note: onClick.Invoke() は interactable に関係なく呼ばれるため、
            // 実際のUI操作では IsInteractable() をチェックする必要がある
            Assert.IsFalse(button.IsInteractable(), "Button IsInteractable should return false");
        }

        /// <summary>
        /// スライダーの値変更が検出されることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Slider_ValueChange_InvokesCallback()
        {
            // Arrange
            var slider = CreateSlider("TestSlider", 0f, 100f, 50f);
            float newValue = 0f;
            slider.onValueChanged.AddListener(value => newValue = value);
            yield return null;

            // Act
            slider.value = 75f;
            yield return null;

            // Assert
            Assert.AreEqual(75f, newValue, 0.01f, "Slider callback should receive new value");
            Assert.AreEqual(75f, slider.value, 0.01f, "Slider value should be updated");
        }

        /// <summary>
        /// スライダーの範囲制限が機能することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Slider_ValueClamped_ToMinMax()
        {
            // Arrange
            var slider = CreateSlider("ClampedSlider", 0f, 100f, 50f);
            yield return null;

            // Act - 範囲外の値を設定
            slider.value = 150f;
            float valueAboveMax = slider.value;

            slider.value = -50f;
            float valueBelowMin = slider.value;

            // Assert
            Assert.AreEqual(100f, valueAboveMax, "Value should be clamped to max");
            Assert.AreEqual(0f, valueBelowMin, "Value should be clamped to min");
        }

        /// <summary>
        /// トグルの状態変更が検出されることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Toggle_StateChange_InvokesCallback()
        {
            // Arrange
            var toggle = CreateToggle("TestToggle", false);
            bool? newState = null;
            toggle.onValueChanged.AddListener(value => newState = value);
            yield return null;

            // Act
            toggle.isOn = true;
            yield return null;

            // Assert
            Assert.IsTrue(newState.HasValue, "Callback should be invoked");
            Assert.IsTrue(newState.Value, "New state should be true");
        }

        /// <summary>
        /// 入力フィールドのテキスト変更が検出されることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator InputField_TextChange_InvokesCallback()
        {
            // Arrange
            var inputField = CreateInputField("TestInputField");
            string changedText = null;
            inputField.onValueChanged.AddListener(text => changedText = text);
            yield return null;

            // Act
            inputField.text = "Hello World";
            yield return null;

            // Assert
            Assert.AreEqual("Hello World", changedText, "Callback should receive new text");
        }

        /// <summary>
        /// 入力フィールドの文字数制限が機能することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator InputField_CharacterLimit_Enforced()
        {
            // Arrange
            var inputField = CreateInputField("LimitedInputField");
            inputField.characterLimit = 10;
            yield return null;

            // Act
            inputField.text = "This is a very long text that exceeds the limit";
            yield return null;

            // Assert
            Assert.LessOrEqual(inputField.text.Length, 10, "Text should be limited to character limit");
        }

        /// <summary>
        /// ドロップダウンの選択変更が検出されることを確認
        /// </summary>
        [UnityTest]
        public IEnumerator Dropdown_SelectionChange_InvokesCallback()
        {
            // Arrange
            var dropdown = CreateDropdown("TestDropdown", new[] { "Option1", "Option2", "Option3" });
            int selectedIndex = -1;
            dropdown.onValueChanged.AddListener(index => selectedIndex = index);
            yield return null;

            // Act
            dropdown.value = 2;
            yield return null;

            // Assert
            Assert.AreEqual(2, selectedIndex, "Callback should receive selected index");
        }

        /// <summary>
        /// UI要素のアクティブ/非アクティブ切り替えが正常に動作することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator UIElement_ActiveToggle_Works()
        {
            // Arrange
            var button = CreateButton("ToggleableButton");
            yield return null;

            // Act & Assert - 非アクティブ化
            button.gameObject.SetActive(false);
            yield return null;
            Assert.IsFalse(button.gameObject.activeInHierarchy, "Button should be inactive");

            // Act & Assert - アクティブ化
            button.gameObject.SetActive(true);
            yield return null;
            Assert.IsTrue(button.gameObject.activeInHierarchy, "Button should be active");
        }

        /// <summary>
        /// UIのフェードイン/フェードアウトが動作することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator CanvasGroup_Fade_ChangesAlpha()
        {
            // Arrange
            var panel = new GameObject("FadePanel");
            panel.transform.SetParent(_canvasObject.transform);
            var image = panel.AddComponent<Image>();
            var canvasGroup = panel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            yield return null;

            // Act - フェードアウト
            yield return UniTask.ToCoroutine(async () =>
            {
                float duration = 0.5f;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                    await UniTask.Yield();
                }

                // Assert
                Assert.LessOrEqual(canvasGroup.alpha, 0.1f, "Alpha should be near 0 after fade out");
            });
        }

        /// <summary>
        /// ScrollRectのスクロールが動作することを確認
        /// </summary>
        [UnityTest]
        public IEnumerator ScrollRect_Scroll_ChangesPosition()
        {
            // Arrange
            var scrollRect = CreateScrollRect("TestScrollRect");
            yield return null;

            // 初期位置を1.0f（一番上）に設定
            scrollRect.verticalNormalizedPosition = 1f;
            yield return null;
            float initialPosition = scrollRect.verticalNormalizedPosition;

            // Act - 0.0f（一番下）に移動
            scrollRect.verticalNormalizedPosition = 0f;
            yield return null;

            // Assert
            Assert.AreNotEqual(initialPosition, scrollRect.verticalNormalizedPosition,
                "Scroll position should change");
        }

        #region Helper Methods

        private Button CreateButton(string name)
        {
            var buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(_canvasObject.transform);

            var rectTransform = buttonObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(160, 30);

            var image = buttonObj.AddComponent<Image>();
            var button = buttonObj.AddComponent<Button>();

            return button;
        }

        private Slider CreateSlider(string name, float min, float max, float initial)
        {
            var sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(_canvasObject.transform);

            var rectTransform = sliderObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(160, 20);

            var slider = sliderObj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = initial;

            return slider;
        }

        private Toggle CreateToggle(string name, bool initialState)
        {
            var toggleObj = new GameObject(name);
            toggleObj.transform.SetParent(_canvasObject.transform);

            var rectTransform = toggleObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(160, 20);

            var toggle = toggleObj.AddComponent<Toggle>();
            toggle.isOn = initialState;

            return toggle;
        }

        private InputField CreateInputField(string name)
        {
            var inputObj = new GameObject(name);
            inputObj.transform.SetParent(_canvasObject.transform);

            var rectTransform = inputObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(160, 30);

            var image = inputObj.AddComponent<Image>();

            // Text オブジェクト
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(inputObj.transform);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var text = textObj.AddComponent<Text>();

            // Unity 6ではLegacyRuntime.ttfを使用（Arial.ttfは非推奨）
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var inputField = inputObj.AddComponent<InputField>();
            inputField.textComponent = text;

            return inputField;
        }

        private Dropdown CreateDropdown(string name, string[] options)
        {
            var dropdownObj = new GameObject(name);
            dropdownObj.transform.SetParent(_canvasObject.transform);

            var rectTransform = dropdownObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(160, 30);

            var image = dropdownObj.AddComponent<Image>();
            var dropdown = dropdownObj.AddComponent<Dropdown>();

            // オプションを追加
            dropdown.options.Clear();
            foreach (var option in options)
            {
                dropdown.options.Add(new Dropdown.OptionData(option));
            }

            return dropdown;
        }

        private ScrollRect CreateScrollRect(string name)
        {
            var scrollObj = new GameObject(name);
            scrollObj.transform.SetParent(_canvasObject.transform);

            var rectTransform = scrollObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200, 200);

            var image = scrollObj.AddComponent<Image>();
            var scrollRect = scrollObj.AddComponent<ScrollRect>();

            // Content
            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(scrollObj.transform);
            var contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(200, 500);

            scrollRect.content = contentRect;
            scrollRect.vertical = true;
            scrollRect.horizontal = false;

            return scrollRect;
        }

        #endregion
    }
}
