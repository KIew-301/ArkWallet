using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using Microsoft.Extensions.Caching.Memory;

namespace ArkWallet.PerformanceTests.E2e;

internal sealed class CachingTokenQueryService(
    ITokenQueryService inner,
    IMemoryCache cache) : ITokenQueryService
{
    internal const string AllTokensKey = "e2e-cache:all-tokens";

    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public Task<Result<List<TokenInfoWithPriceChange>>> GetAllActiveTokensAsync()
        => cache.GetOrCreateAsync(AllTokensKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return inner.GetAllActiveTokensAsync();
        })!;

    public Task<Result<TokenInfo>> GetTokenInfoAsync(string symbol)
        => cache.GetOrCreateAsync($"e2e-cache:token:{symbol}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return inner.GetTokenInfoAsync(symbol);
        })!;

    internal static void Clear(IMemoryCache cache) => cache.Remove(AllTokensKey);
}
