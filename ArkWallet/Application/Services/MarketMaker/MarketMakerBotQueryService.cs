using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MarketMaker;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MarketMaker;

internal class MarketMakerBotQueryService(
    ArkWalletDbContext dbContext,
    ILogger<MarketMakerBotQueryService> logger) : IMarketMakerBotQueryService
{
    public async Task<Result<List<MarketMakerBot>>> GetBotsBySymbolAsync(string symbol)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return Result<List<MarketMakerBot>>.Fail("Symbol cannot be empty");

            var bots = await dbContext.MarketMakerBots
                .Where(b => b.Symbol == symbol)
                .ToListAsync();

            return Result<List<MarketMakerBot>>.Ok(bots);
        }, logger, nameof(MarketMakerBotQueryService));
    }

    public async Task<Result<MarketMakerBot>> GetBotByIdAsync(long botId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var bot = await dbContext.MarketMakerBots
                .FirstOrDefaultAsync(b => b.Id == botId);

            if (bot == null)
                return Result<MarketMakerBot>.Fail($"Bot with ID {botId} not found");

            return Result<MarketMakerBot>.Ok(bot);
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
                bot.SetBasePower(basePower.Value);

            if (role != null)
            {
                var parsed = Enum.Parse<BotRole>(role, ignoreCase: true);
                bot.SetRole(parsed);
            }

            if (isActive.HasValue)
                bot.SetActive(isActive.Value);

            await dbContext.SaveChangesAsync();
            return Result.Ok();
        }, logger, nameof(MarketMakerBotQueryService));
    }
}
