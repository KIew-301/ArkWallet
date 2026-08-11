using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MarketMaker;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MarketMaker;
using static Result;

internal class MarketMakerOrderService(
    ArkWalletDbContext dbContext,
    IOrderCreationService orderCreationService,
    ILogger<MarketMakerOrderService> logger) : IMarketMakerOrderService
{
    public async Task<Result> ExecuteMarketOrderAsync(long botId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var bot = await dbContext.MarketMakerBots.FindAsync(botId);

            if (bot == null)
                return Result.Fail($"Бот с ID {botId} не найден");

            var token = await dbContext.CharacterTokens
                .FindAsync(bot.Symbol);

            if (token == null)
                return Fail($"Токен {bot.Symbol} не найден");

            var command = BuildOrderCommand(bot, token);

            var result = await orderCreationService.CreateOrderAsync(command);

            if (!result.IsSuccess)
                return Fail($"Не удалось создать ордер: {result.Message}");

            return Ok();
        }, logger, nameof(MarketMakerOrderService));
    }

    public async Task<Result> ExecuteMarketMakerOrdersAsync(IEnumerable<long> botIds)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var ids = botIds.Distinct().ToArray();

            if (ids.Length == 0)
                return Ok();

            var bots = await dbContext.MarketMakerBots
                .Where(b => ids.Contains(b.Id) && b.IsActive)
                .ToListAsync();

            if (bots.Count == 0)
                return Result.Fail("Список ботов пуст");

            var symbols = bots.Select(b => b.Symbol).Distinct().ToArray();

            var tokens = await dbContext.CharacterTokens
                .Where(t => symbols.Contains(t.Symbol))
                .ToDictionaryAsync(t => t.Symbol);

            var commands = new List<CreateOrderCommand>(bots.Count);

            foreach (var bot in bots)
            {
                if (!tokens.TryGetValue(bot.Symbol, out var token))
                {
                    logger.LogWarning("Токен {Symbol} не найден", bot.Symbol);
                    continue;
                }

                commands.Add(BuildOrderCommand(bot, token));
            }

            if (commands.Count == 0)
                return Result.Fail("Не удалось сформировать команды ордеров");

            commands = commands.OrderBy(_ => Guid.NewGuid()).ToList();

            var result = await orderCreationService.CreateOrdersAsync(commands);

            if (!result.IsSuccess)
                return Result.Fail($"Не удалось создать ордера: {result.Message}");

            return Ok();
        }, logger, nameof(MarketMakerOrderService));
    }

    private CreateOrderCommand BuildOrderCommand(MarketMakerBot bot, CharacterToken token)
    {
        var isBuyer = bot.Role == BotRole.Buyer;
        var deviation = 0.2m;

        var targetPrice = isBuyer
            ? token.CurrentPrice * (1 + deviation)
            : token.CurrentPrice * (1 - deviation);

        var minPower = (int)(bot.BasePower * 0.5m);
        var maxPower = (int)(bot.BasePower + minPower);

        var quantity = RandomNumberGenerator.GetInt32(minPower, maxPower);
        var direction = isBuyer ? "купить" : "продать";

        return new CreateOrderCommand(
            bot.TraderId,
            direction,
            bot.Symbol,
            quantity,
            Math.Round(targetPrice, 2)
        );
    }
}
