using System.Data;

namespace Game.Server.Database;

/// <summary>
/// リクエストスコープの DB セッション（Unit of Work パターン）。
/// Scoped ライフタイムで全リポジトリが同一コネクションを共有し、
/// 複数書き込みをトランザクションで原子的に実行する。
/// </summary>
public interface IDbSession : IDisposable, IAsyncDisposable
{
    IDbConnection Connection { get; }
    IDbTransaction? Transaction { get; }
    void Commit();
    void Rollback();

    /// <summary>
    /// トランザクションスコープを開始する。
    /// <c>using var tx = _dbSession.BeginScope();</c> で囲み、
    /// 成功時は <see cref="DbTransactionScope.Commit"/> を呼ぶ。
    /// Commit されずに Dispose されると自動ロールバックされる。
    /// </summary>
    DbTransactionScope BeginScope(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
}

/// <summary>
/// <see cref="IDbSession.BeginScope"/> が返すトランザクションスコープ。
/// <c>using</c> で囲み、成功パスで <see cref="Commit"/> を呼ぶ。
/// 例外等で Commit されずに Dispose されると自動ロールバックされる。
/// </summary>
public sealed class DbTransactionScope : IDisposable
{
    private readonly IDbSession _session;
    private bool _committed;

    internal DbTransactionScope(IDbSession session)
    {
        _session = session;
    }

    public void Commit()
    {
        _session.Commit();
        _committed = true;
    }

    public void Dispose()
    {
        if (!_committed && _session.Transaction != null)
            _session.Rollback();
    }
}
