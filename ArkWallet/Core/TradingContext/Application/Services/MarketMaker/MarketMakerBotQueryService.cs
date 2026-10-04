using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.MarketMaker;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.TradingContext.Application.Services.MarketMaker;

internal class MarketMakerBotQueryService(
    ArkWalletDbContext dbContext,
    ILogger<MarketMakerBotQueryService> logger) : IMarketMakerBotQueryService
{
    public async Task<Result<List<MarketMakerBotRecord>>> GetAllBotsAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var bots = await dbContext.MarketMakerBots
                .OrderBy(b => b.Symbol)
                .ThenBy(b => b.Role)
                .ThenBy(b => b.Id)
                .ToListAsync();

            return Result<List<MarketMakerBotRecord>>.Ok(bots);
        }, logger, nameof(MarketMakerBotQueryService));
    }

    public async Task<Result<List<MarketMakerBotRecord>>> GetBotsBySymbolAsync(string symbol)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return Result<List<MarketMakerBotRecord>>.Fail("Symbol cannot be empty");

            var bots = await dbContext.MarketMakerBots
                .Where(b => b.Symbol == symbol)
                .ToListAsync();

            return Result<List<MarketMakerBotRecord>>.Ok(bots);
        }, logger, nameof(MarketMakerBotQueryService));
    }

    public async Task<Result<MarketMakerBotRecord>> GetBotByIdAsync(long botId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var bot = await dbContext.MarketMakerBots
                .FirstOrDefaultAsync(b => b.Id == botId);

            if (bot == null)
                return Result<MarketMakerBotRecord>.Fail($"Bot with ID {botId} not found");

            return Result<MarketMakerBotRecord>.Ok(bot);
        }, logger, nameof(MarketMakerBotQueryService));
    }

    public async Task<Result> UpdateBotAsync(long botId, decimal? basePower, string? role, bool? isActive)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var bot = await dbContext.MarketMakerBots
                .FirstOrDefaultAsync(b => b.Id == botId);

            if (bot == null)
                return Result.Fail($"Bot with ID {botId} not found");

            if (basePower.HasValue)
            {
                bot.BasePower = basePower.Value;
                bot.ActivePower = basePower.Value;
            }

            if (role != null)
            {
                var parsed = Enum.Parse<BotRole>(role, ignoreCase: true);
                bot.Role = parsed;
            }

            if (isActive.HasValue)
                bot.IsActive = isActive.Value;

            await dbContext.SaveChangesAsync();
            return Result.Ok();
        }, logger, nameof(MarketMakerBotQueryService));
    }
}
