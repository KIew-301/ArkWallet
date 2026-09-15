using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.MarketMaker;
using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.TradingContext.Application.Services.MarketMaker;
using static Result;

internal class MarketMakerOrderService(
    ArkWalletDbContext dbContext,
    IOrderCreationService orderCreationService,
    ILogger<MarketMakerOrderService> logger,
    MarketMakerOrderEngine marketMakerOrderEngine) : IMarketMakerOrderService
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

            var domainBot = MarketMakerGridMapper.ToMarketMaker(bot);
            var marketCommand = MarketMakerOrderEngine.BuildActiveOrder(domainBot, token.CurrentPrice);
            var command = new CreateOrderCommand(marketCommand.TraderId, marketCommand.Direction, marketCommand.Symbol, marketCommand.Quantity, marketCommand.Price);

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

            var bots = await LoadActiveBotsAsync(ids);
            if (bots.Count == 0)
                return Result.Fail("Список ботов пуст");

            var tokens = await LoadTokensAsync(bots);

            var commands = BuildCommands(bots, tokens);
            if (commands.Count == 0)
                return Result.Fail("Не удалось сформировать команды ордеров");

            commands = commands.OrderBy(_ => Guid.NewGuid()).ToList();

            var result = await orderCreationService.CreateOrdersAsync(commands);

            if (!result.IsSuccess)
                return Result.Fail($"Не удалось создать ордера: {result.Message}");

            return Ok();
        }, logger, nameof(MarketMakerOrderService));
    }

    /// <summary>Loads active bots by identifiers, first from the change tracker, then from the database.</summary>
    private async Task<List<MarketMakerBot>> LoadActiveBotsAsync(long[] ids)
    {
        var bots = dbContext.MarketMakerBots.Local
            .Where(b => ids.Contains(b.Id) && b.IsActive)
            .ToList();

        var missingIds = ids.Except(bots.Select(b => b.Id)).ToArray();
        if (missingIds.Length > 0)
        {
            var fromDb = await dbContext.MarketMakerBots
                .Where(b => missingIds.Contains(b.Id) && b.IsActive)
                .ToListAsync();
            bots.AddRange(fromDb);
        }

        return bots;
    }

    /// <summary>Loads tokens for the bot symbols, first from the change tracker, then from the database.</summary>
    private async Task<Dictionary<string, CharacterToken>> LoadTokensAsync(List<MarketMakerBot> bots)
    {
        var symbols = bots.Select(b => b.Symbol).Distinct().ToArray();

        var tokens = dbContext.CharacterTokens.Local
            .Where(t => symbols.Contains(t.Symbol))
            .ToDictionary(t => t.Symbol);

        var missingSymbols = symbols.Except(tokens.Keys).ToArray();
        if (missingSymbols.Length > 0)
        {
            var fromDb = await dbContext.CharacterTokens
                .Where(t => missingSymbols.Contains(t.Symbol))
                .ToListAsync();
            foreach (var t in fromDb)
                tokens.TryAdd(t.Symbol, t);
        }

        return tokens;
    }

    private List<CreateOrderCommand> BuildCommands(List<MarketMakerBot> bots, Dictionary<string, CharacterToken> tokens)
    {
        var commands = new List<CreateOrderCommand>(bots.Count);
        foreach (var bot in bots)
        {
            if (!tokens.TryGetValue(bot.Symbol, out var token))
            {
                logger.LogWarning("Токен {Symbol} не найден", bot.Symbol);
                continue;
            }
            var domainBot = MarketMakerGridMapper.ToMarketMaker(bot);
            var marketCommand = MarketMakerOrderEngine.BuildActiveOrder(domainBot, token.CurrentPrice);
            commands.Add(new CreateOrderCommand(marketCommand.TraderId, marketCommand.Direction, marketCommand.Symbol, marketCommand.Quantity, marketCommand.Price));
        }
        return commands;
    }
}
