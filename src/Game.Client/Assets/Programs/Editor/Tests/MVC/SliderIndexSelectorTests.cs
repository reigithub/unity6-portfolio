using System.Collections.Generic;
using System.Reflection;
using Game.Core.UI;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.MVC
{
    [TestFixture]
    public class SliderIndexSelectorTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"field {field} not found");
            f.SetValue(target, value);
        }

        /// <summary>
        /// 実 Slider + Button(prev/next) を組んだ SliderValueSelector を生成。
        /// EditMode のため Start は走らない。private 参照を注入し、SetOptions/Initialize を明示的に呼ぶ。
        /// _label は注入せず null のまま（コンポーネントが null ガードする。ラベル表示は Play で確認）。
        /// </summary>
        private SliderIndexSelector Build(string[] options, out Slider slider, out Button prev, out Button next)
        {
            _root = new GameObject("Root");

            slider = new GameObject("Slider").AddComponent<Slider>();
            slider.transform.SetParent(_root.transform);
            prev = new GameObject("Prev").AddComponent<Button>();
            prev.transform.SetParent(_root.transform);
            next = new GameObject("Next").AddComponent<Button>();
            next.transform.SetParent(_root.transform);

            var selector = _root.AddComponent<SliderIndexSelector>();
            SetPrivate(selector, "_slider", slider);
            SetPrivate(selector, "_prevButton", prev);
            SetPrivate(selector, "_nextButton", next);

            selector.SetLabels(options);
            selector.Initialize();
            return selector;
        }

        [Test]
        public void SetOptions_ConfiguresSliderRange()
        {
            var selector = Build(new[] { "A", "B", "C" }, out var slider, out _, out _);

            Assert.IsTrue(slider.wholeNumbers, "整数スライダーであること");
            Assert.AreEqual(0, slider.minValue, "minValue は 0");
            Assert.AreEqual(2, slider.maxValue, "maxValue は Count-1");
            Assert.AreEqual(3, selector.Count);
        }

        [Test]
        public void Next_AdvancesIndex_AndClampsAtEnd()
        {
            var selector = Build(new[] { "A", "B", "C" }, out _, out _, out var next);
            selector.SetIndex(0);

            next.onClick.Invoke();
            Assert.AreEqual(1, selector.Index);
            next.onClick.Invoke();
            Assert.AreEqual(2, selector.Index);
            next.onClick.Invoke(); // 末尾でクランプ（それ以上進まない）
            Assert.AreEqual(2, selector.Index);
        }

        [Test]
        public void Prev_GoesBack_AndClampsAtStart()
        {
            var selector = Build(new[] { "A", "B", "C" }, out _, out var prev, out _);
            selector.SetIndex(2);

            prev.onClick.Invoke();
            Assert.AreEqual(1, selector.Index);
            prev.onClick.Invoke();
            Assert.AreEqual(0, selector.Index);
            prev.onClick.Invoke(); // 先頭でクランプ
            Assert.AreEqual(0, selector.Index);
        }

        [Test]
        public void ButtonInteractable_TogglesAtEnds()
        {
            var selector = Build(new[] { "A", "B", "C" }, out _, out var prev, out var next);

            selector.SetIndex(0);
            Assert.IsFalse(prev.interactable, "先頭では < は無効");
            Assert.IsTrue(next.interactable, "先頭では > は有効");

            selector.SetIndex(1);
            Assert.IsTrue(prev.interactable, "中間では両方有効");
            Assert.IsTrue(next.interactable);

            selector.SetIndex(2);
            Assert.IsTrue(prev.interactable, "末尾では < は有効");
            Assert.IsFalse(next.interactable, "末尾では > は無効");
        }

        [Test]
        public void OnValueChanged_FiresWhenIndexChanges()
        {
            var selector = Build(new[] { "A", "B", "C" }, out _, out _, out var next);
            selector.SetIndex(0);

            var received = new List<int>();
            using var sub = selector.OnValueChanged.Subscribe(received.Add);

            selector.SetIndex(1);          // 値が変わるので発火
            Assert.AreEqual(1, received.Count, "SetIndex で値が変われば発火");
            Assert.AreEqual(1, received[0], "発火値は新しい index");

            next.onClick.Invoke();         // ステップでも発火
            Assert.AreEqual(2, received.Count, "ステップでも発火");
            Assert.AreEqual(2, received[1], "発火値は新しい index");
        }
    }
}
