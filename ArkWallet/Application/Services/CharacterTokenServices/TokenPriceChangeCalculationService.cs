using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.CharacterTokenServices;
using static Result<TokenPriceChangesData>;

internal class TokenPriceChangeCalculationService(ArkWalletDbContext dbContext, ILogger<TokenPriceChangeCalculationService> logger, TimeProvider timeProvider) : ITokenPriceChangesCalculationService
{
    public async Task<Result<TokenPriceChangesData>> TakeTokenPriceChangesAsync(string symbol, int periodDays)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (periodDays < 1)
                return Fail($"Минимальный период для расчёта: 1 день");

            var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == symbol);

            if (token == null)
                return Fail($"Токен с идентификатором {symbol} не найден");

            var cutoffDate = timeProvider.GetUtcNow().UtcDateTime.AddDays(-periodDays);
            var firstCandleInPeriod = await dbContext.PriceCandles
                .FirstOrDefaultAsync(c => c.CharacterTokenId == symbol && c.Timestamp >= cutoffDate);

            if (firstCandleInPeriod == null)
                return Fail("Истории цены токена не существует");

            var currentBalance = token.CurrentPrice;
            var previousBalance = firstCandleInPeriod.OpenPrice;
            var changeAbsolute = currentBalance - previousBalance;
            var сhangePercent = changeAbsolute / previousBalance * 100m;
            return Ok(new TokenPriceChangesData(currentBalance, previousBalance, changeAbsolute, сhangePercent));
        }, logger, nameof(TokenPriceChangeCalculationService));
    }

    public async Task<Dictionary<string, decimal>> TakeSymbolsPriceChangesAsync(string[] symbols, int candlePosition)
    {
        ArgumentNullException.ThrowIfNull(symbols);

        if (symbols.Length == 0)
            return new Dictionary<string, decimal>();

        if (candlePosition < 1)
            throw new ArgumentOutOfRangeException(nameof(candlePosition), "Позиция свечи должна быть больше нуля");

        var result = new Dictionary<string, decimal>(symbols.Length);

        var lastCandles = await dbContext.PriceCandles
            .Where(c => symbols.Contains(c.CharacterTokenId))
            .GroupBy(c => c.CharacterTokenId)
            .Select(g => new { Symbol = g.Key, Last = g.OrderByDescending(c => c.Timestamp).First() })
            .ToListAsync();

        var targetCandles = await dbContext.PriceCandles
            .Where(c => symbols.Contains(c.CharacterTokenId))
            .GroupBy(c => c.CharacterTokenId)
            .Select(g => new { Symbol = g.Key, Candle = g.OrderByDescending(c => c.Timestamp).Skip(candlePosition).FirstOrDefault() })
            .ToListAsync();

        var targetBySymbol = targetCandles
            .Where(x => x.Candle is not null)
            .ToDictionary(x => x.Symbol, x => x.Candle!);

        foreach (var last in lastCandles)
        {
            if (!targetBySymbol.TryGetValue(last.Symbol, out var target))
                continue;

            if (target.OpenPrice == 0m)
                continue;

            result[last.Symbol] = (last.Last.ClosePrice - target.OpenPrice) / target.OpenPrice * 100m;
        }

        return result;
    }
}