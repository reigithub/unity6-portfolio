namespace Fusion.Addons.KCC
{
	/// <summary>
	/// KCCData 拡張: 外部から設定される目標速度。
	/// CopyUserDataFromOther でロールバック対応。
	/// </summary>
	public partial class KCCData
	{
		public float Speed;

		partial void CopyUserDataFromOther(KCCData other)
		{
			Speed = other.Speed;
		}
	}
}
