namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// 敵システムの実装モード
    /// Inspector上のEnum切り替えでA/B比較可能
    /// </summary>
    public enum EnemySystemMode
    {
        /// <summary>従来のMonoBehaviourベース実装</summary>
        MonoBehaviour = 0,

        /// <summary>ECS + Jobs + Burst並列実装</summary>
        ECS = 1
    }
}
