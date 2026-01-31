using Game.Library.Shared.Dto;

namespace Game.Server.Services.Interfaces;

public interface IMasterDataService
{
    MasterDataVersionDto GetCurrentVersion();

    string? GetTableEtag(string tableName);

    byte[]? GetTableBinary(string tableName);
}
