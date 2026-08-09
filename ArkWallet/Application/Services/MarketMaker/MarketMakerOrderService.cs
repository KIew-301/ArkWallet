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

            var isBuyer = bot.Role == BotRole.Buyer;
            var deviation = 0.2m;

            var targetPrice = isBuyer
                ? token.CurrentPrice * (1 + deviation)
                : token.CurrentPrice * (1 - deviation);

            var minPower = (int)(bot.BasePower * 0.5m);
            var maxPower = (int)(bot.BasePower + minPower);

            var quantity = RandomNumberGenerator.GetInt32(minPower, maxPower);
            var direction = isBuyer ? "купить" : "продать";

            var command = new CreateOrderCommand(
                bot.TraderId,
                direction,
                bot.Symbol,
                quantity,
                Math.Round(targetPrice, 2)
            );

            var result = await orderCreationService.CreateOrderAsync(command);

            if (!result.IsSuccess)
                return Fail($"Не удалось создать ордер: {result.Message}");

            return Ok();
        }, logger, nameof(MarketMakerOrderService));
    }
}