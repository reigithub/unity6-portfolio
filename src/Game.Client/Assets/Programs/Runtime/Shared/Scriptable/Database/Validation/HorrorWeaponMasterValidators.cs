#if UNITY_EDITOR
using Game.Shared.Enums;
using Game.Shared.Scriptable.Database.Tables;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// 投擲武器（WeaponType = Throwable）の必須パラメータを検証する。
    /// 未設定のままだとランタイムで投擲不能（LogError で不発）になるため、編集時に決定的に検出する。
    /// </summary>
    public sealed class HorrorWeaponMasterThrowableValidator : IRecordValidator<HorrorWeaponMaster>
    {
        public void Validate(HorrorWeaponMaster record, ValidationResult result, IRecordGetter recordGetter)
        {
            if (record.WeaponType != HorrorWeaponType.Throwable) return;

            if (string.IsNullOrEmpty(record.ProjectileAssetName))
                result.AddError(record.Id.ToString(), "Throwable 武器に ProjectileAssetName が設定されていません。");

            if (record.ThrowSpeed <= 0f)
                result.AddError(record.Id.ToString(), $"Throwable 武器の ThrowSpeed（{record.ThrowSpeed}）は正の値が必要です。");

            // 90 度超は視線の背後へ投げることになり、投擲動作として成立しない
            if (record.ThrowPitchOffset < 0f || record.ThrowPitchOffset > 90f)
                result.AddError(record.Id.ToString(), $"Throwable 武器の ThrowPitchOffset（{record.ThrowPitchOffset}）は 0〜90 度の範囲が必要です。");

            if (record.FuseSeconds < 0f)
                result.AddError(record.Id.ToString(), $"Throwable 武器の FuseSeconds（{record.FuseSeconds}）は 0 以上が必要です。");

            if (record.EffectRadius <= 0f)
                result.AddError(record.Id.ToString(), $"Throwable 武器の EffectRadius（{record.EffectRadius}）は正の値が必要です。");

            if (record.EffectDurationSeconds <= 0f)
                result.AddError(record.Id.ToString(), $"Throwable 武器の EffectDurationSeconds（{record.EffectDurationSeconds}）は正の値が必要です。");
        }
    }
}
#endif
