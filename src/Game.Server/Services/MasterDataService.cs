using System.Reflection;
using System.Security.Cryptography;
using Game.Library.Shared.Dto;
using Game.Server.Services.Interfaces;
using MasterMemory;
using MessagePack;

namespace Game.Server.Services;

public class MasterDataService : IMasterDataService
{
    private readonly Dictionary<string, byte[]> _tableCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _etagCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly MasterDataVersionDto _version;

    public MasterDataService(ILogger<MasterDataService> logger)
    {
        var assembly = typeof(Game.Library.Shared.MasterData.MemoryTables.SurvivorPlayerMaster).Assembly;
        var tableTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<MemoryTableAttribute>() != null)
            .ToList();

        var tableHashes = new Dictionary<string, string>();

        foreach (var type in tableTypes)
        {
            string tableName = type.Name;

            try
            {
                var emptyArray = Array.CreateInstance(type, 0);
                byte[] binary = MessagePackSerializer.Serialize(emptyArray.GetType(), emptyArray);

                _tableCache[tableName] = binary;

                string hash = Convert.ToHexString(SHA256.HashData(binary)).ToLowerInvariant();
                _etagCache[tableName] = $"\"{hash}\"";
                tableHashes[tableName] = hash;

                logger.LogInformation("Loaded master data table: {TableName} ({Bytes} bytes)", tableName, binary.Length);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to serialize master data table: {TableName}", tableName);
            }
        }

        string allHashesConcat = string.Join(",", tableHashes.OrderBy(kv => kv.Key).Select(kv => kv.Value));
        string versionHash = Convert.ToHexString(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(allHashesConcat))).ToLowerInvariant()[..8];

        _version = new MasterDataVersionDto
        {
            Version = $"1.0.0-{versionHash}",
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            TableHashes = tableHashes,
        };

        logger.LogInformation(
            "Master data initialized: {TableCount} tables, version {Version}",
            tableHashes.Count,
            _version.Version);
    }

    public MasterDataVersionDto GetCurrentVersion() => _version;

    public string? GetTableEtag(string tableName)
    {
        return _etagCache.TryGetValue(tableName, out string? etag) ? etag : null;
    }

    public byte[]? GetTableBinary(string tableName)
    {
        return _tableCache.TryGetValue(tableName, out byte[]? data) ? data : null;
    }
}
