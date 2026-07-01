using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Domain.Exceptions;
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
        bool includeCancelled = true)
    {
        try
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
                .Where(o => o.TraderTelegramId == traderTelegramId && statuses.Contains(o.Status))
                .Include(o => o.CharacterToken)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            if (!orders.Any())
                return Ok(new List<OrderInfo>());

            var result = orders
                .Where(o => o.CharacterToken != null)
                .Select(o => OrderInfo.FromEntity(o, o.CharacterToken!))
                .ToList();

            return Ok(result);
        }
        catch (DomainException ex)
        {
            return Fail($"Ошибка бизнес-логики: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OrderQueryService:GetTraderOrdersAsync Error");
            return Fail($"Внутренняя ошибка сервера: {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}