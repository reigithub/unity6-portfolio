namespace Game.Horror.Enums
{
    /// <summary>
    /// UI操作効果音の種別。None はマーカーの「鳴らさない」設定値で、再生要求としては使わない
    /// （マーカー側で Play を呼ばない契約。Profile.Play への到達は LogError で顕在化する）。
    /// </summary>
    public enum HorrorUISoundType
    {
        None,
        Submit,
        Cancel,
        Select,
        TabChanged,
        ValueChanged,
    }
}
