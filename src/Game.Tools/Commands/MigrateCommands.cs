using FluentMigrator.Runner;
using Game.Server.Database.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Game.Tools.Commands;

public class MigrateCommands
{
    /// <summary>
    /// Run pending database migrations.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    public void Up(string connectionString)
    {
        using var sp = BuildServiceProvider(connectionString);
        var runner = sp.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
        AnsiConsole.MarkupLine("[green]Migration completed successfully.[/]");
    }

    /// <summary>
    /// Rollback the last database migration.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="steps">Number of migrations to rollback.</param>
    public void Down(string connectionString, int steps = 1)
    {
        using var sp = BuildServiceProvider(connectionString);
        var runner = sp.GetRequiredService<IMigrationRunner>();

        for (int i = 0; i < steps; i++)
        {
            runner.Rollback(1);
        }

        AnsiConsole.MarkupLine($"[green]Rolled back {steps} migration(s).[/]");
    }

    /// <summary>
    /// Show current migration status.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    public void Status(string connectionString)
    {
        using var sp = BuildServiceProvider(connectionString);
        var runner = sp.GetRequiredService<IMigrationRunner>();
        runner.ListMigrations();
    }

    private static ServiceProvider BuildServiceProvider(string connectionString)
    {
        return new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(M0001_CreateMasterSchema).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole())
            .BuildServiceProvider(false);
    }
}
