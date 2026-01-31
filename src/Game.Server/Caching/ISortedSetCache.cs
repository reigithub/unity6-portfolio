namespace Game.Server.Caching;

public interface ISortedSetCache
{
    Task AddAsync(string key, string member, double score, CancellationToken ct = default);

    Task<List<SortedSetEntry>> GetTopAsync(string key, int count, int offset = 0, CancellationToken ct = default);

    Task<long?> GetRankDescendingAsync(string key, string member, CancellationToken ct = default);

    Task RemoveAsync(string key, string member, CancellationToken ct = default);

    Task RemoveKeyAsync(string key, CancellationToken ct = default);
}

public record SortedSetEntry(string Member, double Score);
