using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Services.Orchestrators;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.Orchestrators;

public class MiningMachineSlotTakingTokenOrchestratorTest
{
    private static MiningMachineSlotTakingTokenOrchestrator CreateOrchestrator(
        ArkWalletDbContext db,
        Mock<IMiningMachineSlotTakingTokenService> takingTokenService,
        Mock<IPortfolioUpdatingService> portfolioService) =>
        new(
            db,
            takingTokenService.Object,
            portfolioService.Object,
            NullLogger<MiningMachineSlotTakingTokenOrchestrator>.Instance);

    [Fact]
    public async Task TakeTokensFromMachineAsync_WithCollectedTokens_UpdatesPortfolio()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var takingTokenService = new Mock<IMiningMachineSlotTakingTokenService>();
        takingTokenService
            .Setup(x => x.TakeTokensFromMachineAsync(111, 1))
            .ReturnsAsync(Result<MiningTokenCollectionResult>.Ok(new MiningTokenCollectionResult("AAA", 3)));

        var portfolioService = new Mock<IPortfolioUpdatingService>();
        portfolioService
            .Setup(x => x.CreateOrUpdatePortfolioAsync(111, "AAA", 3))
            .ReturnsAsync(Result.Ok());

        var orchestrator = CreateOrchestrator(db, takingTokenService, portfolioService);

        var result = await orchestrator.TakeTokensFromMachineAsync(111, 1);

        Assert.True(result.IsSuccess, result.Message);
        portfolioService.Verify(x => x.CreateOrUpdatePortfolioAsync(111, "AAA", 3), Times.Once);
    }

    [Fact]
    public async Task TakeTokensFromMachineAsync_WithoutTokens_SkipsPortfolio()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var takingTokenService = new Mock<IMiningMachineSlotTakingTokenService>();
        takingTokenService
            .Setup(x => x.TakeTokensFromMachineAsync(111, 1))
            .ReturnsAsync(Result<MiningTokenCollectionResult>.Ok(new MiningTokenCollectionResult(string.Empty, 0)));

        var portfolioService = new Mock<IPortfolioUpdatingService>();

        var orchestrator = CreateOrchestrator(db, takingTokenService, portfolioService);

        var result = await orchestrator.TakeTokensFromMachineAsync(111, 1);

        Assert.True(result.IsSuccess, result.Message);
        portfolioService.Verify(x => x.CreateOrUpdatePortfolioAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task TakeTokensFromMachineAsync_TakingFails_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var takingTokenService = new Mock<IMiningMachineSlotTakingTokenService>();
        takingTokenService
            .Setup(x => x.TakeTokensFromMachineAsync(111, 1))
            .ReturnsAsync(Result<MiningTokenCollectionResult>.Fail("Слота не существует"));

        var portfolioService = new Mock<IPortfolioUpdatingService>();

        var orchestrator = CreateOrchestrator(db, takingTokenService, portfolioService);

        var result = await orchestrator.TakeTokensFromMachineAsync(111, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal("Слота не существует", result.Message);
        portfolioService.Verify(x => x.CreateOrUpdatePortfolioAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task TakeTokensFromMachinesAsync_UpdatesPortfolioForEachCollection()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var takingTokenService = new Mock<IMiningMachineSlotTakingTokenService>();
        takingTokenService
            .Setup(x => x.TakeTokensFromMachinesAsync(111))
            .ReturnsAsync(Result<List<MiningTokenCollectionResult>>.Ok(
                new List<MiningTokenCollectionResult>
                {
                    new("AAA", 3),
                    new("BBB", 2)
                }));

        var portfolioService = new Mock<IPortfolioUpdatingService>();
        portfolioService
            .Setup(x => x.CreateOrUpdatePortfolioAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Result.Ok());

        var orchestrator = CreateOrchestrator(db, takingTokenService, portfolioService);

        var result = await orchestrator.TakeTokensFromMachinesAsync(111);

        Assert.True(result.IsSuccess, result.Message);
        portfolioService.Verify(x => x.CreateOrUpdatePortfolioAsync(111, "AAA", 3), Times.Once);
        portfolioService.Verify(x => x.CreateOrUpdatePortfolioAsync(111, "BBB", 2), Times.Once);
    }

    [Fact]
    public async Task TakeTokensFromMachinesAsync_PortfolioFails_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var takingTokenService = new Mock<IMiningMachineSlotTakingTokenService>();
        takingTokenService
            .Setup(x => x.TakeTokensFromMachinesAsync(111))
            .ReturnsAsync(Result<List<MiningTokenCollectionResult>>.Ok(
                new List<MiningTokenCollectionResult> { new("AAA", 3) }));

        var portfolioService = new Mock<IPortfolioUpdatingService>();
        portfolioService
            .Setup(x => x.CreateOrUpdatePortfolioAsync(111, "AAA", 3))
            .ReturnsAsync(Result.Fail("Не удалось обновить портфель"));

        var orchestrator = CreateOrchestrator(db, takingTokenService, portfolioService);

        var result = await orchestrator.TakeTokensFromMachinesAsync(111);

        Assert.False(result.IsSuccess);
        Assert.Equal("Не удалось обновить портфель", result.Message);
    }
}
