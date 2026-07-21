using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MarketMaker;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MarketMaker;

using static Result<MarketMakerBotRegistrationData>;

internal class MarketMakerBotRegistrationService(
    ArkWalletDbContext dbContext,
    ITraderRegistrationService traderRegistrationService,
    ILogger<MarketMakerBotRegistrationService> logger) : IMarketMakerBotRegistrationService
{
    public async Task<Result<MarketMakerBotRegistrationData>> RegisterBotAsync(int telegramFakeId, string symbol, BotRole botRole, decimal initialPower = 50)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync();

            if (string.IsNullOrWhiteSpace(symbol))
                return Fail("Символ токена не может быть пустым");

            if (initialPower <= 0)
                return Fail("Начальная мощность должна быть больше нуля");

            var registrationResult = await traderRegistrationService.RegisterTraderAsync(telegramFakeId, $"MarketMakerBot_{symbol}", false);

            if (!registrationResult.IsSuccess && registrationResult.Message != "Пользователь уже существует")
                return Fail($"Не удалось зарегистрировать трейдера: {registrationResult.Message}");

            var bot = MarketMakerBot.Create(telegramFakeId, symbol, botRole, initialPower);

            await dbContext.MarketMakerBots.AddAsync(bot);
            await dbContext.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new MarketMakerBotRegistrationData(
                BotId: bot.Id,
                TraderId: telegramFakeId
            ));
        }, logger, nameof(MarketMakerBotRegistrationService));
    }
}