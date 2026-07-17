using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.MarketMaker;
using ArkWallet.Application.Contracts.Orchestrators;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.Orchestrators;

internal class MarketMakerOrchestrator(
    ArkWalletDbContext dbContext,
    IMarketMakerBotRegistrationService botRegistrationService,
    IPortfolioUpdatingService portfolioUpdatingService,
    IOrderCreationService orderCreationService,
    IMarketMakerOrderService marketMakerOrderService,
    MarketMakerGridEngine marketMakerGridEngine,
    ILogger<MarketMakerOrchestrator> logger) : IMarketMakerOrchestrator
{
    public async Task<Result> EnsureBotsRegisteredAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var tokens = await dbContext.CharacterTokens
                .Where(t => t.IsActive)
                .Select(t => t.Symbol)
                .ToListAsync();

            foreach (var symbol in tokens)
            {
                foreach (var (traderId, role) in new[] { (101L, BotRole.Buyer), (102L, BotRole.Seller) })
                {
                    var exists = await dbContext.MarketMakerBots
                        .AnyAsync(b => b.TraderId == traderId && b.Symbol == symbol);

                    if (exists)
                        continue;

                    var botResult = await botRegistrationService.RegisterBotAsync(
                        (int)traderId,
                        symbol,
                        role,
                        20m);

                    if (!botResult.IsSuccess)
                    {
                        logger.LogError("Failed to register bot {TraderId} for {Symbol}: {Error}", traderId, symbol, botResult.Message);
                        return Result.Fail($"Не удалось зарегистрировать бота {traderId} для {symbol}: {botResult.Message}");
                    }

                    logger.LogInformation("Bot {TraderId} registered with role {Role} for {Symbol}", traderId, role, symbol);
                }
            }

            return Result.Ok();
        }, logger, nameof(MarketMakerOrchestrator));
    }

    public async Task<Result> EnsureTraderBalancesAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var traderIds = new[] { 101L, 102L };

            foreach (var traderId in traderIds)
            {
                var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);
                if (trader == null)
                {
                    logger.LogWarning("Trader {TraderId} not found", traderId);
                    continue;
                }

                if (trader.Balance < 1_000_000_000m)
                {
                    trader.AddToBalance(1_000_000_000m - trader.Balance);
                    trader.MarkDirty();
                    await dbContext.SaveChangesAsync();
                    logger.LogInformation("Trader {TraderId} balance replenished", traderId);
                }
            }

            var tokens = await dbContext.CharacterTokens
                .Where(t => t.IsActive)
                .Select(t => t.Symbol)
                .ToListAsync();

            foreach (var symbol in tokens)
            {
                foreach (var traderId in traderIds)
                {
                    var portfolioResult = await portfolioUpdatingService.CreateOrUpdatePortfolioAsync(
                        traderId,
                        symbol,
                        100_000_000);

                    if (!portfolioResult.IsSuccess)
                    {
                        logger.LogError("Failed to update portfolio for trader {TraderId} on {Symbol}: {Error}", traderId, symbol, portfolioResult.Message);
                        return Result.Fail($"Не удалось обновить портфель трейдера {traderId} для {symbol}: {portfolioResult.Message}");
                    }
                }
            }

            return Result.Ok();
        }, logger, nameof(MarketMakerOrchestrator));
    }

    public async Task<Result> UpdateAllBotsGridAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var bots = await dbContext.MarketMakerBots
                .Where(b => b.IsActive)
                .ToListAsync();

            if (bots.Count == 0)
                return Result.Fail("Список ботов пуст");

            foreach (var bot in bots)
            {
                var result = await UpdateBotGridAsync(bot);
                if (!result.IsSuccess)
                    return result;
            }

            return Result.Ok();
        }, logger, nameof(MarketMakerOrchestrator));
    }

    public async Task<Result> ProcessBotsAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var bots = await dbContext.MarketMakerBots
                .Where(b => b.IsActive)
                .ToListAsync();

            bots = bots.OrderBy(_ => Guid.NewGuid()).ToList();

            if (bots.Count == 0)
                return Result.Fail("Список ботов пуст");

            foreach (var bot in bots)
            {
                if (DateTime.UtcNow >= bot.NextPowerChange)
                {
                    bot.UpdatePower(10, 50);
                    logger.LogDebug("Bot {BotId} power updated to {Power}", bot.Id, bot.BasePower);
                }

                if (DateTime.UtcNow >= bot.NextRebalance || DateTime.UtcNow.Minute == 0)
                {
                    var result = await UpdateBotGridAsync(bot);
                    if (!result.IsSuccess)
                        return result;

                    bot.UpdateRebalanced();
                    logger.LogDebug("Bot {BotId} grid updated", bot.Id);
                }

                var marketOrderResult = await marketMakerOrderService.ExecuteMarketOrderAsync(bot.TraderId, bot.Symbol);
                if (!marketOrderResult.IsSuccess)
                {
                    logger.LogWarning("Failed to execute market order for bot {BotId}: {Error}", bot.Id, marketOrderResult.Message);
                }
            }

            await dbContext.SaveChangesAsync();
            return Result.Ok();
        }, logger, nameof(MarketMakerOrchestrator));
    }

    private async Task<Result> UpdateBotGridAsync(MarketMakerBot bot)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var token = await dbContext.CharacterTokens
                .FirstOrDefaultAsync(t => t.Symbol == bot.Symbol);

            if (token == null)
            {
                logger.LogWarning("Token {Symbol} not found", bot.Symbol);
                return Result.Fail($"Токен {bot.Symbol} не найден");
            }

            var existingOrders = await dbContext.TradeOrders
                .Where(o => o.CharacterTokenId == bot.Symbol
                            && o.TraderTelegramId == bot.TraderId
                            && o.Status == OrderStatus.Active)
                .ToListAsync();

            var commands = marketMakerGridEngine.GetOrdersToPlace(bot, token.CurrentPrice, existingOrders);

            foreach (var command in commands)
            {
                var result = await orderCreationService.CreateOrderAsync(command);
                if (!result.IsSuccess)
                {
                    logger.LogWarning("Failed to create order for bot {BotId}: {Error}", bot.Id, result.Message);
                }
            }

            logger.LogDebug("Grid updated for bot {BotId}, {Count} orders placed", bot.Id, commands.Count);
            return Result.Ok();
        }, logger, nameof(MarketMakerOrchestrator));
    }
}