using System.Reflection;
using System.Text.RegularExpressions;
using Game.Core.Services;
using Game.Horror.Interaction;
using Game.Shared.Enums;
using Game.Shared.Input;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services.Interfaces;
using NSubstitute;
using NUnit.Framework;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests.MVC.Horror.Interaction
{
    [TestFixture]
    public class InteractionPromptPoolTests
    {
        private InteractionPromptPool _pool;
        private GameObject _poolGo;
        private InteractionPromptView _prefab;
        private GameObject _anchorGo;

        // Construct の購読が実 InputAction（Player.Interact）を要求するため、実アセットを生成して保持する。
        // 生成された ProjectInputActions.Dispose() は Object.Destroy を呼ぶため EditMode では使えず、
        // TearDown で内部アセットを DestroyImmediate する。
        private ProjectInputActions _inputActions;

        [TearDown]
        public void TearDown()
        {
            // GameServiceManager.StartUp();
            // var addressableAssetService = new AddressableAssetService();
            // var localizationService = new LocalizationService();
            // GameServiceManager.Register<IInputSystemService, InputSystemService>(new InputSystemService(localizationService));
            // GameServiceManager.Register<IInputActionIconService, InputActionIconService>(new InputActionIconService(addressableAssetService));

            if (_anchorGo != null) Object.DestroyImmediate(_anchorGo);
            if (_poolGo != null) Object.DestroyImmediate(_poolGo);
            if (_prefab != null) Object.DestroyImmediate(_prefab.gameObject);
            if (_inputActions != null)
            {
                Object.DestroyImmediate(_inputActions.asset);
                _inputActions = null;
            }
        }

        // ---- フェイクサービス（NSubstitute。アセンブリ標準の手法に合わせる） ----

        // GetStringByContextActions はキーをそのまま返すため、Bind 時の動詞キー選択（toggle 反映）を
        // テキストの一致で直接検証できる
        private static ILocalizationService CreateLocalizationSubstitute()
        {
            var localization = Substitute.For<ILocalizationService>();
            localization.OnLocaleChanged.Returns(new Subject<Locale>());
            localization.GetStringByContextActions(Arg.Any<string>()).Returns(ci => ci.Arg<string>());
            return localization;
        }

        // Construct が購読する Observable 群と Player.Interact 参照のみ意味を持たせる
        private IInputSystemService CreateInputServiceSubstitute()
        {
            _inputActions = new ProjectInputActions();

            var input = Substitute.For<IInputSystemService>();
            input.Player.Returns(_inputActions.Player);
            input.OnControlSchemeChanged.Returns(new Subject<string>());
            input.OnDeviceChanged.Returns(new Subject<InputDeviceChangeInfo>());
            input.OnBindingChanged.Returns(new Subject<InputAction>());
            input.GetBindingDisplayString(Arg.Any<InputAction>(), Arg.Any<string>()).Returns("TestBinding");
            input.GetBindingInfo(Arg.Any<InputAction>(), Arg.Any<string>()).Returns(new InputBindingInfo());
            return input;
        }

        // ---- ヘルパー ----

        // 貸出・Bind に必要な参照（RectTransform ルート・動詞テキスト・入力表示テキスト）を最小限だけ配線したプレハブ雛形。
        // InteractionPromptView.Awake が transform を RectTransform へキャストするため RectTransform は必須。
        private static InteractionPromptView CreatePromptPrefab()
        {
            var go = new GameObject("PromptViewPrefab", typeof(RectTransform));
            var view = go.AddComponent<InteractionPromptView>();

            var interactionTextGo = new GameObject("InteractionText", typeof(RectTransform));
            interactionTextGo.transform.SetParent(go.transform);
            var interactionText = interactionTextGo.AddComponent<TextMeshProUGUI>();

            var inputBindingTextGo = new GameObject("InputBindingText", typeof(RectTransform));
            inputBindingTextGo.transform.SetParent(go.transform);
            var inputBindingText = inputBindingTextGo.AddComponent<TextMeshProUGUI>();

            var holdGaugeGo = new GameObject("HoldGauge");
            holdGaugeGo.transform.SetParent(go.transform);
            var holdGauge = holdGaugeGo.AddComponent<Image>();

            SetPrivateField(view, "_interactionText", interactionText);
            SetPrivateField(view, "_inputBindingText", inputBindingText);
            SetPrivateField(view, "_holdGauge", holdGauge);

            return view;
        }

        // プールを生成し、プレハブ・prewarm 数（reflection 注入）・フェイクサービスで初期化してフィールドへ保持する
        private InteractionPromptPool CreateInitializedPool(int prewarmCount)
        {
            _poolGo = new GameObject("Pool");
            _pool = _poolGo.AddComponent<InteractionPromptPool>();
            _prefab = CreatePromptPrefab();

            SetPrivateField(_pool, "_promptPrefab", _prefab);
            SetPrivateField(_pool, "_prewarmCount", prewarmCount);

            // _pool.Initialize(CreateLocalizationSubstitute(), CreateInputServiceSubstitute());
            _pool.Initialize();
            return _pool;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            return (T)target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(target);
        }

        private static string GetInteractionText(InteractionPromptView view)
            => GetPrivateField<TextMeshProUGUI>(view, "_interactionText").text;

        private static HorrorInteractionMaster CreateMaster() => new()
        {
            Id = 1,
            InteractionVerbLocalizeKey = "VerbOpen",
            ReinteractionVerbLocalizeKey = "VerbClose",
            InputType = InteractionInputType.Instant,
        };

        // ---- Rent / Return ----

        // Return したインスタンスは、次の Rent で再利用される（同一参照）。
        // 待機列は FIFO のため、同一参照を保証できるのは待機が返却分のみになる prewarm=1 の構成
        [Test]
        public void Rent_AfterReturn_ReusesSameInstance()
        {
            var pool = CreateInitializedPool(prewarmCount: 1);

            var first = pool.Rent();
            pool.Return(first);
            var second = pool.Rent();

            Assert.That(second, Is.SameAs(first));
        }

        // 同一インスタンスを2回返却すると、二重返却として LogError で顕在化し（無音で握りつぶさない）、待機列も汚染しない。
        // 待機列の重複不在を「再 Rent が同一参照になる」ことで検証するため prewarm=1 の構成にする
        [Test]
        public void Return_CalledTwice_LogsErrorAndKeepsQueueConsistent()
        {
            var pool = CreateInitializedPool(prewarmCount: 1);

            var view = pool.Rent();
            pool.Return(view);

            LogAssert.Expect(LogType.Error, new Regex("二重返却"));
            pool.Return(view);

            // 待機列は1件のまま（二重返却で重複登録されていないこと）を、再 Rent が新規生成にならないことで確認する
            var rentedAgain = pool.Rent();
            Assert.That(rentedAgain, Is.SameAs(view));
        }

        // prewarm 数を超えて Rent すると Warning ログで顕在化しつつ、追加生成して動作を継続する
        [Test]
        public void Rent_BeyondPrewarmCount_LogsWarningAndCreatesAdditionalInstance()
        {
            var pool = CreateInitializedPool(prewarmCount: 1);

            var first = pool.Rent();

            LogAssert.Expect(LogType.Warning, new Regex("prewarm"));
            var second = pool.Rent();

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second, Is.Not.Null);
        }

        // null の返却は呼び出し側の貸出参照管理の欠陥として LogError で顕在化する
        [Test]
        public void Return_Null_LogsError()
        {
            var pool = CreateInitializedPool(prewarmCount: 1);

            LogAssert.Expect(LogType.Error, new Regex("null"));
            pool.Return(null);
        }

        // ---- Bind / Unbind ----

        // Bind 時、interactionToggle=false なら通常動詞キー、true なら再インタラクト動詞キーがテキストへ反映される
        [Test]
        public void Bind_ReflectsInteractionToggle_IntoVerbText()
        {
            var pool = CreateInitializedPool(prewarmCount: 2);
            _anchorGo = new GameObject("Anchor");

            var view = pool.Rent();
            var master = CreateMaster();

            view.Bind(master, _anchorGo.transform, interactionToggle: false);
            Assert.That(GetInteractionText(view), Is.EqualTo("VerbOpen"));

            view.Bind(master, _anchorGo.transform, interactionToggle: true);
            Assert.That(GetInteractionText(view), Is.EqualTo("VerbClose"));
        }

        // Unbind 後も SetHoldProgress は例外を投げず安全に動作する（InteractableBase は _rentedView != null の場合のみ
        // 転送するため通常到達しないが、View 単体としての不変条件として固定する）
        [Test]
        public void SetHoldProgress_AfterUnbind_DoesNotThrow()
        {
            var pool = CreateInitializedPool(prewarmCount: 2);
            _anchorGo = new GameObject("Anchor");

            var view = pool.Rent();
            view.Bind(CreateMaster(), _anchorGo.transform, interactionToggle: false);
            pool.Return(view);

            Assert.DoesNotThrow(() => view.SetHoldProgress(0.5f));
        }
    }
}
