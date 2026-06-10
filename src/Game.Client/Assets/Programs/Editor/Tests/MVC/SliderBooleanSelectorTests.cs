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
    public class SliderBooleanSelectorTests
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
        /// 実 Slider + Button(prev/next) を組んだ SliderBooleanSelector を生成。
        /// EditMode のため Start は走らない。private 参照を注入し、SetLabels/Initialize を明示的に呼ぶ。
        /// _label は注入せず null のまま（コンポーネントが null ガードする）。
        /// </summary>
        private SliderBooleanSelector Build(out Slider slider, out Button prev, out Button next)
        {
            _root = new GameObject("Root");

            slider = new GameObject("Slider").AddComponent<Slider>();
            slider.transform.SetParent(_root.transform);
            prev = new GameObject("Prev").AddComponent<Button>();
            prev.transform.SetParent(_root.transform);
            next = new GameObject("Next").AddComponent<Button>();
            next.transform.SetParent(_root.transform);

            var selector = _root.AddComponent<SliderBooleanSelector>();
            SetPrivate(selector, "_slider", slider);
            SetPrivate(selector, "_prevButton", prev);
            SetPrivate(selector, "_nextButton", next);

            selector.SetLabels(new[] { "OFF", "ON" });
            selector.Initialize();
            return selector;
        }

        [Test]
        public void Configure_SetsBooleanRange()
        {
            Build(out var slider, out _, out _);

            Assert.IsTrue(slider.wholeNumbers, "整数スライダーであること");
            Assert.AreEqual(0, slider.minValue, "minValue は 0");
            Assert.AreEqual(1, slider.maxValue, "maxValue は 1（bool）");
        }

        [Test]
        public void SetValue_TogglesValue()
        {
            var selector = Build(out _, out _, out _);

            selector.SetBool(true);
            Assert.IsTrue(selector.IsOn);

            selector.SetBool(false);
            Assert.IsFalse(selector.IsOn);
        }

        [Test]
        public void NextSetsOn_PrevSetsOff()
        {
            var selector = Build(out _, out var prev, out var next);
            selector.SetBool(false);

            next.onClick.Invoke();
            Assert.IsTrue(selector.IsOn, "> で ON");

            prev.onClick.Invoke();
            Assert.IsFalse(selector.IsOn, "< で OFF");
        }

        [Test]
        public void ButtonInteractable_TogglesAtEnds()
        {
            var selector = Build(out _, out var prev, out var next);

            selector.SetBool(false);
            Assert.IsFalse(prev.interactable, "OFF では < は無効");
            Assert.IsTrue(next.interactable, "OFF では > は有効");

            selector.SetBool(true);
            Assert.IsTrue(prev.interactable, "ON では < は有効");
            Assert.IsFalse(next.interactable, "ON では > は無効");
        }

        [Test]
        public void OnValueChanged_FiresOnChange()
        {
            var selector = Build(out _, out _, out _);
            selector.SetBool(false);

            var received = new List<bool>();
            using var sub = selector.OnValueChanged.Subscribe(received.Add);

            selector.SetBool(true);   // false → true で発火
            Assert.AreEqual(1, received.Count, "値が変われば発火");
            Assert.IsTrue(received[0], "発火値は新しい値");
        }
    }
}
