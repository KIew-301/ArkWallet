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
        MarketDataMasks mask, IReadOnlyCollection<string> symbols, CancellationToken ct)
    {
        var result = new Dictionary<string, MarketConditions>(StringComparer.OrdinalIgnoreCase);
        if (symbols.Count == 0 || mask == MarketDataMasks.None)
            return result;

        var currentBySymbol = await LoadCurrentPricesAsync(mask, symbols, ct);
        var levelsByTokenId = await LoadLevelsByTokenIdsAsync(mask, symbols, ct);

        foreach (var symbol in symbols)
        {
            decimal? basePrice = GetCachedBasePrice(symbol, ct);
            if (!basePrice.HasValue)
                continue;

            decimal? dayAgo = null;
            if (mask.HasFlag(MarketDataMasks.DayAgoPrice))
                dayAgo = await LoadDayAgoPriceAsync(symbol, ct);

            if (dayAgo is 0m or null)
                continue;

            var levels = GetLevelsForSymbol(symbol, levelsByTokenId);

            result[symbol] = new MarketConditions(currentBySymbol[symbol], dayAgo, basePrice.Value, levels);
        }

        return result;
    }

    private async Task<Dictionary<string, decimal>> LoadCurrentPricesAsync(
        MarketDataMasks mask, IReadOnlyCollection<string> symbols, CancellationToken ct)
    {
        if (!mask.HasFlag(MarketDataMasks.CurrentPrice))
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        return await dbContext.CharacterTokens
            .Where(t => symbols.Contains(t.Symbol))
            .ToDictionaryAsync(t => t.Symbol, t => t.CurrentPrice, ct);
    }

    private async Task<Dictionary<string, IReadOnlyCollection<PlacedOrderLevel>>> LoadLevelsByTokenIdsAsync(
        MarketDataMasks mask, IReadOnlyCollection<string> symbols, CancellationToken ct)
    {
        if (!mask.HasFlag(MarketDataMasks.ExistingLevels))
            return new Dictionary<string, IReadOnlyCollection<PlacedOrderLevel>>(StringComparer.OrdinalIgnoreCase);

        var selectedIds = symbols.ToArray();
        var levelRows = await dbContext.TradeOrders
            .Where(o => o.Status == OrderStatus.Active && selectedIds.Contains(o.CharacterTokenId))
            .Select(o => new { o.CharacterTokenId, o.Price, o.Type })
            .ToListAsync(ct);

        return levelRows
            .GroupBy(r => r.CharacterTokenId)
            .ToDictionary(g => g.Key,
                g => (IReadOnlyCollection<PlacedOrderLevel>)g
                    .Select(r => new PlacedOrderLevel(r.Price, r.Type == OrderType.Buy))
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<decimal?> LoadDayAgoPriceAsync(string symbol, CancellationToken ct)
    {
        var dayAgoCutoff = DateTime.UtcNow.AddHours(-24);

        if (_dayAgoCache.TryGetValue(symbol, out var cached) &&
            DateTime.UtcNow - cached.FetchedAt < DayAgoTtl)
        {
            return cached.Value;
        }

        var dayAgo = await dbContext.PriceCandles
            .Where(c => c.CharacterTokenId == symbol && c.Timestamp <= dayAgoCutoff)
            .OrderByDescending(c => c.Timestamp)
            .Select(c => c.OpenPrice)
            .FirstOrDefaultAsync(ct);

        if (dayAgo == 0m)
            _dayAgoCache.TryAdd(symbol, (null, DateTime.UtcNow));
        else
            _dayAgoCache[symbol] = (dayAgo, DateTime.UtcNow);

        return dayAgo;
    }

    private decimal? GetCachedBasePrice(string symbol, CancellationToken ct)
    {
        if (!_basePriceCache.TryGetValue(symbol, out var cached))
        {
            cached = dbContext.PriceCandles
                .Where(c => c.CharacterTokenId == symbol)
                .OrderBy(c => c.Timestamp)
                .Select(c => c.OpenPrice)
                .FirstOrDefaultAsync(ct)
                .Result;

            _basePriceCache[symbol] = cached;
        }

        return cached == 0m ? (decimal?)null : cached;
    }

    private static IReadOnlyCollection<PlacedOrderLevel> GetLevelsForSymbol(
        string symbol, Dictionary<string, IReadOnlyCollection<PlacedOrderLevel>> levelsByTokenId)
        => levelsByTokenId.TryGetValue(symbol, out var found) ? found : Array.Empty<PlacedOrderLevel>();
}