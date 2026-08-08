using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TradeOrderServices;

using static Result<List<OrderInfo>>;

internal class OrderQueryService(
    ArkWalletDbContext dbContext,
    ILogger<OrderQueryService> logger) : IOrderQueryService
{
    public async Task<Result<List<OrderInfo>>> GetTraderOrdersAsync(
        long traderTelegramId,
        bool includeActive = true,
        bool includeFilled = true,
        bool includeCancelled = true,
        bool withTokenInfo = false)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var statuses = BuildStatuses(includeActive, includeFilled, includeCancelled);

            if (statuses.Count == 0)
                return Ok(new List<OrderInfo>());

            var orders = await dbContext.TradeOrders
                .AsNoTracking()
                .Where(o => o.TraderTelegramId == traderTelegramId && statuses.Contains(o.Status) && o.CharacterToken != null)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new
                {
                    o.Id,
                    o.Type,
                    o.Quantity,
                    o.FilledQuantity,
                    o.Price,
                    o.Status,
                    Symbol = o.CharacterToken!.Symbol,
                    TokenName = o.CharacterToken.Name,
                    IconUrl = o.CharacterToken.IconUrl,
                    CurrentPrice = o.CharacterToken.CurrentPrice
                })
                .ToListAsync();

            if (orders.Count == 0)
                return Ok(new List<OrderInfo>());

            var result = orders
                .Select(o => new OrderInfo(
                    o.Id,
                    o.Symbol,
                    o.TokenName,
                    FormatType(o.Type),
                    o.Quantity,
                    o.FilledQuantity,
                    ComputeFillPercent(o.Quantity, o.FilledQuantity),
                    o.Price,
                    o.Status.ToString(),
                    GetIconUrl(o.IconUrl, withTokenInfo),
                    GetCurrentPrice(o.CurrentPrice, withTokenInfo)))
                .ToList();

            return Ok(result);
        }, logger, nameof(OrderQueryService));
    }

    private static List<OrderStatus> BuildStatuses(bool includeActive, bool includeFilled, bool includeCancelled)
    {
        var statuses = new List<OrderStatus>();

        if (includeActive)
            statuses.Add(OrderStatus.Active);
        if (includeFilled)
            statuses.Add(OrderStatus.Filled);
        if (includeCancelled)
            statuses.Add(OrderStatus.Cancelled);

        return statuses;
    }

    private static string FormatType(OrderType type) => type == OrderType.Buy ? "Buy" : "Sell";

    private static decimal ComputeFillPercent(int quantity, int filledQuantity)
        => quantity > 0 ? (decimal)filledQuantity / quantity * 100m : 0m;

    private static string? GetIconUrl(string? iconUrl, bool withTokenInfo) => withTokenInfo ? iconUrl : null;

    private static decimal? GetCurrentPrice(decimal currentPrice, bool withTokenInfo) => withTokenInfo ? currentPrice : null;
}