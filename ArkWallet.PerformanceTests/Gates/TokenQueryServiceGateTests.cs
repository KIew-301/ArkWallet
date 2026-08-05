using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.PerformanceTests.Gates;

[Collection("Perf")]
public class TokenQueryServiceGateTests
{
    private const int TokenCount = 50;

    [Fact]
    public async Task GetAllActiveTokensAsync_With50Tokens_StaysWithinQueryBudget()
    {
        var counter = new QueryCounter();
        using var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();
        await GatesSeed.SeedTokenCatalogAsync(db, TokenCount);

        var priceChangeService = new TokenPriceChangeCalculationService(
            db, NullLogger<TokenPriceChangeCalculationService>.Instance, TimeProvider.System);
        var service = new TokenQueryService(db, priceChangeService, NullLogger<TokenQueryService>.Instance);

        await PerfWarmup.RunAsync(async () => await service.GetAllActiveTokensAsync());
        counter.Reset();

        using var scope = new PerfScope(counter);
        using (scope.Step("GetAllActiveTokensAsync"))
        {
            var result = await service.GetAllActiveTokensAsync();
            Assert.True(result.IsSuccess, result.Message);
            Assert.True(result.TryGetData(out var data));
            Assert.Equal(TokenCount, data.Count);
        }

        GateAssert.QueryBudget("token-query-50t", GateBudgets.TokenQuery50T, counter, scope);
    }
}
