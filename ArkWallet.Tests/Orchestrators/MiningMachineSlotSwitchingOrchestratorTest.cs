using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Services.Orchestrators;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.Orchestrators;

public class MiningMachineSlotSwitchingOrchestratorTest
{
    private static MiningMachineSlotSwitchingOrchestrator CreateOrchestrator(
        ArkWalletDbContext db,
        Mock<IMiningMachineSlotSwitchingService> switchingService,
        Mock<IPortfolioUpdatingService> portfolioService) =>
        new(
            db,
            switchingService.Object,
            portfolioService.Object,
            NullLogger<MiningMachineSlotSwitchingOrchestrator>.Instance);

    [Fact]
    public async Task SwitchTargetTokenAsync_WithCollectedTokens_UpdatesPortfolio()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var switchingService = new Mock<IMiningMachineSlotSwitchingService>();
        switchingService
            .Setup(x => x.SwitchTargetTokenAsync(111, 1, "AAA"))
            .ReturnsAsync(Result<MiningTokenCollectionResult>.Ok(new MiningTokenCollectionResult("AAA", 5)));

        var portfolioService = new Mock<IPortfolioUpdatingService>();
        portfolioService
            .Setup(x => x.CreateOrUpdatePortfolioAsync(111, "AAA", 5))
            .ReturnsAsync(Result.Ok());

        var orchestrator = CreateOrchestrator(db, switchingService, portfolioService);

        var result = await orchestrator.SwitchTargetTokenAsync(111, 1, "AAA");

        Assert.True(result.IsSuccess, result.Message);
        portfolioService.Verify(x => x.CreateOrUpdatePortfolioAsync(111, "AAA", 5), Times.Once);
    }

    [Fact]
    public async Task SwitchTargetTokenAsync_WithoutCollectedTokens_SkipsPortfolio()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var switchingService = new Mock<IMiningMachineSlotSwitchingService>();
        switchingService
            .Setup(x => x.SwitchTargetTokenAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(Result<MiningTokenCollectionResult>.Ok(new MiningTokenCollectionResult(string.Empty, 0)));

        var portfolioService = new Mock<IPortfolioUpdatingService>();

        var orchestrator = CreateOrchestrator(db, switchingService, portfolioService);

        var result = await orchestrator.SwitchTargetTokenAsync(111, 1, "AAA");

        Assert.True(result.IsSuccess, result.Message);
        portfolioService.Verify(x => x.CreateOrUpdatePortfolioAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SwitchTargetTokenAsync_SwitchingFails_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var switchingService = new Mock<IMiningMachineSlotSwitchingService>();
        switchingService
            .Setup(x => x.SwitchTargetTokenAsync(111, 1, "AAA"))
            .ReturnsAsync(Result<MiningTokenCollectionResult>.Fail("Слота не существует"));

        var portfolioService = new Mock<IPortfolioUpdatingService>();

        var orchestrator = CreateOrchestrator(db, switchingService, portfolioService);

        var result = await orchestrator.SwitchTargetTokenAsync(111, 1, "AAA");

        Assert.False(result.IsSuccess);
        Assert.Equal("Слота не существует", result.Message);
        portfolioService.Verify(x => x.CreateOrUpdatePortfolioAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SwitchTargetTokenAsync_PortfolioFails_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var switchingService = new Mock<IMiningMachineSlotSwitchingService>();
        switchingService
            .Setup(x => x.SwitchTargetTokenAsync(111, 1, "AAA"))
            .ReturnsAsync(Result<MiningTokenCollectionResult>.Ok(new MiningTokenCollectionResult("AAA", 5)));

        var portfolioService = new Mock<IPortfolioUpdatingService>();
        portfolioService
            .Setup(x => x.CreateOrUpdatePortfolioAsync(111, "AAA", 5))
            .ReturnsAsync(Result.Fail("Не удалось обновить портфель"));

        var orchestrator = CreateOrchestrator(db, switchingService, portfolioService);

        var result = await orchestrator.SwitchTargetTokenAsync(111, 1, "AAA");

        Assert.False(result.IsSuccess);
        Assert.Equal("Не удалось обновить портфель", result.Message);
    }
}
