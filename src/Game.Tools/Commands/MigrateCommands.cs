using Game.Server.Database;
using Game.Tools.Data;
using Spectre.Console;

namespace Game.Tools.Commands;

public class MigrateCommands
{
    /// <summary>
    /// Run pending database migrations.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string. Falls back to appsettings.json if omitted.</param>
    /// <param name="schema">Target schema (master, user, all). Omit for all schemas.</param>
    public void Up(string connectionString = "", string schema = "")
    {
        var cs = AppConfig.ResolveConnectionString(connectionString);
        foreach (var s in ResolveSchemas(schema))
        {
            AnsiConsole.MarkupLine($"[blue]Running migrations for schema '{s}'...[/]");
            MigrationRunnerFactory.MigrateUp(cs, s);
        }
        AnsiConsole.MarkupLine("[green]Migration completed successfully.[/]");
    }

    /// <summary>
    /// Rollback the last database migration.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string. Falls back to appsettings.json if omitted.</param>
    /// <param name="steps">Number of migrations to rollback.</param>
    /// <param name="schema">Target schema (master, user, all). Omit for all schemas.</param>
    public void Down(string connectionString = "", int steps = 1, string schema = "")
    {
        var cs = AppConfig.ResolveConnectionString(connectionString);
        // Down は逆順で実行
        foreach (var s in ResolveSchemas(schema).Reverse())
        {
            AnsiConsole.MarkupLine($"[blue]Rolling back schema '{s}' ({steps} step(s))...[/]");
            MigrationRunnerFactory.Rollback(cs, s, steps);
        }
        AnsiConsole.MarkupLine($"[green]Rolled back {steps} migration(s).[/]");
    }

    /// <summary>
    /// Show current migration status.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string. Falls back to appsettings.json if omitted.</param>
    /// <param name="schema">Target schema (master, user, all). Omit for all schemas.</param>
    public void Status(string connectionString = "", string schema = "")
    {
        var cs = AppConfig.ResolveConnectionString(connectionString);
        foreach (var s in ResolveSchemas(schema))
        {
            AnsiConsole.MarkupLine($"[bold]── Schema: {s} ──[/]");
            MigrationRunnerFactory.ListMigrations(cs, s);
        }
    }

    /// <summary>
    /// Reset database by dropping schemas and optionally re-applying migrations.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string. Falls back to appsettings.json if omitted.</param>
    /// <param name="version">Target migration version to re-apply up to. 0 = drop only (skip MigrateUp).</param>
    /// <param name="seed">Re-seed master data after reset.</param>
    /// <param name="force">Skip confirmation prompt.</param>
    /// <param name="schema">Target schema (master, user, all). Omit for all schemas.</param>
    public void Reset(string connectionString = "", long version = 0, bool seed = false, bool force = false, string schema = "")
    {
        if (!force)
        {
            var confirmed = AnsiConsole.Confirm(
                "[yellow]This will drop all tables and re-create them. Continue?[/]",
                defaultValue: false);
            if (!confirmed)
            {
                AnsiConsole.MarkupLine("[yellow]Aborted.[/]");
                return;
            }
        }

        var cs = AppConfig.ResolveConnectionString(connectionString);
        var schemas = ResolveSchemas(schema);

        // Drop schemas via raw SQL (逆順)
        foreach (var s in schemas.Reverse())
        {
            AnsiConsole.MarkupLine($"[yellow]Dropping schema '{s}'...[/]");
            MigrationRunnerFactory.DropSchema(cs, s);
        }

        // Re-apply: 正順
        if (version > 0)
        {
            foreach (var s in schemas)
            {
                AnsiConsole.MarkupLine($"[blue]Re-applying migrations for schema '{s}' up to version {version}...[/]");
                MigrationRunnerFactory.MigrateUp(cs, s, version);
            }
        }

        if (seed)
        {
            AnsiConsole.MarkupLine("[blue]Seeding master data...[/]");
            var seeder = new DatabaseSeeder();
            seeder.Seed(cs, "masterdata/raw/", [MigrationSchema.Master]);
        }

        AnsiConsole.MarkupLine("[green]Database reset completed successfully.[/]");
    }

    private static string[] ResolveSchemas(string schema)
        => MigrationSchema.ResolveSchemas(schema);
}
