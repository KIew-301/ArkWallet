using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.MarketMaker;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.TradingContext.Application.Services.MarketMaker;

using static Result<MarketMakerBotRegistrationData>;

internal class MarketMakerBotRegistrationService(
    ArkWalletDbContext dbContext,
    ILogger<MarketMakerBotRegistrationService> logger,
    TimeProvider? timeProvider = null) : IMarketMakerBotRegistrationService
{
    public async Task<Result<MarketMakerBotRegistrationData>> RegisterBotAsync(string symbol, BotRole botRole, decimal initialPower = 50)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync();

            if (string.IsNullOrWhiteSpace(symbol))
                return Fail("Символ токена не может быть пустым");

            if (initialPower <= 0)
                return Fail("Начальная мощность должна быть больше нуля");

            var trader = Trader.Create($"MarketMakerBot_{symbol}", isBot: true);
            await dbContext.Traders.AddAsync(trader);
            await dbContext.SaveChangesAsync();

            var bot = MarketMakerBotRecord.Create(trader.Id, symbol, botRole, initialPower, timeProvider);

            await dbContext.MarketMakerBots.AddAsync(bot);
            await dbContext.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new MarketMakerBotRegistrationData(
                BotId: bot.Id,
                TraderId: trader.Id
            ));
        }, logger, nameof(MarketMakerBotRegistrationService));
    }

    public async Task<Result<long>> CreateDedicatedTraderAsync(string symbol, BotRole role)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var trader = Trader.Create($"MarketMakerBot_{symbol}_{role}", isBot: true);
            await dbContext.Traders.AddAsync(trader);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return Result<long>.Ok(trader.Id);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
