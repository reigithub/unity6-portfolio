using System.Security.Cryptography;
using System.Text.Json;
using Game.Shared.Services.RemoteAsset;
using Spectre.Console;

namespace Game.Tools.Commands;

/// <summary>
/// Addressables関連のCLIコマンド
/// </summary>
public class AddressablesCommands
{
    /// <summary>
    /// Generate local bundles manifest from Addressables build output.
    /// </summary>
    /// <param name="buildPath">Addressables build output path (e.g., ServerData/StandaloneWindows64)</param>
    /// <param name="output">Output manifest file path</param>
    /// <param name="version">Version string (default: 0.1.0)</param>
    public void GenerateManifest(string buildPath, string output, string version = "0.1.0")
    {
        if (!Directory.Exists(buildPath))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Build path not found: {buildPath}");
            Environment.ExitCode = 1;
            return;
        }

        AnsiConsole.MarkupLine("[blue]Generating local bundles manifest...[/]");
        AnsiConsole.MarkupLine($"  Build path: {buildPath}");
        AnsiConsole.MarkupLine($"  Output: {output}");

        // catalog.hash を読み込み
        var catalogHash = ReadCatalogHash(buildPath);
        if (string.IsNullOrEmpty(catalogHash))
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] catalog.hash not found, using empty hash");
            catalogHash = "";
        }

        var manifest = new LocalBundlesManifest
        {
            Version = version,
            BuildTime = DateTime.UtcNow.ToString("o"),
            CatalogHash = catalogHash,
            LocalBundles = []
        };

        // バンドルファイルを検索
        var bundleFiles = Directory.GetFiles(buildPath, "*.bundle", SearchOption.AllDirectories);
        AnsiConsole.MarkupLine($"  Found {bundleFiles.Length} bundle files");

        foreach (var bundleFile in bundleFiles)
        {
            var relativePath = Path.GetRelativePath(buildPath, bundleFile);

            // ローカルバンドルのみを含める
            if (!AddressablesBundleUtils.IsLocalBundle(relativePath))
            {
                continue;
            }

            var hash = ComputeFileHash(bundleFile);
            var size = new FileInfo(bundleFile).Length;

            manifest.LocalBundles.Add(new LocalBundleInfo
            {
                Path = relativePath.Replace("\\", "/"),
                Hash = hash,
                Size = size
            });

            AnsiConsole.MarkupLine($"    [green]+[/] {relativePath} ({FormatSize(size)})");
        }

        AnsiConsole.MarkupLine($"  Local bundles: {manifest.LocalBundles.Count}");

        // 出力ディレクトリを作成
        var outputDir = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // JSON として出力
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(output, json);

        AnsiConsole.MarkupLine("[green]Manifest generated successfully![/]");
    }

    /// <summary>
    /// List local bundle patterns used for filtering.
    /// </summary>
    public void ListPatterns()
    {
        AnsiConsole.MarkupLine("[blue]Local bundle patterns:[/]");
        foreach (var pattern in AddressablesBundleUtils.GetLocalBundlePatterns())
        {
            AnsiConsole.MarkupLine($"  - {pattern}");
        }
    }

    private static string ReadCatalogHash(string buildPath)
    {
        // catalog_*.hash ファイルを検索
        var hashFiles = Directory.GetFiles(buildPath, "catalog*.hash", SearchOption.TopDirectoryOnly);
        if (hashFiles.Length == 0)
        {
            return "";
        }

        return File.ReadAllText(hashFiles[0]).Trim();
    }

    private static string ComputeFileHash(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// マニフェスト用のモデルクラス
    /// </summary>
    private class LocalBundlesManifest
    {
        public string Version { get; set; } = "";
        public string BuildTime { get; set; } = "";
        public string CatalogHash { get; set; } = "";
        public List<LocalBundleInfo> LocalBundles { get; set; } = [];
    }

    private class LocalBundleInfo
    {
        public string Path { get; set; } = "";
        public string Hash { get; set; } = "";
        public long Size { get; set; }
    }
}
