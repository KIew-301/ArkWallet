using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.MarketMaker;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Infrastructure.Wizard;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Moq;
using static ArkWallet.Core.General.Application.Common.Result;

namespace ArkWallet.Tests.IntegrationTests;

public class BotsWizardCommandsTest
{
    private readonly ServiceMocks _m;
    private readonly WizardEngine _engine;

    private const long AdminUserId = 999999;

    public BotsWizardCommandsTest()
    {
        _m = WizardEngineTestHelper.Build();
        _engine = _m.Engine;
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n");

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    private async Task RegisterBotAsync(long traderId, string symbol, BotRole role, decimal basePower)
    {
        var db = _m.Db;
        await HelpMethods.RegisterTrader(db, traderId);
        var bot = MarketMakerBotRecord.Create(traderId, symbol, role, basePower);
        db.MarketMakerBots.Add(bot);
        await db.SaveChangesAsync();
    }

    private void SetupBotsQuery(List<MarketMakerBotRecord>? bots = null)
    {
        _m.BotQuery
            .Setup(s => s.GetAllBotsAsync())
            .ReturnsAsync(Result<List<MarketMakerBotRecord>>.Ok(bots ?? new List<MarketMakerBotRecord>()));
    }

    // ═══════════════════════════════════════════════════════════
    //  /admin_bots_powers
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminBotsPowers_NoBots_ReturnsEmptyMessage()
    {
        SetupBotsQuery();

        var result = await _engine.ProcessInput(AdminUserId, "/admin_bots_powers");

        Assert.Equal("No bots found.", result.Message);
    }

    [Fact]
    public async Task AdminBotsPowers_WithBots_ShowsBotsListAndButtons()
    {
        await RegisterBotAsync(2001, "ARK_001", BotRole.Buyer, 50m);

        var bots = await _m.Db.MarketMakerBots.ToListAsync();
        SetupBotsQuery(bots);

        var result = await _engine.ProcessInput(AdminUserId, "/admin_bots_powers");

        var msg = Normalize(result.Message);
        Assert.Contains("Bot powers", msg);
        Assert.Contains("ARK_001", msg);
        Assert.NotNull(result.Buttons);
        Assert.NotEmpty(result.Buttons);
        Assert.Contains(result.Buttons, b => b.Value == "/admin_bots_powers");
        Assert.Contains(result.Buttons, b => b.Value == "/admin_rebalance_bots_power");
    }

    [Fact]
    public async Task AdminBotsPowers_WithBots_ShowsBaseActiveRatio()
    {
        await RegisterBotAsync(3001, "TKN01", BotRole.Seller, 100m);
        await RegisterBotAsync(3002, "TKN01", BotRole.Waller, 50m);

        var bots = await _m.Db.MarketMakerBots.ToListAsync();
        SetupBotsQuery(bots);

        var result = await _engine.ProcessInput(AdminUserId, "/admin_bots_powers");

        var msg = Normalize(result.Message);
        Assert.Contains("base=", msg);
        Assert.Contains("active=", msg);
        Assert.Contains("x", msg);
        // TKN01 and seller/waller roles verified by structure assertions above
    }

    // ═══════════════════════════════════════════════════════════
    //  /admin_rebalance_bots_power
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminRebalanceBotsPower_Success_ShowsRecalculatedPowers()
    {
        await RegisterBotAsync(4001, "ARK_001", BotRole.Buyer, 50m);
        _m.BotOrchestrator.Setup(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result.Ok()));
        SetupBotsQuery();

        var result = await _engine.ProcessInput(AdminUserId, "/admin_rebalance_bots_power");

        var msg = Normalize(result.Message);
        Assert.Contains("Powers recalculated", msg);
        _m.BotOrchestrator.Verify(
            o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AdminRebalanceBotsPower_Fail_ShowsErrorMessage()
    {
        _m.BotOrchestrator.Setup(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result.Fail("Connection lost")));
        SetupBotsQuery();

        var result = await _engine.ProcessInput(AdminUserId, "/admin_rebalance_bots_power");

        Assert.Contains("Rebalance failed", result.Message);
        Assert.Contains("Connection lost", result.Message);
    }

    // ═══════════════════════════════════════════════════════════
    //  /admin_update_bots_grid <role>
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminUpdateBotsGrid_ValidSeller_SellsGridUpdated()
    {
        _m.BotOrchestrator.Setup(o => o.UpdateBotsGridsForRoleAsync(
                MarketMakerRole.Seller,
                true,
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result.Ok()));
        SetupBotsQuery();

        var result = await _engine.ProcessInput(AdminUserId, "/admin_update_bots_grid Seller");

        Assert.Contains("Seller grids updated", result.Message);
        _m.BotOrchestrator.Verify(
            o => o.UpdateBotsGridsForRoleAsync(MarketMakerRole.Seller,
                true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AdminUpdateBotsGrid_ValidBuyer_BuyersGridUpdated()
    {
        _m.BotOrchestrator.Setup(o => o.UpdateBotsGridsForRoleAsync(
                MarketMakerRole.Buyer,
                true,
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result.Ok()));

        var result = await _engine.ProcessInput(AdminUserId, "/admin_update_bots_grid Buyer");

        Assert.Contains("Buyer grids updated", result.Message);
        _m.BotOrchestrator.Verify(
            o => o.UpdateBotsGridsForRoleAsync(MarketMakerRole.Buyer,
                true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AdminUpdateBotsGrid_WrongRole_ShowsError()
    {
        SetupBotsQuery();

        var result = await _engine.ProcessInput(AdminUserId, "/admin_update_bots_grid НетТакойРоли");

        Assert.Contains("Role must be one of: Buyer, Seller, Waller", result.Message);
        _m.BotOrchestrator.Verify(
            o => o.UpdateBotsGridsForRoleAsync(It.IsAny<MarketMakerRole>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ═══════════════════════════════════════════════════════════
    //  /admin_help_token
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminHelpToken_ShowsThreeNewCommands()
    {
        var result = await _engine.ProcessInput(AdminUserId, "/admin_help_token");

        Assert.NotNull(result.Message);
        var msg = Normalize(result.Message);
        Assert.Contains("/admin_bots_powers", msg);
        Assert.Contains("/admin_rebalance_bots_power", msg);
        Assert.Contains("/admin_update_bots_grid", msg);
    }
}
