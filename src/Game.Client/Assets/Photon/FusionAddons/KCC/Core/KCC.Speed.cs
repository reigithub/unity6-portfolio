namespace Fusion.Addons.KCC
{
	/// <summary>
	/// KCC 拡張: 目標速度の設定。
	/// _fixedData と _renderData の両方に書き込み、ロールバック時の整合性を保証する。
	/// </summary>
	public partial class KCC
	{
		public void SetSpeed(float speed)
		{
			_renderData.Speed = speed;

			if (IsInFixedUpdate == true)
			{
				_fixedData.Speed = speed;
			}
		}
	}
}
