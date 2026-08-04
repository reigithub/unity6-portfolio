using Game.Core.Services;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services.Interfaces;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Dialogs
{
    /// <summary>
    /// アイテム詳細ダイアログの SPECS 欄（武器のみ）。
    /// 威力・安定性・射撃精度・連射速度・装填速度を 0〜1 ゲージ（Slider）で表示し、
    /// 装填数のみ数値そのまま表示する。
    /// </summary>
    public class HorrorWeaponSpecsView : MonoBehaviour
    {
        [SerializeField] private Slider _powerGauge;
        [SerializeField] private Slider _stabilityGauge;
        [SerializeField] private Slider _accuracyGauge;
        [SerializeField] private Slider _fireRateGauge;
        [SerializeField] private Slider _reloadSpeedGauge;

        [SerializeField] private TextMeshProUGUI _specsLabel;
        [SerializeField] private TextMeshProUGUI _powerLabel;
        [SerializeField] private TextMeshProUGUI _stabilityLabel;
        [SerializeField] private TextMeshProUGUI _accuracyLabel;
        [SerializeField] private TextMeshProUGUI _fireRateLabel;
        [SerializeField] private TextMeshProUGUI _reloadSpeedLabel;
        [SerializeField] private TextMeshProUGUI _capacityLabel;
        [SerializeField] private TextMeshProUGUI _capacityValueText;

        // ゲージ正規化の基準値（暫定値。実データでの見た目調整により後日変更される想定）
        internal const float DamageBest = 100f;
        internal const float InstabilityWorst = 5f; // リコイル pitch(度) × recover(秒) の最悪基準
        internal const float SpreadWorst = 10f; // 拡散角の最悪基準（度）
        internal const float FireIntervalWorst = 2f;
        internal const float FireIntervalBest = 0.1f;
        internal const float ReloadWorst = 5f;
        internal const float ReloadBest = 0.5f;

        private ILocalizationService _localizationService;

        /// <summary>
        /// ローカライズサービスを解決し、ラベルを適用する。以後ロケール変更時も自動で再適用する。
        /// </summary>
        public void Initialize()
        {
            _localizationService = GameServiceManager.Resolve<ILocalizationService>();

            ApplyLabels();
            _localizationService.OnLocaleChanged
                .Subscribe(_ => ApplyLabels())
                .AddTo(this);
        }

        /// <summary>指定武器の調整値から各ゲージと装填数表示を更新する。</summary>
        /// <param name="master">表示対象の武器マスターデータ。</param>
        public void SetWeapon(HorrorWeaponMaster master)
        {
            if (_powerGauge != null) _powerGauge.value = CalculatePowerValue(master.Damage);
            if (_stabilityGauge != null) _stabilityGauge.value = CalculateStabilityValue(master.RecoilCameraPitch, master.RecoilRecoverSeconds);
            if (_accuracyGauge != null) _accuracyGauge.value = CalculateAccuracyValue(master.SpreadAngle);
            if (_fireRateGauge != null) _fireRateGauge.value = CalculateFireRateValue(master.FireInterval);
            if (_reloadSpeedGauge != null) _reloadSpeedGauge.value = CalculateReloadSpeedValue(master.ReloadDuration);

            if (_capacityValueText != null) _capacityValueText.text = master.MagazineSize.ToString();
        }

        /// <summary>SPECS 欄を非表示にする（武器以外のアイテム選択時）。</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void ApplyLabels()
        {
            if (_specsLabel != null) _specsLabel.text = _localizationService.GetStringByUITexts("ItemDetail_Specs");
            if (_powerLabel != null) _powerLabel.text = _localizationService.GetStringByUITexts("ItemDetail_Power");
            if (_stabilityLabel != null) _stabilityLabel.text = _localizationService.GetStringByUITexts("ItemDetail_Stability");
            if (_accuracyLabel != null) _accuracyLabel.text = _localizationService.GetStringByUITexts("ItemDetail_Accuracy");
            if (_fireRateLabel != null) _fireRateLabel.text = _localizationService.GetStringByUITexts("ItemDetail_FireRate");
            if (_reloadSpeedLabel != null) _reloadSpeedLabel.text = _localizationService.GetStringByUITexts("ItemDetail_ReloadSpeed");
            if (_capacityLabel != null) _capacityLabel.text = _localizationService.GetStringByUITexts("ItemDetail_Capacity");
        }

        /// <summary>威力ゲージ値（0〜1）を算出する。ダメージが基準値以上ならクランプして 1。</summary>
        internal static float CalculatePowerValue(int damage)
            => Mathf.Clamp01(damage / DamageBest);

        /// <summary>
        /// 安定性ゲージ値（0〜1）を算出する。リコイルの跳ね上げ角×収束秒数が大きいほど不安定＝値が小さくなる。
        /// </summary>
        internal static float CalculateStabilityValue(float recoilPitch, float recoverSeconds)
            => 1f - Mathf.Clamp01(recoilPitch * recoverSeconds / InstabilityWorst);

        /// <summary>射撃精度ゲージ値（0〜1）を算出する。拡散角が大きいほど精度が低くなる。</summary>
        internal static float CalculateAccuracyValue(float spreadAngle)
            => 1f - Mathf.Clamp01(spreadAngle / SpreadWorst);

        /// <summary>連射速度ゲージ値（0〜1）を算出する。発砲間隔が短いほど値が高い。</summary>
        internal static float CalculateFireRateValue(float fireInterval)
            => Normalize(fireInterval, FireIntervalWorst, FireIntervalBest);

        /// <summary>リロード速度ゲージ値（0〜1）を算出する。リロード時間が短いほど値が高い。</summary>
        internal static float CalculateReloadSpeedValue(float reloadDuration)
            => Normalize(reloadDuration, ReloadWorst, ReloadBest);

        /// <summary>
        /// worst〜best の範囲で value を 0〜1 に正規化する（小さいほど良い指標を高値ゲージへ反転する用途）。
        /// worst と best が等しい場合はゼロ除算を避けて 0 を返す。
        /// </summary>
        internal static float Normalize(float value, float worst, float best)
            => Mathf.Approximately(worst, best) ? 0f : Mathf.InverseLerp(worst, best, value);
    }
}
