using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Core.TradingContext.Application.Services.TraderServices;
using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.TradeAggregate;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.Core.TradingContext.Application.Services.TraderServices;

public class BalanceSavingServiceTest
{
    /// <summary>
    /// Ботам нельзя писать снапшоты в БД — это запрещает перекосы данных между
    /// балансом (которым управляет BotOrchestrator) и историей снимков.
    /// </summary>
    [Fact]
    public async Task SaveBalanceToDatabase_BotTrader_ReturnsFailAndNoSnapshot()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        long botTelegramId = 16001L;
        await HelpMethods.RegisterTrader(db, botTelegramId, "BotTrader", isBot: true);

        var bot = await db.Traders.FirstAsync(t => t.TelegramId == botTelegramId);

        var service = new BalanceSavingService(db, NullLogger<BalanceSavingService>.Instance);
        var result = await service.SaveBalanceToDatabase(
            bot.Id,
            2000m, 1000m, 100m, 100m, 500m,
            DateTime.UtcNow);

        Assert.False(result.IsSuccess);

        var snapshots = await db.BalanceSnapshots
            .Where(s => s.TraderId == bot.Id)
            .ToListAsync();
        Assert.Empty(snapshots);
    }

    /// <summary>
    /// Обычные трейдеры должны сохранять снапшоты без ограничений — иначе отсутствует
    /// история баланса для аудита и мониторинга. Сервис получает Traders.Id, поэтому
    /// проверка бота обязана идти по Id: поиск по TelegramId отсекал бы живых людей.
    /// </summary>
    [Fact]
    public async Task SaveBalanceToDatabase_NormalTrader_ReturnsSuccessAndOneSnapshot()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        long traderTelegramId = 16101L;
        await HelpMethods.RegisterTrader(db, traderTelegramId, "NormalTrader");

        var trader = await db.Traders.FirstAsync(t => t.TelegramId == traderTelegramId);

        var service = new BalanceSavingService(db, NullLogger<BalanceSavingService>.Instance);
        var result = await service.SaveBalanceToDatabase(
            trader.Id,
            2500m, 1500m, 200m, 200m, 500m,
            DateTime.UtcNow);

        Assert.True(result.IsSuccess);

        var snapshots = await db.BalanceSnapshots
            .Where(s => s.TraderId == trader.Id)
            .CountAsync();
        Assert.Equal(1, snapshots);
    }
}
