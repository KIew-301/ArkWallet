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
        try
        {
            var botConfigs = new[]
            {
            new { TraderId = 101L, Role = BotRole.Buyer, Symbol = "ZZZ", Power = 20m },
            new { TraderId = 102L, Role = BotRole.Seller, Symbol = "ZZZ", Power = 20m }
        };

            foreach (var config in botConfigs)
            {
                var exists = await dbContext.MarketMakerBots
                    .AnyAsync(b => b.TraderId == config.TraderId && b.Symbol == config.Symbol);

                if (exists)
                    continue;

                var botResult = await botRegistrationService.RegisterBotAsync(
                    (int)config.TraderId,
                    config.Symbol,
                    config.Role,
                    config.Power);

                if (!botResult.IsSuccess)
                {
                    logger.LogError("Failed to register bot {TraderId}: {Error}", config.TraderId, botResult.Message);
                    return Result.Fail($"Не удалось зарегистрировать бота {config.TraderId}: {botResult.Message}");
                }

                logger.LogInformation("Bot {TraderId} registered with role {Role}", config.TraderId, config.Role);
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MarketMakerOrchestrator:EnsureBotsRegisteredAsync Error");
            return Result.Fail($"Внутренняя ошибка сервера: {ex.Message}");
        }
    }

    public async Task<Result> EnsureTraderBalancesAsync()
    {
        try
        {
            var botConfigs = new[]
            {
            new { TraderId = 101L, Symbol = "ZZZ" },
            new { TraderId = 102L, Symbol = "ZZZ" }
        };

            foreach (var config in botConfigs)
            {
                var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == config.TraderId);
                if (trader == null)
                {
                    logger.LogWarning("Trader {TraderId} not found", config.TraderId);
                    continue;
                }

                var needUpdate = false;

                if (trader.Balance < 1_000_000_000m)
                {
                    trader.AddToBalance(1_000_000_000m - trader.Balance);
                    trader.MarkDirty();
                    needUpdate = true;
                }

                var portfolioResult = await portfolioUpdatingService.CreateOrUpdatePortfolioAsync(
                    config.TraderId,
                    config.Symbol,
                    100_000_000);

                if (!portfolioResult.IsSuccess)
                {
                    logger.LogError("Failed to update portfolio for trader {TraderId}: {Error}", config.TraderId, portfolioResult.Message);
                    return Result.Fail($"Не удалось обновить портфель трейдера {config.TraderId}: {portfolioResult.Message}");
                }

                if (needUpdate)
                {
                    await dbContext.SaveChangesAsync();
                    logger.LogInformation("Trader {TraderId} balance replenished", config.TraderId);
                }
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MarketMakerOrchestrator:EnsureTraderBalancesAsync Error");
            return Result.Fail($"Внутренняя ошибка сервера: {ex.Message}");
        }
    }

    public async Task<Result> UpdateAllBotsGridAsync()
    {
        try
        {
            var bots = await dbContext.MarketMakerBots
                .Where(b => b.IsActive && b.Symbol == "ZZZ")
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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MarketMakerOrchestrator:UpdateAllBotsGridAsync Error");
            return Result.Fail($"Внутренняя ошибка сервера: {ex.Message}");
        }
    }

    public async Task<Result> ProcessBotsAsync()
    {
        try
        {
            var bots = await dbContext.MarketMakerBots
                .Where(b => b.IsActive && b.Symbol == "ZZZ")
                .ToListAsync();

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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MarketMakerOrchestrator:ProcessBotsAsync Error");
            return Result.Fail($"Внутренняя ошибка сервера: {ex.Message}");
        }
    }

    private async Task<Result> UpdateBotGridAsync(MarketMakerBot bot)
    {
        try
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

            foreach (var order in existingOrders)
            {
                order.Cancel(bot.TraderId);
            }

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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MarketMakerOrchestrator:UpdateBotGridAsync Error for bot {BotId}", bot.Id);
            return Result.Fail($"Внутренняя ошибка сервера: {ex.Message}");
        }
    }
}