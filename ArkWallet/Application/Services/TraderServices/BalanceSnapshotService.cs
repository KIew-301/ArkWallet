using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TraderServices;

internal class BalanceSnapshotService(ArkWalletDbContext db, ILogger<BalanceSnapshotService> logger) : IBalanceSnapshotService
{
    public async Task<BalanceSnapshotResult> TakeTotalTraderBalanceSnapshot(long traderTelegramId)
    {
        try
        {
            var trader = await db.Traders
                .Include(t => t.Portfolio)
                .Include(t => t.Orders)
                .FirstOrDefaultAsync(t => t.TelegramId == traderTelegramId);

            if (trader == null)
                return BalanceSnapshotResult.Fail("Трейдер на найден", traderTelegramId);

            var mainBalance = trader.Balance;
            var longOrderReserve = 0m;
            var shortOrderReserve = 0m;
            var balanceInTokens = 0m;

            var longOrders = trader.Orders.Where(o => o.IsLong());
            var shortOrders = trader.Orders.Where(o => o.IsShort());
            var portfolioItems = trader.Portfolio;
            var activeSymbols = shortOrders
                .Select(o => o.CharacterTokenId)
                .Union(portfolioItems.Select(p => p.CharacterTokenId))
                .ToArray();

            var tokenPrices = new Dictionary<string, decimal>();

            if (activeSymbols.Length > 0)
                tokenPrices = await db.CharacterTokens
                    .Where(t => activeSymbols.Contains(t.Symbol))
                    .ToDictionaryAsync(t => t.Symbol, t => t.CurrentPrice);

            foreach (var order in longOrders)
                longOrderReserve += order.GetReservedBalance();

            foreach (var order in shortOrders)
                if (tokenPrices.TryGetValue(order.CharacterTokenId, out var price))
                    shortOrderReserve += order.GetRemainingQuantity() * price;

            foreach (var item in portfolioItems)
                if (tokenPrices.TryGetValue(item.CharacterTokenId, out var price))
                    balanceInTokens += item.Quantity * price;

            var totalBalance = mainBalance + longOrderReserve + shortOrderReserve + balanceInTokens;

            return BalanceSnapshotResult.Ok(traderTelegramId, totalBalance, mainBalance, longOrderReserve, shortOrderReserve, balanceInTokens);
        }
        catch (DomainException ex)
        {
            return BalanceSnapshotResult.Fail($"Ошибка бизнес-логики: {ex.Message}", traderTelegramId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Ошибка создания снапшота для трейдера {traderTelegramId}");
            return BalanceSnapshotResult.Fail("Внутренняя ошибка сервера", traderTelegramId);
        }
    }
}

public record BalanceSnapshotResult(
    bool IsSuccess, string message, long traderTelegramId, decimal totalBalance,
    decimal mainBalance, decimal longOrderReserve, decimal shortOrderReserve,
    decimal balanceInTokens, DateTime dateTimeSnapshot)
{
    public static BalanceSnapshotResult Ok(long traderTelegramId, decimal totalBalance, decimal mainBalance, decimal longOrderReserve, decimal shortOrderReserve, decimal balanceInTokens)
    {
        return new(true, "Снимок сделан успешно", traderTelegramId, totalBalance, mainBalance, longOrderReserve, shortOrderReserve, balanceInTokens, DateTime.UtcNow);
    }

    public static BalanceSnapshotResult Fail(string message, long traderTelegramId)
    {
        return new(false, message, traderTelegramId, 0, 0, 0, 0, 0, DateTime.UtcNow);
    }
};