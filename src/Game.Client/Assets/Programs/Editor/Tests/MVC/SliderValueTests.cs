using System.Reflection;
using Game.Core.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.MVC
{
    [TestFixture]
    public class SliderValueTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"field {field} not found");
            f.SetValue(target, value);
        }

        /// <summary>
        /// FOV スライダー不具合の回帰テスト。
        /// prefab と同じく Slider 自身が min=60/max=120/value=60 で構成された状態で
        /// SliderValue(min=60, max=120, step=5, value=60) を Initialize すると、
        /// 初期値 60 が正しく適用されること（再スケーリング時のクランプと購読時即時発火で 120 に化けないこと）。
        /// </summary>
        [Test]
        public void Initialize_AppliesInspectorValue_WhenSliderPreconfiguredWithRealRange()
        {
            _go = new GameObject("SliderValueTest");
            var slider = _go.AddComponent<Slider>();
            slider.minValue = 60f;
            slider.maxValue = 120f;
            slider.wholeNumbers = true;
            slider.value = 60f;

            var sv = _go.AddComponent<SliderValue>();
            SetPrivate(sv, "_slider", slider);
            SetPrivate(sv, "_min", 60f);
            SetPrivate(sv, "_max", 120f);
            SetPrivate(sv, "_step", 5f);
            SetPrivate(sv, "_value", 60f);

            sv.Initialize();

            Assert.AreEqual(60f, sv.Value, 0.001f);
        }

        [Test]
        public void Initialize_AppliesMidValue_AndSnapsToStep()
        {
            _go = new GameObject("SliderValueTest");
            var slider = _go.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 180f;
            slider.wholeNumbers = true;
            slider.value = 0f;

            var sv = _go.AddComponent<SliderValue>();
            SetPrivate(sv, "_slider", slider);
            SetPrivate(sv, "_min", 0f);
            SetPrivate(sv, "_max", 180f);
            SetPrivate(sv, "_step", 5f);
            SetPrivate(sv, "_value", 90f);

            sv.Initialize();

            Assert.AreEqual(90f, sv.Value, 0.001f);
        }
    }
}
