using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.TraderServices;
using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.TradingContext.Application.Services.TraderServices;
using static ArkWallet.Core.General.Application.Common.Result<BalanceSnapshotData>;

internal class BalanceSnapshotService(ArkWalletDbContext db, ILogger<BalanceSnapshotService> logger) : IBalanceSnapshotService
{
    public async Task<Result<BalanceSnapshotData>> TakeTotalTraderBalanceSnapshot(long traderTelegramId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var data = await FetchTraderDataAsync(traderTelegramId);

            if (data is null)
                return Fail("Трейдер на найден");

            var tokenPrices = await LoadTokenPricesAsync(data.Value.ActiveOrders, data.Value.Portfolio);
            var miningSlotsValue = await LoadMiningSlotsValueAsync(traderTelegramId);

            var tradingBalances = ComputeTradingBalances(data.Value.ActiveOrders, data.Value.Portfolio, tokenPrices);

            var totalBalance = data.Value.Balance + tradingBalances.LongOrderReserve + tradingBalances.ShortOrderReserve + tradingBalances.BalanceInTokens + miningSlotsValue;

            return Ok(new(traderTelegramId, totalBalance, data.Value.Balance, tradingBalances.LongOrderReserve, tradingBalances.ShortOrderReserve, tradingBalances.BalanceInTokens, DateTime.UtcNow));
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
                var tradingBalances = ComputeTradingBalances(trader.ActiveOrders, trader.Portfolio, tokenPrices);

                var totalBalance = trader.Balance + tradingBalances.LongOrderReserve + tradingBalances.ShortOrderReserve + tradingBalances.BalanceInTokens + miningSlotsValue;

                result[trader.TelegramId] = new(trader.TelegramId, totalBalance, trader.Balance, tradingBalances.LongOrderReserve, tradingBalances.ShortOrderReserve, tradingBalances.BalanceInTokens, DateTime.UtcNow);
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

    private async Task<(decimal Balance, IEnumerable<OrderSnapshot> ActiveOrders, IEnumerable<PortfolioSnapshot> Portfolio)?> FetchTraderDataAsync(long traderTelegramId)
    {
        var data = await db.Traders
            .Where(t => t.TelegramId == traderTelegramId)
            .Select(t => new
            {
                t.Balance,
                ActiveOrders = t.Orders
                    .Where(o => o.Status == OrderStatus.Active)
                    .Select(o => new OrderSnapshot(o.Type, o.CharacterTokenId, o.Quantity - o.FilledQuantity, o.Price)),
                Portfolio = t.Portfolio.Select(p => new PortfolioSnapshot(p.CharacterTokenId, p.Quantity))
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync();

        if (data is null)
            return null;

        return (data.Balance, data.ActiveOrders, data.Portfolio);
    }

    private async Task<decimal> LoadMiningSlotsValueAsync(long traderTelegramId)
    {
        return await db.MiningMachineSlots
            .Where(s => s.TraderId == traderTelegramId && s.Status != MiningMachineSlotStatus.Sold)
            .SumAsync(s => s.Cost);
    }

    private static (decimal LongOrderReserve, decimal ShortOrderReserve, decimal BalanceInTokens) ComputeTradingBalances(
        IEnumerable<OrderSnapshot> activeOrders,
        IEnumerable<PortfolioSnapshot> portfolio,
        Dictionary<string, decimal> tokenPrices)
    {
        var longOrderReserve = 0m;
        var shortOrderReserve = 0m;

        foreach (var order in activeOrders)
        {
            if (order.Type == OrderType.Buy)
                longOrderReserve += order.Remaining * order.Price;
            else if (tokenPrices.TryGetValue(order.CharacterTokenId, out var price))
                shortOrderReserve += order.Remaining * price;
        }

        var balanceInTokens = portfolio.Sum(item =>
            tokenPrices.TryGetValue(item.CharacterTokenId, out var price) ? item.Quantity * price : 0m);

        return (longOrderReserve, shortOrderReserve, balanceInTokens);
    }

    private sealed record OrderSnapshot(OrderType Type, string CharacterTokenId, decimal Remaining, decimal Price);

    private sealed record PortfolioSnapshot(string CharacterTokenId, int Quantity);
}

public record BalanceSnapshotData(
    long traderTelegramId,
    decimal totalBalance,
    decimal mainBalance,
    decimal longOrderReserve,
    decimal shortOrderReserve,
    decimal balanceInTokens,
    DateTime dateTimeSnapshot)
{
    /// <summary>Идентификатор трейдера в Telegram.</summary>
    public long traderTelegramId { get; init; } = traderTelegramId;

    /// <summary>Полный баланс: основной + резервы ордеров + портфель в токенах + слоты майнинга.</summary>
    public decimal totalBalance { get; init; } = totalBalance;

    /// <summary>Основной денежный баланс без учёта резервов и портфеля.</summary>
    public decimal mainBalance { get; init; } = mainBalance;

    /// <summary>Резерв под активные ордера на покупку (Buy).</summary>
    public decimal longOrderReserve { get; init; } = longOrderReserve;

    /// <summary>Резерв под активные ордера на продажу (Sell).</summary>
    public decimal shortOrderReserve { get; init; } = shortOrderReserve;

    /// <summary>Стоимость позиций в портфеле, выраженная в токенах по текущим ценам.</summary>
    public decimal balanceInTokens { get; init; } = balanceInTokens;

    /// <summary>Временная метка снимка баланса.</summary>
    public DateTime dateTimeSnapshot { get; init; } = dateTimeSnapshot;
}
