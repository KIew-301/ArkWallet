using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.CharacterTokenServices;
using ArkWallet.Core.TradingContext.Application.Contracts.TradeServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.TradingContext.Application.Services.TradeServices;

using static Result<List<TradeInfo>>;

internal class TradeQueryService(
    ArkWalletDbContext dbContext,
    ILogger<TradeQueryService> logger) : ITradeQueryService
{
    /// <summary>Плоская проекция сделки: значения материализуются в SQL, TradeInfo собирается в памяти.</summary>
    private sealed record TradeRow(
        long BuyerId,
        decimal Price,
        decimal Quantity,
        DateTime ExecutedAt,
        string Symbol,
        string Name,
        decimal CurrentPrice,
        string IconUrl,
        string ImageUrl);

    private IQueryable<Trade> BaseTradesQuery(long traderTelegramId) =>
        dbContext.Trades
            .AsNoTracking()
            .Where(t => (t.BuyerId == traderTelegramId || t.SellerId == traderTelegramId) && t.CharacterToken != null);

    /// <summary>Проекция сделок, отсортированных по дате исполнения (новые первыми). Сортировка выполняется до проекции — EF не транслирует OrderBy по уже спроецированному типу.</summary>
    private IQueryable<TradeRow> BuildTradesQuery(long traderTelegramId) =>
        BaseTradesQuery(traderTelegramId)
            .OrderByDescending(t => t.ExecutedAt)
            .Select(t => new TradeRow(
                t.BuyerId,
                t.Price,
                t.Quantity,
                t.ExecutedAt,
                t.CharacterToken.Symbol,
                t.CharacterToken.Name,
                t.CharacterToken.CurrentPrice,
                t.CharacterToken.IconUrl,
                t.CharacterToken.ImageUrl));

    /// <summary>Определяет роль трейдера в сделке и считает прибыль: покупатель тратит (минус), продавец получает (плюс).</summary>
    private static TradeInfo MapToTradeInfo(TradeRow row, long traderTelegramId)
    {
        var isBuyer = row.BuyerId == traderTelegramId;
        return new TradeInfo(
            isBuyer ? "Buyer" : "Seller",
            row.Price,
            row.Quantity,
            isBuyer ? -(row.Quantity * row.Price) : row.Quantity * row.Price,
            row.ExecutedAt,
            new TokenInfo(row.Symbol, row.Name, row.CurrentPrice, row.IconUrl, row.ImageUrl));
    }

    public async Task<Result<List<TradeInfo>>> GetTraderTradesAsync(long traderTelegramId, bool withTokenInfo = false)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var rows = await BuildTradesQuery(traderTelegramId).ToListAsync();

            return Ok(rows.Select(r => MapToTradeInfo(r, traderTelegramId)).ToList());
        }, logger, nameof(TradeQueryService));
    }

    public async Task<Result<PagedResult<TradeInfo>>> GetTraderTradesPageAsync(long traderTelegramId, int page, int pageSize, bool withTokenInfo = false)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var totalCount = await BaseTradesQuery(traderTelegramId).CountAsync();
            var rows = await BuildTradesQuery(traderTelegramId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = rows.Select(r => MapToTradeInfo(r, traderTelegramId)).ToList();
            return Result<PagedResult<TradeInfo>>.Ok(new PagedResult<TradeInfo>(items, page, pageSize, totalCount));
        }, logger, nameof(TradeQueryService));
    }
}