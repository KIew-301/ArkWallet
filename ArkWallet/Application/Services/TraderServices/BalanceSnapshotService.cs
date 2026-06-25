using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TraderServices;
using static ArkWallet.Application.Common.Result<BalanceSnapshotData>;

internal class BalanceSnapshotService(ArkWalletDbContext db, ILogger<BalanceSnapshotService> logger) : IBalanceSnapshotService
{
    public async Task<Result<BalanceSnapshotData>> TakeTotalTraderBalanceSnapshot(long traderTelegramId)
    {
        try
        {
            var trader = await db.Traders
                .Include(t => t.Portfolio)
                .Include(t => t.Orders)
                .FirstOrDefaultAsync(t => t.TelegramId == traderTelegramId);

            if (trader == null)
                return Fail("Трейдер на найден");

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

            return Ok(new(traderTelegramId, totalBalance, mainBalance, longOrderReserve, shortOrderReserve, balanceInTokens, DateTime.UtcNow));
        }
        catch (DomainException ex)
        {
            return Fail($"Ошибка бизнес-логики: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Ошибка создания снапшота для трейдера {traderTelegramId}");
            return Fail("Внутренняя ошибка сервера");
        }
    }
}

public record BalanceSnapshotData(
    long traderTelegramId, decimal totalBalance,
    decimal mainBalance, decimal longOrderReserve, decimal shortOrderReserve,
    decimal balanceInTokens, DateTime dateTimeSnapshot);