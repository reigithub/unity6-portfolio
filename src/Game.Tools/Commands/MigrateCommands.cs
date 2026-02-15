using Game.Server.Database;
using Game.Tools.Database;
using Spectre.Console;

namespace Game.Tools.Commands;

public class MigrateCommands
{
    /// <summary>
    /// Run pending database migrations.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string. Falls back to appsettings.json if omitted.</param>
    /// <param name="schema">Target schema (master, user, ranking, all). Omit for all schemas.</param>
    /// <param name="env">Environment: dev/develop/development or prod/release/production.</param>
    /// <param name="force">Skip confirmation prompt for production environment.</param>
    public void Up(string connectionString = "", string schema = "", string env = "", bool force = false)
    {
        var environment = AppConfig.ResolveEnvironment(env);
        if (!ConfirmProductionOperation(environment, force, "run migrations"))
        {
            return;
        }

        var cs = AppConfig.ResolveConnectionString(connectionString, env);
        AnsiConsole.MarkupLine($"[grey]Environment: {environment}[/]");

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
    /// <param name="schema">Target schema (master, user, ranking, all). Omit for all schemas.</param>
    /// <param name="env">Environment: dev/develop/development or prod/release/production.</param>
    /// <param name="force">Skip confirmation prompt for production environment.</param>
    public void Down(string connectionString = "", int steps = 1, string schema = "", string env = "", bool force = false)
    {
        var environment = AppConfig.ResolveEnvironment(env);
        if (!ConfirmProductionOperation(environment, force, "rollback migrations"))
        {
            return;
        }

        var cs = AppConfig.ResolveConnectionString(connectionString, env);
        AnsiConsole.MarkupLine($"[grey]Environment: {environment}[/]");

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
    /// <param name="schema">Target schema (master, user, ranking, all). Omit for all schemas.</param>
    /// <param name="env">Environment: dev/develop/development or prod/release/production.</param>
    public void Status(string connectionString = "", string schema = "", string env = "")
    {
        var environment = AppConfig.ResolveEnvironment(env);
        var cs = AppConfig.ResolveConnectionString(connectionString, env);
        AnsiConsole.MarkupLine($"[grey]Environment: {environment}[/]");

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
    /// <param name="schema">Target schema (master, user, ranking, all). Omit for all schemas.</param>
    /// <param name="env">Environment: dev/develop/development or prod/release/production.</param>
    public void Reset(string connectionString = "", long version = 0, bool seed = false, bool force = false, string schema = "", string env = "")
    {
        var environment = AppConfig.ResolveEnvironment(env);

        if (!force)
        {
            var message = AppConfig.IsProduction(env)
                ? "[red bold]⚠ PRODUCTION DATABASE: This will drop all tables and re-create them. Are you absolutely sure?[/]"
                : "[yellow]This will drop all tables and re-create them. Continue?[/]";

            var confirmed = AnsiConsole.Confirm(message, defaultValue: false);
            if (!confirmed)
            {
                AnsiConsole.MarkupLine("[yellow]Aborted.[/]");
                return;
            }
        }

        var cs = AppConfig.ResolveConnectionString(connectionString, env);
        AnsiConsole.MarkupLine($"[grey]Environment: {environment}[/]");

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

    private static bool ConfirmProductionOperation(string environment, bool force, string operation)
    {
        if (!environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (force)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠ Production mode: --force specified, skipping confirmation.[/]");
            return true;
        }

        var confirmed = AnsiConsole.Confirm(
            $"[red bold]⚠ PRODUCTION DATABASE: You are about to {operation}. Continue?[/]",
            defaultValue: false);

        if (!confirmed)
        {
            AnsiConsole.MarkupLine("[yellow]Aborted.[/]");
        }

        return confirmed;
    }

    private static string[] ResolveSchemas(string schema)
        => MigrationSchema.ResolveSchemas(schema);
}
