using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TraderServices;
using static ArkWallet.Application.Common.Result<BalanceSnapshotData>;

internal class BalanceSnapshotService(ArkWalletDbContext db, ILogger<BalanceSnapshotService> logger) : IBalanceSnapshotService
{
    public async Task<Result<BalanceSnapshotData>> TakeTotalTraderBalanceSnapshot(long traderTelegramId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var data = await db.Traders
                .Where(t => t.TelegramId == traderTelegramId)
                .Select(t => new
                {
                    t.Balance,
                    Portfolio = t.Portfolio.Select(p => new { p.CharacterTokenId, p.Quantity }),
                    ActiveOrders = t.Orders
                        .Where(o => o.Status == OrderStatus.Active)
                        .Select(o => new { o.Type, o.CharacterTokenId, Remaining = o.Quantity - o.FilledQuantity, o.Price })
                })
                .AsSplitQuery()
                .FirstOrDefaultAsync();

            if (data == null)
                return Fail("Трейдер на найден");

            var mainBalance = data.Balance;
            var longOrderReserve = 0m;
            var shortOrderReserve = 0m;
            var balanceInTokens = 0m;

            var activeSymbols = data.ActiveOrders
                .Where(o => o.Type == OrderType.Sell)
                .Select(o => o.CharacterTokenId)
                .Union(data.Portfolio.Select(p => p.CharacterTokenId))
                .ToArray();

            var tokenPrices = new Dictionary<string, decimal>();

            if (activeSymbols.Length > 0)
                tokenPrices = await db.CharacterTokens
                    .Where(t => activeSymbols.Contains(t.Symbol))
                    .Select(t => new { t.Symbol, t.CurrentPrice })
                    .ToDictionaryAsync(x => x.Symbol, x => x.CurrentPrice);

            foreach (var order in data.ActiveOrders)
            {
                if (order.Type == OrderType.Buy)
                    longOrderReserve += order.Remaining * order.Price;
                else if (tokenPrices.TryGetValue(order.CharacterTokenId, out var price))
                    shortOrderReserve += order.Remaining * price;
            }

            foreach (var item in data.Portfolio)
                if (tokenPrices.TryGetValue(item.CharacterTokenId, out var price))
                    balanceInTokens += item.Quantity * price;

            var totalBalance = mainBalance + longOrderReserve + shortOrderReserve + balanceInTokens;

            return Ok(new(traderTelegramId, totalBalance, mainBalance, longOrderReserve, shortOrderReserve, balanceInTokens, DateTime.UtcNow));
        }, logger, nameof(BalanceSnapshotService));
    }
}

public record BalanceSnapshotData(
    long traderTelegramId, decimal totalBalance,
    decimal mainBalance, decimal longOrderReserve, decimal shortOrderReserve,
    decimal balanceInTokens, DateTime dateTimeSnapshot);