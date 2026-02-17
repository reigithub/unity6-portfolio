using Game.Server.Configuration;
using Game.Server.MasterData;
using Game.Server.Services.Interfaces;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Options;

namespace Game.Server.Services;

public class MasterDataService : IMasterDataService
{
    public MemoryDatabase MemoryDatabase { get; }

    public MasterDataService(IOptions<MasterDataSettings> settings, ILogger<MasterDataService> logger)
    {
        string binaryPath = settings.Value.BinaryPath;

        if (!File.Exists(binaryPath))
        {
            throw new FileNotFoundException(
                $"Master data binary not found: {binaryPath}. " +
                "Run 'dotnet run --project src/Game.Tools -- masterdata build --out-server src/Game.Server/MasterData/masterdata.bytes' to generate it.",
                binaryPath);
        }

        byte[] binary = File.ReadAllBytes(binaryPath);
        IFormatterResolver resolver = CompositeResolver.Create(MasterMemoryResolver.Instance, StandardResolver.Instance);
        MemoryDatabase = new MemoryDatabase(binary, formatterResolver: resolver, maxDegreeOfParallelism: Environment.ProcessorCount);

        logger.LogInformation(
            "Master data loaded from {Path} ({Bytes} bytes)",
            binaryPath,
            binary.Length);
    }
}
