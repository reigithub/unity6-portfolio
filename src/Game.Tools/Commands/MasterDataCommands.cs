using System.Security.Cryptography;
using Game.Tools.CodeGen;
using Game.Tools.Data;
using Game.Tools.Proto;
using MessagePack;
using MessagePack.Resolvers;
using Spectre.Console;

namespace Game.Tools.Commands;

public class MasterDataCommands
{
    /// <summary>
    /// Generate C# MemoryTable classes from .proto schema files.
    /// </summary>
    public void Codegen(string protoDir = "masterdata/proto/", string? outClient = null, string? outServer = null, string? outRealtime = null, bool verify = false)
    {
        var reader = new ProtoSchemaReader();
        var generator = new CSharpClassGenerator();

        AnsiConsole.MarkupLine($"[blue]Reading proto schemas from:[/] {Path.GetFullPath(protoDir)}");
        var tables = reader.ReadAll(protoDir);
        AnsiConsole.MarkupLine($"[green]Found {tables.Count} table definitions[/]");

        var targets = new List<(string Label, string? OutDir, int Bit, string Namespace)>();
        if (outClient != null)
        {
            targets.Add(("Client", outClient, DeployTargetHelper.Client, "Game.Client.MasterData"));
        }

        if (outServer != null)
        {
            targets.Add(("Server", outServer, DeployTargetHelper.Server, "Game.Server.MasterData"));
        }

        if (outRealtime != null)
        {
            targets.Add(("Realtime", outRealtime, DeployTargetHelper.Realtime, "Game.RealTime.MasterData"));
        }

        // Default: generate client if no target specified
        if (targets.Count == 0 && !verify)
        {
            AnsiConsole.MarkupLine("[yellow]No output target specified. Use --out-client, --out-server, or --out-realtime.[/]");
            return;
        }

        if (verify)
        {
            RunVerify(tables, generator, protoDir);
            return;
        }

        foreach (var (label, outDir, bit, ns) in targets)
        {
            AnsiConsole.MarkupLine($"\n[blue]Generating for target:[/] {label} → {outDir}");
            Directory.CreateDirectory(outDir!);

            int count = 0;
            foreach (var table in tables)
            {
                if (!DeployTargetHelper.ShouldInclude(table.DeployTarget, bit))
                {
                    continue;
                }

                var source = generator.Generate(table, ns, label, bit);
                var filePath = Path.Combine(outDir!, $"{table.TableName}.cs");
                File.WriteAllText(filePath, source);
                count++;
            }

            AnsiConsole.MarkupLine($"[green]Generated {count} classes → {outDir}[/]");
        }
    }

    private static void RunVerify(List<TableDefinition> tables, CSharpClassGenerator generator, string protoDir)
    {
        var existingDir = Path.GetFullPath("src/Game.Shared/Runtime/Shared/MasterData/MemoryTables/");
        AnsiConsole.MarkupLine($"[blue]Verify mode: comparing with[/] {existingDir}");

        if (!Directory.Exists(existingDir))
        {
            AnsiConsole.MarkupLine("[red]Existing directory not found![/]");
            return;
        }

        int matched = 0;
        int diffCount = 0;
        int missing = 0;

        foreach (var table in tables)
        {
            // Generate with the original namespace for comparison
            var source = generator.Generate(table, "Game.Library.Shared.MasterData.MemoryTables", "Shared", 0);

            var existingFile = Path.Combine(existingDir, $"{table.TableName}.cs");
            if (!File.Exists(existingFile))
            {
                AnsiConsole.MarkupLine($"  [yellow]MISSING:[/] {table.TableName}.cs not found in existing directory");
                missing++;
                continue;
            }

            var existingContent = File.ReadAllText(existingFile);
            var normalizedGenerated = NormalizeForComparison(source);
            var normalizedExisting = NormalizeForComparison(existingContent);

            if (normalizedGenerated == normalizedExisting)
            {
                AnsiConsole.MarkupLine($"  [green]MATCH:[/] {table.TableName}");
                matched++;
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]DIFF:[/] {table.TableName}");
                ShowDiff(table.TableName, normalizedExisting, normalizedGenerated);
                diffCount++;
            }
        }

        AnsiConsole.MarkupLine($"\n[blue]Results:[/] {matched} matched, {diffCount} different, {missing} missing");

        if (diffCount > 0 || missing > 0)
        {
            Environment.ExitCode = 1;
        }
    }

    private static string NormalizeForComparison(string content)
    {
        // Remove auto-generated comments, XML doc comments, #region/#endregion,
        // and normalize whitespace
        var lines = content
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .Where(l => !l.StartsWith("//", StringComparison.Ordinal))
            .Where(l => !l.StartsWith("///", StringComparison.Ordinal))
            .Where(l => !l.StartsWith("#region", StringComparison.Ordinal))
            .Where(l => !l.StartsWith("#endregion", StringComparison.Ordinal));

        return string.Join("\n", lines);
    }

    private static void ShowDiff(string tableName, string existing, string generated)
    {
        var existingLines = existing.Split('\n');
        var generatedLines = generated.Split('\n');

        int maxLines = Math.Max(existingLines.Length, generatedLines.Length);
        for (int i = 0; i < maxLines; i++)
        {
            string? existLine = i < existingLines.Length ? existingLines[i] : null;
            string? genLine = i < generatedLines.Length ? generatedLines[i] : null;

            if (existLine != genLine)
            {
                if (existLine != null)
                {
                    AnsiConsole.MarkupLine($"    [red]- {Markup.Escape(existLine)}[/]");
                }

                if (genLine != null)
                {
                    AnsiConsole.MarkupLine($"    [green]+ {Markup.Escape(genLine)}[/]");
                }
            }
        }
    }

    /// <summary>
    /// Scaffold a new .proto file from an existing C# MemoryTable class.
    /// </summary>
    public void Scaffold(string className, string outDir = "masterdata/proto/", string target = "server")
    {
        if (target != "server" && target != "client")
        {
            AnsiConsole.MarkupLine($"[red]Unsupported target:[/] {target}. Use 'server' or 'client'.");
            Environment.ExitCode = 1;
            return;
        }

        var metaDb = target == "client"
            ? Game.Client.MasterData.MemoryDatabase.GetMetaDatabase()
            : Game.Server.MasterData.MemoryDatabase.GetMetaDatabase();
        var tableInfos = metaDb.GetTableInfos().ToArray();

        var tableInfo = tableInfos.FirstOrDefault(t =>
            t.DataType.Name.Equals(className, StringComparison.OrdinalIgnoreCase));

        if (tableInfo == null)
        {
            AnsiConsole.MarkupLine($"[red]Class not found:[/] {className}");
            AnsiConsole.MarkupLine("[blue]Available MemoryTable classes:[/]");
            foreach (var t in tableInfos.OrderBy(t => t.DataType.Name))
            {
                AnsiConsole.MarkupLine($"  {t.DataType.Name}");
            }

            Environment.ExitCode = 1;
            return;
        }

        var type = tableInfo.DataType;
        var absoluteOutDir = Path.GetFullPath(outDir);
        var subDir = ProtoFileGenerator.EstimateSubDirectory(type.Name, absoluteOutDir);
        var generator = new ProtoFileGenerator();
        var protoText = generator.Generate(type, subDir);

        var snakeName = CodeGen.NameConverter.ToSnakeCase(type.Name);
        var outputPath = Path.Combine(absoluteOutDir, subDir, $"{snakeName}.proto");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, protoText);

        AnsiConsole.MarkupLine($"[green]Generated:[/] {outputPath}");
    }

    /// <summary>
    /// Build MessagePack binary from TSV files using proto schema definitions.
    /// </summary>
    public void Build(string tsvDir = "masterdata/raw/", string protoDir = "masterdata/proto/", string? outClient = null, string? outServer = null, string? outRealtime = null)
    {
        var reader = new ProtoSchemaReader();
        AnsiConsole.MarkupLine($"[blue]Reading proto schemas from:[/] {Path.GetFullPath(protoDir)}");
        var tables = reader.ReadAll(protoDir);
        AnsiConsole.MarkupLine($"[green]Found {tables.Count} table definitions[/]");

        var absoluteTsvDir = Path.GetFullPath(tsvDir);
        AnsiConsole.MarkupLine($"[blue]TSV directory:[/] {absoluteTsvDir}");

        // Discover generated MemoryTable types via MetaDatabase
        var serverTableTypeMap = Game.Server.MasterData.MemoryDatabase.GetMetaDatabase()
            .GetTableInfos()
            .ToDictionary(t => t.DataType.Name, t => t.DataType);
        var clientTableTypeMap = Game.Client.MasterData.MemoryDatabase.GetMetaDatabase()
            .GetTableInfos()
            .ToDictionary(t => t.DataType.Name, t => t.DataType);

        var targets = new List<(string Label, string? OutPath, int Bit)>();
        if (outClient != null)
        {
            targets.Add(("Client", outClient, DeployTargetHelper.Client));
        }

        if (outServer != null)
        {
            targets.Add(("Server", outServer, DeployTargetHelper.Server));
        }

        if (outRealtime != null)
        {
            targets.Add(("Realtime", outRealtime, DeployTargetHelper.Realtime));
        }

        if (targets.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No output target specified. Use --out-client, --out-server, or --out-realtime.[/]");
            return;
        }

        // Initialize MessagePack resolvers
        var serverResolver = CompositeResolver.Create(
            Game.Server.MasterData.MasterMemoryResolver.Instance,
            StandardResolver.Instance);
        var clientResolver = CompositeResolver.Create(
            Game.Client.MasterData.MasterMemoryResolver.Instance,
            StandardResolver.Instance);

        foreach (var (label, outPath, bit) in targets)
        {
            AnsiConsole.MarkupLine($"\n[blue]Building binary for:[/] {label} → {outPath}");

            var isClient = bit == DeployTargetHelper.Client;
            var tableTypeMap = isClient ? clientTableTypeMap : serverTableTypeMap;
            var formatterResolver = isClient ? clientResolver : serverResolver;

            object databaseBuilder = isClient
                ? new Game.Client.MasterData.DatabaseBuilder(formatterResolver)
                : new Game.Server.MasterData.DatabaseBuilder(formatterResolver);
            var appendMethods = databaseBuilder.GetType()
                .GetMethods()
                .Where(m => m.Name == "Append" && m.GetParameters().Length == 1)
                .ToDictionary(m => m.GetParameters()[0].ParameterType.GetGenericArguments()[0]);

            int tableCount = 0;
            int rowCount = 0;

            foreach (var table in tables)
            {
                if (!DeployTargetHelper.ShouldInclude(table.DeployTarget, bit))
                {
                    continue;
                }

                if (!tableTypeMap.TryGetValue(table.TableName, out var type))
                {
                    AnsiConsole.MarkupLine($"  [yellow]SKIP:[/] {table.TableName} - type not found in assembly");
                    continue;
                }

                var tsvPath = Path.Combine(absoluteTsvDir, $"{table.TableName}.tsv");
                if (!File.Exists(tsvPath))
                {
                    AnsiConsole.MarkupLine($"  [yellow]SKIP:[/] {table.TableName} - TSV not found at {tsvPath}");
                    continue;
                }

                var elements = TsvReader.ReadTsv(type, tsvPath);
                var typedArray = Array.CreateInstance(type, elements.Length);
                Array.Copy(elements, typedArray, elements.Length);

                if (appendMethods.TryGetValue(type, out var appendMethod))
                {
                    appendMethod.Invoke(databaseBuilder, [typedArray]);
                    tableCount++;
                    rowCount += elements.Length;
                    AnsiConsole.MarkupLine($"  [green]OK:[/] {table.TableName} ({elements.Length} rows)");
                }
                else
                {
                    AnsiConsole.MarkupLine($"  [yellow]SKIP:[/] {table.TableName} - no Append method found");
                }
            }

            var binary = (byte[])databaseBuilder.GetType().GetMethod("Build")!.Invoke(databaseBuilder, null)!;

            var outDir = Path.GetDirectoryName(outPath!);
            if (outDir != null)
            {
                Directory.CreateDirectory(outDir);
            }

            File.WriteAllBytes(outPath!, binary);

            var hash = Convert.ToHexString(SHA256.HashData(binary)).ToLowerInvariant()[..16];
            AnsiConsole.MarkupLine($"[green]Built {tableCount} tables ({rowCount} total rows) → {outPath} ({binary.Length} bytes, sha256: {hash}...)[/]");
        }
    }

    /// <summary>
    /// Validate TSV data against proto schema definitions.
    /// </summary>
    public void Validate(string tsvDir = "masterdata/raw/", string protoDir = "masterdata/proto/")
    {
        var reader = new ProtoSchemaReader();
        var tables = reader.ReadAll(protoDir);
        var absoluteTsvDir = Path.GetFullPath(tsvDir);

        AnsiConsole.MarkupLine($"[blue]Validating TSV files in:[/] {absoluteTsvDir}");
        AnsiConsole.MarkupLine($"[blue]Against {tables.Count} proto definitions[/]");

        var tableTypeMap = Game.Server.MasterData.MemoryDatabase.GetMetaDatabase()
            .GetTableInfos()
            .ToDictionary(t => t.DataType.Name, t => t.DataType);

        int valid = 0;
        int errors = 0;
        int missing = 0;

        foreach (var table in tables)
        {
            if (!tableTypeMap.TryGetValue(table.TableName, out var type))
            {
                AnsiConsole.MarkupLine($"  [yellow]SKIP:[/] {table.TableName} - type not found");
                continue;
            }

            var tsvPath = Path.Combine(absoluteTsvDir, $"{table.TableName}.tsv");
            if (!File.Exists(tsvPath))
            {
                AnsiConsole.MarkupLine($"  [yellow]MISSING:[/] {table.TableName}.tsv");
                missing++;
                continue;
            }

            try
            {
                var elements = TsvReader.ReadTsv(type, tsvPath);
                AnsiConsole.MarkupLine($"  [green]VALID:[/] {table.TableName} ({elements.Length} rows)");
                valid++;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"  [red]ERROR:[/] {table.TableName} - {ex.Message}");
                errors++;
            }
        }

        AnsiConsole.MarkupLine($"\n[blue]Results:[/] {valid} valid, {errors} errors, {missing} missing");

        if (errors > 0)
        {
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// Show diff between two MasterData binary files.
    /// </summary>
    public void Diff(string oldPath, string newPath)
    {
        if (!File.Exists(oldPath))
        {
            AnsiConsole.MarkupLine($"[red]File not found:[/] {oldPath}");
            Environment.ExitCode = 1;
            return;
        }

        if (!File.Exists(newPath))
        {
            AnsiConsole.MarkupLine($"[red]File not found:[/] {newPath}");
            Environment.ExitCode = 1;
            return;
        }

        var oldBytes = File.ReadAllBytes(oldPath);
        var newBytes = File.ReadAllBytes(newPath);

        var oldHash = Convert.ToHexString(SHA256.HashData(oldBytes)).ToLowerInvariant();
        var newHash = Convert.ToHexString(SHA256.HashData(newBytes)).ToLowerInvariant();

        AnsiConsole.MarkupLine($"[blue]Old:[/] {oldPath} ({oldBytes.Length} bytes, sha256: {oldHash[..16]}...)");
        AnsiConsole.MarkupLine($"[blue]New:[/] {newPath} ({newBytes.Length} bytes, sha256: {newHash[..16]}...)");

        if (oldHash == newHash)
        {
            AnsiConsole.MarkupLine("[green]Files are identical.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Files differ.[/] Size: {oldBytes.Length} → {newBytes.Length} ({newBytes.Length - oldBytes.Length:+#;-#;0} bytes)");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// Export MasterData to various formats (json, tsv).
    /// </summary>
    public void Export(string format, string inputPath, string outDir, string target = "server")
    {
        if (format != "json" && format != "tsv")
        {
            AnsiConsole.MarkupLine($"[red]Unsupported format:[/] {format}. Use 'json' or 'tsv'.");
            Environment.ExitCode = 1;
            return;
        }

        if (!File.Exists(inputPath))
        {
            AnsiConsole.MarkupLine($"[red]File not found:[/] {inputPath}");
            Environment.ExitCode = 1;
            return;
        }

        if (target != "server" && target != "client")
        {
            AnsiConsole.MarkupLine($"[red]Unsupported target:[/] {target}. Use 'server' or 'client'.");
            Environment.ExitCode = 1;
            return;
        }

        AnsiConsole.MarkupLine($"[blue]Reading binary:[/] {Path.GetFullPath(inputPath)}");
        AnsiConsole.MarkupLine($"[blue]Target:[/] {target}");
        var binary = File.ReadAllBytes(inputPath);

        object db;
        if (target == "client")
        {
            var resolver = CompositeResolver.Create(
                Game.Client.MasterData.MasterMemoryResolver.Instance,
                StandardResolver.Instance);
            db = new Game.Client.MasterData.MemoryDatabase(binary, formatterResolver: resolver, maxDegreeOfParallelism: Environment.ProcessorCount);
        }
        else
        {
            var resolver = CompositeResolver.Create(
                Game.Server.MasterData.MasterMemoryResolver.Instance,
                StandardResolver.Instance);
            db = new Game.Server.MasterData.MemoryDatabase(binary, formatterResolver: resolver, maxDegreeOfParallelism: Environment.ProcessorCount);
        }

        AnsiConsole.MarkupLine($"[blue]Exporting as {format} to:[/] {Path.GetFullPath(outDir)}");
        MasterDataExporter.Export(db, format, outDir);
    }
}
