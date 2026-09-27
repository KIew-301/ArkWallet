using System.Collections.Concurrent;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using OrderStatus = ArkWallet.Core.General.Domain.ValueObjects.OrderStatus;
using OrderType = ArkWallet.Core.General.Domain.ValueObjects.OrderType;

namespace ArkWallet.Core.General.Application.Services.Orchestrators;

/// <summary>
/// Пакетный сбор рыночных условий для коллекции модификаторов.
/// Текущая цена читается из токенов (одна строка на символ, обновляется при торгах);
/// «база» (открытие первой свечи) кэшируется и не перечитывается после прогрева;
/// «день назад» — точечный запрос последней свечи старше 24 ч (индекс CharacterTokenId+Timestamp).
/// </summary>
internal sealed class MarketDataProvider(ArkWalletDbContext dbContext)
{
    private readonly ConcurrentDictionary<string, decimal> _basePriceCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (decimal? Value, DateTime FetchedAt)> _dayAgoCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan DayAgoTtl = TimeSpan.FromMinutes(30);

    public async Task<IReadOnlyDictionary<string, MarketConditions>> LoadAsync(
        MarketDataMask mask, IReadOnlyCollection<string> symbols, CancellationToken ct)
    {
        var result = new Dictionary<string, MarketConditions>(StringComparer.OrdinalIgnoreCase);
        if (symbols.Count == 0 || mask == MarketDataMask.None)
        {
            return result;
        }

        var needCurrent = mask.HasFlag(MarketDataMask.CurrentPrice);
        var needDayAgo = mask.HasFlag(MarketDataMask.DayAgoPrice);
        var needBase = mask.HasFlag(MarketDataMask.BasePrice);
        var needLevels = mask.HasFlag(MarketDataMask.ExistingLevels);

        var dayAgoCutoff = DateTime.UtcNow.AddHours(-24);

        // Текущая цена — одна строка на символ.
        var currentBySymbol = needCurrent
            ? await dbContext.CharacterTokens
                .Where(t => symbols.Contains(t.Symbol))
                .ToDictionaryAsync(t => t.Symbol, t => t.CurrentPrice, ct)
            : new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        // Активные ордера по символам — уровни для дедупликации сеток.
        IReadOnlyDictionary<string, IReadOnlyCollection<PlacedOrderLevel>> levelsBySymbol =
            new Dictionary<string, IReadOnlyCollection<PlacedOrderLevel>>(StringComparer.OrdinalIgnoreCase);
        if (needLevels)
        {
            var levelRows = await dbContext.TradeOrders
                .Where(o => o.Status == OrderStatus.Active && symbols.Contains(o.CharacterTokenId))
                .Select(o => new { o.CharacterTokenId, o.Price, o.Type })
                .ToListAsync(ct);

            levelsBySymbol = levelRows
                .GroupBy(r => r.CharacterTokenId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyCollection<PlacedOrderLevel>)g
                        .Select(r => new PlacedOrderLevel(r.Price, r.Type == OrderType.Buy))
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);
        }

        foreach (var symbol in symbols)
        {
            if (needCurrent && !currentBySymbol.ContainsKey(symbol))
            {
                continue;
            }

            var current = needCurrent ? currentBySymbol[symbol] : 0m;

            decimal? basePrice = null;
            if (needBase)
            {
                if (!_basePriceCache.TryGetValue(symbol, out var cached))
                {
                    cached = await dbContext.PriceCandles
                        .Where(c => c.CharacterTokenId == symbol)
                        .OrderBy(c => c.Timestamp)
                        .Select(c => c.OpenPrice)
                        .FirstOrDefaultAsync(ct);
                    _basePriceCache[symbol] = cached;
                }

                if (cached == 0m)
                {
                    continue;
                }

                basePrice = cached;
            }

            decimal? dayAgo = null;
            if (needDayAgo)
            {
                if (_dayAgoCache.TryGetValue(symbol, out var cached) &&
                    DateTime.UtcNow - cached.FetchedAt < DayAgoTtl)
                {
                    dayAgo = cached.Value;
                }
                else
                {
                    dayAgo = await dbContext.PriceCandles
                        .Where(c => c.CharacterTokenId == symbol && c.Timestamp <= dayAgoCutoff)
                        .OrderByDescending(c => c.Timestamp)
                        .Select(c => c.OpenPrice)
                        .FirstOrDefaultAsync(ct);

                    if (dayAgo is 0m or null)
                    {
                        _dayAgoCache.TryAdd(symbol, (null, DateTime.UtcNow));
                    }
                    else
                    {
                        _dayAgoCache[symbol] = (dayAgo, DateTime.UtcNow);
                    }
                }

                if (dayAgo is 0m or null)
                {
                    continue;
                }
            }

            var levels = needLevels && levelsBySymbol.TryGetValue(symbol, out var found)
                ? found
                : Array.Empty<PlacedOrderLevel>();

            result[symbol] = new MarketConditions(current, dayAgo, basePrice, levels);
        }

        return result;
    }
}