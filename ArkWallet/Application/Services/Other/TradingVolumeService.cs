using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.Other;

internal class TradingVolumeService(
    ArkWalletDbContext dbContext,
    ILogger<TradingVolumeService> logger) : ITradingVolumeService
{
    private const long BotIdMin = 100;
    private const long BotIdMax = 1000;

    public async Task<Result<decimal>> GetTokenVolumeAsync(string symbol, int periodDays, bool includeBots)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var query = dbContext.Trades
                .Where(t => t.CharacterTokenId == symbol);

            query = ApplyPeriodFilter(query, periodDays);
            if (!includeBots)
                query = query.Where(t => t.BuyerId < BotIdMin || t.BuyerId > BotIdMax)
                             .Where(t => t.SellerId < BotIdMin || t.SellerId > BotIdMax);

            var volume = await query.SumAsync(t => (decimal)t.Quantity * t.Price);
            return Result<decimal>.Ok(volume);
        }, logger, nameof(TradingVolumeService));
    }

    public async Task<Result<decimal>> GetTotalVolumeAsync(int periodDays, bool includeBots)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var query = dbContext.Trades.AsQueryable();

            query = ApplyPeriodFilter(query, periodDays);
            if (!includeBots)
                query = query.Where(t => t.BuyerId < BotIdMin || t.BuyerId > BotIdMax)
                             .Where(t => t.SellerId < BotIdMin || t.SellerId > BotIdMax);

            var volume = await query.SumAsync(t => (decimal)t.Quantity * t.Price);
            return Result<decimal>.Ok(volume);
        }, logger, nameof(TradingVolumeService));
    }

    public async Task<Result<List<(string Symbol, decimal Volume)>>> GetVolumePerTokenAsync(int periodDays, bool includeBots)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var query = dbContext.Trades.AsQueryable();

            query = ApplyPeriodFilter(query, periodDays);
            if (!includeBots)
                query = query.Where(t => t.BuyerId < BotIdMin || t.BuyerId > BotIdMax)
                             .Where(t => t.SellerId < BotIdMin || t.SellerId > BotIdMax);

            var trades = await query.Select(t => new { t.CharacterTokenId, t.Quantity, t.Price }).ToListAsync();

            var volumes = trades
                .GroupBy(t => t.CharacterTokenId)
                .Select(g => (Symbol: g.Key, Volume: g.Sum(t => (decimal)t.Quantity * t.Price)))
                .OrderByDescending(x => x.Volume)
                .ToList();

            return Result<List<(string, decimal)>>.Ok(volumes);
        }, logger, nameof(TradingVolumeService));
    }

    private static IQueryable<Domain.Entities.Trade> ApplyPeriodFilter(IQueryable<Domain.Entities.Trade> query, int periodDays)
    {
        if (periodDays > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-periodDays);
            query = query.Where(t => t.ExecutedAt >= cutoff);
        }
        return query;
    }
}
