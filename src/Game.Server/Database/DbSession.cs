using System.Data;

namespace Game.Server.Database;

/// <summary>
/// <see cref="IDbSession"/> の実装（Unit of Work パターン）。
/// コンストラクタで接続を開き、Scoped ライフタイムで共有する。
/// Dispose 時に未コミットのトランザクションは自動ロールバックされる。
/// </summary>
public class DbSession : IDbSession
{
    private readonly IDbConnection _connection;
    private IDbTransaction? _transaction;
    private bool _disposed;

    public DbSession(IDbConnectionFactory connectionFactory)
    {
        _connection = connectionFactory.CreateConnection();
    }

    public IDbConnection Connection => _connection;
    public IDbTransaction? Transaction => _transaction;

    public DbTransactionScope BeginScope(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        if (_transaction != null)
            throw new InvalidOperationException("Transaction is already started.");

        _transaction = _connection.BeginTransaction(isolationLevel);
        return new DbTransactionScope(this);
    }

    public void Commit()
    {
        if (_transaction == null)
            throw new InvalidOperationException("No active transaction to commit.");

        _transaction.Commit();
        _transaction.Dispose();
        _transaction = null;
    }

    public void Rollback()
    {
        if (_transaction == null)
            throw new InvalidOperationException("No active transaction to rollback.");

        _transaction.Rollback();
        _transaction.Dispose();
        _transaction = null;
    }

    /// <summary>
    /// 未コミットのトランザクションをロールバックし、DB コネクションを閉じる。
    /// 本番環境では ASP.NET Core の DI コンテナが HTTP リクエスト終了時に
    /// Scoped サービスとして自動的に呼び出す（明示呼び出し不要）。
    /// テストでは <see cref="IAsyncLifetime.DisposeAsync"/> 等で明示的に呼ぶこと。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _transaction?.Rollback();
        _transaction?.Dispose();
        _transaction = null;

        _connection.Dispose();
    }

    /// <inheritdoc cref="Dispose"/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _transaction?.Rollback();
        _transaction?.Dispose();
        _transaction = null;

        if (_connection is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            _connection.Dispose();
    }
}
