using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Entities;
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
                    Portfolio = t.Portfolio.Select(p => new PortfolioSnapshot(p.CharacterTokenId, p.Quantity)),
                    ActiveOrders = t.Orders
                        .Where(o => o.Status == OrderStatus.Active)
                        .Select(o => new OrderSnapshot(o.Type, o.CharacterTokenId, o.Quantity - o.FilledQuantity, o.Price))
                })
                .AsSplitQuery()
                .FirstOrDefaultAsync();

            if (data == null)
                return Fail("Трейдер на найден");

            var tokenPrices = await LoadTokenPricesAsync(data.ActiveOrders, data.Portfolio);

            var miningSlotsValue = await db.MiningMachineSlots
                .Where(s => s.TraderId == traderTelegramId && s.Status != MiningMachineSlotStatus.Sold)
                .SumAsync(s => s.Cost);

            var (totalBalance, longOrderReserve, shortOrderReserve, balanceInTokens) =
                ComputeSnapshot(data.Balance, data.ActiveOrders, data.Portfolio, tokenPrices, miningSlotsValue);

            return Ok(new(traderTelegramId, totalBalance, data.Balance, longOrderReserve, shortOrderReserve, balanceInTokens, DateTime.UtcNow));
        }, logger, nameof(BalanceSnapshotService));
    }

    public async Task<Result<IReadOnlyDictionary<long, BalanceSnapshotData>>> TakeTotalTraderBalanceSnapshotsAsync(IEnumerable<long> traderTelegramIds)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var ids = traderTelegramIds.Distinct().ToArray();
            if (ids.Length == 0)
                return Result<IReadOnlyDictionary<long, BalanceSnapshotData>>.Ok(new Dictionary<long, BalanceSnapshotData>());

            var data = await db.Traders
                .Where(t => ids.Contains(t.TelegramId))
                .Select(t => new
                {
                    t.TelegramId,
                    t.Balance,
                    Portfolio = t.Portfolio.Select(p => new PortfolioSnapshot(p.CharacterTokenId, p.Quantity)),
                    ActiveOrders = t.Orders
                        .Where(o => o.Status == OrderStatus.Active)
                        .Select(o => new OrderSnapshot(o.Type, o.CharacterTokenId, o.Quantity - o.FilledQuantity, o.Price))
                })
                .AsSplitQuery()
                .ToListAsync();

            var tokenPrices = await LoadTokenPricesAsync(
                data.SelectMany(d => d.ActiveOrders),
                data.SelectMany(d => d.Portfolio));

            var miningSlotsByTrader = await db.MiningMachineSlots
                .Where(s => ids.Contains(s.TraderId) && s.Status != MiningMachineSlotStatus.Sold)
                .GroupBy(s => s.TraderId)
                .Select(g => new { TraderId = g.Key, TotalCost = g.Sum(s => s.Cost) })
                .ToDictionaryAsync(x => x.TraderId, x => x.TotalCost);

            var result = new Dictionary<long, BalanceSnapshotData>();
            foreach (var trader in data)
            {
                var miningSlotsValue = miningSlotsByTrader.GetValueOrDefault(trader.TelegramId, 0m);

                var (totalBalance, longOrderReserve, shortOrderReserve, balanceInTokens) =
                    ComputeSnapshot(trader.Balance, trader.ActiveOrders, trader.Portfolio, tokenPrices, miningSlotsValue);

                result[trader.TelegramId] = new(trader.TelegramId, totalBalance, trader.Balance, longOrderReserve, shortOrderReserve, balanceInTokens, DateTime.UtcNow);
            }

            return Result<IReadOnlyDictionary<long, BalanceSnapshotData>>.Ok(result);
        }, logger, nameof(BalanceSnapshotService));
    }

    private async Task<Dictionary<string, decimal>> LoadTokenPricesAsync(
        IEnumerable<OrderSnapshot> activeOrders,
        IEnumerable<PortfolioSnapshot> portfolio)
    {
        var activeSymbols = activeOrders
            .Where(o => o.Type == OrderType.Sell)
            .Select(o => o.CharacterTokenId)
            .Union(portfolio.Select(p => p.CharacterTokenId))
            .ToArray();

        if (activeSymbols.Length == 0)
            return new Dictionary<string, decimal>();

        return await db.CharacterTokens
            .Where(t => activeSymbols.Contains(t.Symbol))
            .Select(t => new { t.Symbol, t.CurrentPrice })
            .ToDictionaryAsync(x => x.Symbol, x => x.CurrentPrice);
    }

    private static (decimal Total, decimal LongOrderReserve, decimal ShortOrderReserve, decimal BalanceInTokens) ComputeSnapshot(
        decimal mainBalance,
        IEnumerable<OrderSnapshot> activeOrders,
        IEnumerable<PortfolioSnapshot> portfolio,
        Dictionary<string, decimal> tokenPrices,
        decimal miningSlotsValue)
    {
        var longOrderReserve = 0m;
        var shortOrderReserve = 0m;
        var balanceInTokens = 0m;

        foreach (var order in activeOrders)
        {
            if (order.Type == OrderType.Buy)
                longOrderReserve += order.Remaining * order.Price;
            else if (tokenPrices.TryGetValue(order.CharacterTokenId, out var price))
                shortOrderReserve += order.Remaining * price;
        }

        foreach (var item in portfolio)
            if (tokenPrices.TryGetValue(item.CharacterTokenId, out var price))
                balanceInTokens += item.Quantity * price;

        return (mainBalance + longOrderReserve + shortOrderReserve + balanceInTokens + miningSlotsValue,
            longOrderReserve, shortOrderReserve, balanceInTokens);
    }

    private sealed record OrderSnapshot(OrderType Type, string CharacterTokenId, decimal Remaining, decimal Price);

    private sealed record PortfolioSnapshot(string CharacterTokenId, int Quantity);
}

public record BalanceSnapshotData(
    long traderTelegramId, decimal totalBalance,
    decimal mainBalance, decimal longOrderReserve, decimal shortOrderReserve,
    decimal balanceInTokens, DateTime dateTimeSnapshot);
