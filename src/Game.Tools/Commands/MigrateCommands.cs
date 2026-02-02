using FluentMigrator.Runner;
using Game.Server.Database.Migrations;
using Game.Tools.Data;
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

    /// <summary>
    /// Seed database tables from TSV files.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="tsvDir">Directory containing TSV files.</param>
    /// <param name="protoDir">Directory containing .proto definitions.</param>
    /// <param name="userOnly">Only seed User schema tables.</param>
    /// <param name="masterOnly">Only seed Master schema tables.</param>
    public void Seed(string connectionString, string tsvDir = "masterdata/raw/", string protoDir = "masterdata/proto/", bool userOnly = false, bool masterOnly = false)
    {
        if (userOnly && masterOnly)
        {
            AnsiConsole.MarkupLine("[red]Cannot specify both --user-only and --master-only.[/]");
            Environment.ExitCode = 1;
            return;
        }

        AnsiConsole.MarkupLine($"[blue]TSV directory:[/] {Path.GetFullPath(tsvDir)}");
        AnsiConsole.MarkupLine($"[blue]Connection:[/] {MaskConnectionString(connectionString)}");

        var seeder = new DatabaseSeeder();
        seeder.Seed(connectionString, tsvDir, protoDir, userOnly, masterOnly);
    }

    /// <summary>
    /// Dump database tables to TSV files.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="outDir">Output directory for TSV files.</param>
    /// <param name="protoDir">Directory containing .proto definitions.</param>
    /// <param name="userOnly">Only dump User schema tables.</param>
    /// <param name="masterOnly">Only dump Master schema tables.</param>
    public void Dump(string connectionString, string outDir, string protoDir = "masterdata/proto/", bool userOnly = false, bool masterOnly = false)
    {
        if (userOnly && masterOnly)
        {
            AnsiConsole.MarkupLine("[red]Cannot specify both --user-only and --master-only.[/]");
            Environment.ExitCode = 1;
            return;
        }

        AnsiConsole.MarkupLine($"[blue]Output directory:[/] {Path.GetFullPath(outDir)}");
        AnsiConsole.MarkupLine($"[blue]Connection:[/] {MaskConnectionString(connectionString)}");

        var dumper = new DatabaseDumper();
        dumper.Dump(connectionString, outDir, userOnly, masterOnly);
    }

    private static string MaskConnectionString(string connectionString)
    {
        // Mask password in connection string for display
        var parts = connectionString.Split(';');
        var masked = parts.Select(p =>
            p.TrimStart().StartsWith("Password", StringComparison.OrdinalIgnoreCase)
                ? "Password=***"
                : p);
        return string.Join(";", masked);
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
