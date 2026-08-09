using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using Microsoft.Extensions.Caching.Memory;

namespace ArkWallet.PerformanceTests.E2e;

internal static class CacheCounters
{
    public static long Hits;
    public static long Misses;

    public static void Reset()
    {
        Hits = 0;
        Misses = 0;
    }
}

internal sealed class CachingTokenQueryService(
    ITokenQueryService inner,
    IMemoryCache cache) : ITokenQueryService
{
    internal const string AllTokensKey = "e2e-cache:all-tokens";

    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public Task<Result<List<TokenInfoWithPriceChange>>> GetAllActiveTokensAsync()
        => GetOrAddAsync(AllTokensKey, inner.GetAllActiveTokensAsync);

    public Task<Result<TokenInfo>> GetTokenInfoAsync(string symbol)
        => GetOrAddAsync($"e2e-cache:token:{symbol}", () => inner.GetTokenInfoAsync(symbol));

    internal static void Clear(IMemoryCache cache) => cache.Remove(AllTokensKey);

    private async Task<Result<T>> GetOrAddAsync<T>(string key, Func<Task<Result<T>>> factory)
    {
        if (cache.TryGetValue(key, out object? value) && value is Result<T> cached)
        {
            CacheCounters.Hits++;
            return cached;
        }

        CacheCounters.Misses++;
        var result = await factory();
        cache.Set(key, result, Ttl);
        return result;
    }
}
