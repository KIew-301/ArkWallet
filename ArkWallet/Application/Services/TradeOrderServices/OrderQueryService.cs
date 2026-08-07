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
            var statuses = new List<OrderStatus>();

            if (includeActive)
                statuses.Add(OrderStatus.Active);
            if (includeFilled)
                statuses.Add(OrderStatus.Filled);
            if (includeCancelled)
                statuses.Add(OrderStatus.Cancelled);

            if (!statuses.Any())
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
                    o.Type == OrderType.Buy ? "Buy" : "Sell",
                    o.Quantity,
                    o.FilledQuantity,
                    o.Quantity > 0 ? (decimal)o.FilledQuantity / o.Quantity * 100m : 0m,
                    o.Price,
                    o.Status.ToString(),
                    withTokenInfo ? o.IconUrl : null,
                    withTokenInfo ? o.CurrentPrice : null))
                .ToList();

            return Ok(result);
        }, logger, nameof(OrderQueryService));
    }
}