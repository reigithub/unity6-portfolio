namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// EnemyProxyTarget の IsDead lookup を抽象化。
    /// 本番は SurvivorEnemyView が実装、テスト時は Dictionary lookup コストを再現した Mock を差し替える。
    /// </summary>
    public interface IEnemyDeathQuery
    {
        bool IsProxyDead(int networkId);
    }
}
