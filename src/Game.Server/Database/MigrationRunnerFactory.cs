using FluentMigrator;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using Game.Server.Database.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Game.Server.Database;

public static class MigrationRunnerFactory
{
    /// <summary>
    /// 全マイグレーションを最新まで適用する。
    /// </summary>
    public static void MigrateUp(string connectionString, string schema)
    {
        using var sp = BuildServiceProvider(connectionString, schema);
        sp.GetRequiredService<IMigrationRunner>().MigrateUp();
        sp.GetRequiredService<IMigrationProcessor>().CommitTransaction();
    }

    /// <summary>
    /// 指定バージョンまでマイグレーションを適用する。
    /// </summary>
    public static void MigrateUp(string connectionString, string schema, long version)
    {
        using var sp = BuildServiceProvider(connectionString, schema);
        sp.GetRequiredService<IMigrationRunner>().MigrateUp(version);
        sp.GetRequiredService<IMigrationProcessor>().CommitTransaction();
    }

    /// <summary>
    /// 指定バージョンまでロールバックする。
    /// </summary>
    public static void MigrateDown(string connectionString, string schema, long version)
    {
        using var sp = BuildServiceProvider(connectionString, schema);
        sp.GetRequiredService<IMigrationRunner>().MigrateDown(version);
        sp.GetRequiredService<IMigrationProcessor>().CommitTransaction();
    }

    /// <summary>
    /// 指定ステップ数だけロールバックする。
    /// </summary>
    public static void Rollback(string connectionString, string schema, int steps)
    {
        using var sp = BuildServiceProvider(connectionString, schema);
        var runner = sp.GetRequiredService<IMigrationRunner>();
        for (int i = 0; i < steps; i++)
            runner.Rollback(1);
        sp.GetRequiredService<IMigrationProcessor>().CommitTransaction();
    }

    /// <summary>
    /// マイグレーションの適用状態を表示する。
    /// </summary>
    public static void ListMigrations(string connectionString, string schema)
    {
        using var sp = BuildServiceProvider(connectionString, schema);
        sp.GetRequiredService<IMigrationRunner>().ListMigrations();
    }

    /// <summary>
    /// スキーマを DROP CASCADE で削除する（Reset 用）。
    /// </summary>
    public static void DropSchema(string connectionString, string schema)
    {
        using var conn = new Npgsql.NpgsqlConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
        cmd.ExecuteNonQuery();
    }

    private static ServiceProvider BuildServiceProvider(string connectionString, string schema)
    {
        // マイグレーション用接続はプーリングを無効にして
        // ServiceProvider 破棄時のトランザクション競合を防ぐ
        var csb = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
        };

        return new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddPostgres()
                .WithGlobalConnectionString(csb.ConnectionString)
                .ScanIn(typeof(_2026020100010001_CreateMasterSchema).Assembly).For.Migrations()
                .WithVersionTable(new SchemaVersionTableMetaData(schema)))
            .Configure<RunnerOptions>(opt => opt.Tags = new[] { schema })
            .AddLogging(lb => lb.AddFluentMigratorConsole())
            .BuildServiceProvider(false);
    }
}
