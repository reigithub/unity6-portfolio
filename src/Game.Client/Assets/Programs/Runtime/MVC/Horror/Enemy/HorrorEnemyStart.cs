using UnityEngine;

namespace Game.Horror.Enemy
{
    /// <summary>
    /// 敵スポーン地点マーカー。位置・向きと、対応するスポーンエントリの ID のみを持つ。
    /// 生成実行は <see cref="HorrorEnemySpawner"/> が行う（シーン側が走査してスポナーへ渡す）。
    /// HorrorPlayerStart と同じ「シーンに配置 → シーン側が走査」する作法に倣う。
    /// </summary>
    public class HorrorEnemyStart : MonoBehaviour
    {
        [Tooltip("スポーンエントリの ID（HorrorEnemySpawnMasterTable の PrimaryKey）。0 は未設定")]
        [SerializeField] private int _spawnId;

        /// <summary>スポーンエントリの ID（スポナーの registry 構築・検証に使用）</summary>
        public int SpawnId => _spawnId;
    }
}
