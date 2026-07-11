using System;
using Game.Core.Services;
using Game.Shared.Extensions;
using R3;
using TMPro;
using UnityEngine;

namespace Game.Core.UI
{
    /// <summary>
    /// 子 TabGroup を「十字キー/スティック左右(Navigate.x)」で切り替える自己完結ドライバ。
    /// 初期化・入力購読・購読破棄をすべて内包し、ダイアログ側・共通 TabGroup 側は無改変で利用する。
    ///
    /// 配置: サブタブを持つ親タブの Content 配下に置く。親 TabGroup.ChangeTab() が
    /// その Content を SetActive(true/false) するため、OnEnable/OnDisable がそのまま
    /// 「サブタブ領域にいる時だけ左右を購読する」ライフサイクルになる。
    ///
    /// 入力仕様（実測に基づく）: UI/Navigate は PassThrough/Vector2 で started/canceled は
    /// 発火せず、値変化のたび performed のみ発火する（ニュートラル復帰時も performed(0,0)）。
    /// このため押下検知・リリース検知の双方を performed の値で行い、_latched で1入力1切替に正規化する。
    /// </summary>
    public class TabNavigator : MonoBehaviour
    {
        [SerializeField] private TabGroup _tabGroup;

        // 左右入力とみなす最小の x 絶対値（微小入力・デッドゾーン残差を除外）
        [SerializeField] private float _threshold = 0.5f;

        private IInputSystemService _inputService;
        private IInputSystemService InputService => _inputService ??= GameServiceManager.Resolve<IInputSystemService>();

        private IDisposable _subscription;

        // 立ち上がりエッジ検出用。左右が閾値を超えている間 true にして連続切替を抑止し、
        // ニュートラル/上下に戻った performed で false に戻す。
        private bool _latched;

        private void Start()
        {
            // 初期化は初回のみ。子タブを構成し先頭タブを選択状態にする。
            _tabGroup.Initialize();
            _tabGroup.ChangeTab(0);
        }

        private void OnEnable()
        {
            // 親 Content が表示された時だけ Navigate を購読する。
            // 購読は performed のみ（PassThrough のため started/canceled は来ない）。
            _subscription = InputService.UI.Navigate
                .OnPerformedAsObservable()
                .Subscribe(_ => OnNavigate());
        }

        private void OnDisable()
        {
            // 親タブが切り替わって Content が非表示になったら購読解除。
            _subscription?.Dispose();
            _subscription = null;
            _latched = false;
        }

        private void OnNavigate()
        {
            var v = InputService.UI.Navigate.ReadValue<Vector2>();

            // ニュートラル復帰、または上下が優勢な入力では切替しない。
            // 上下優勢の判定は ">"（x==y の斜めは左右として扱い、斜め右で確実に発火させる）。
            // performed(0,0) がニュートラル復帰時に必ず来るので、ここで _latched が解除される。
            if (Mathf.Abs(v.x) < _threshold || Mathf.Abs(v.y) > Mathf.Abs(v.x))
            {
                _latched = false;
                return;
            }

            // 立ち上がりエッジでのみ1回切替（押しっぱなし・スティック微振動での多段切替を防ぐ）。
            if (_latched) return;
            _latched = true;

            if (v.x > 0) _tabGroup.NextTab();
            else _tabGroup.PreviousTab();
        }
    }
}
