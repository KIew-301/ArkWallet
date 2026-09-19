using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionServices;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace ArkWallet.Presentation.API
{
    /// <summary>
    /// Контроллер для управления подписками
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "API-контроллер: только маршрутизация HTTP-запросов к сервисам. Не содержит бизнес-логики, тестируется интеграционно.")]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class SubscriptionsController(
        ISubscriptionQueryService subscriptionQueryService,
        IPurchaseService purchaseService) : ControllerBase
    {
        /// <summary>
        /// Получение списка доступных подписок
        /// </summary>
        /// <returns>Список подписок</returns>
        /// <response code="200">Список подписок успешно получен</response>
        /// <response code="401">Пользователь не авторизован</response>
        /// <response code="400">Ошибка получения данных</response>
        [ProducesResponseType(typeof(SubscriptionsResponse), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(400)]
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetSubscriptions()
        {
            if (!long.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out _))
                return Unauthorized();

            var result = await subscriptionQueryService.GetAllAsync();

            if (!result.TryGetData(out var subscriptions))
                return BadRequest(result.Message);

            var response = subscriptions
                .Select(s => new SubscriptionResponse(
                    s.Id, s.Name, s.Level,
                    s.PriceWeekRubles, s.PriceMonthRubles, s.PriceYearRubles,
                    s.MaxOrders, s.MaxMiningMachines, s.DurationMinutes))
                .ToArray();

            return Ok(new SubscriptionsResponse(response));
        }

        /// <summary>
        /// Покупка подписки
        /// </summary>
        /// <param name="request">Запрос на покупку</param>
        /// <returns>Результат покупки</returns>
        /// <response code="200">Подписка успешно приобретена</response>
        /// <response code="401">Пользователь не авторизован</response>
        /// <response code="400">Ошибка покупки подписки</response>
        [ProducesResponseType(typeof(PurchaseSubscriptionResponse), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(400)]
        [Authorize]
        [HttpPost("purchase")]
        public async Task<IActionResult> Purchase([FromBody] PurchaseSubscriptionRequest request)
        {
            if (!long.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userTelegramId))
                return Unauthorized();

            if (!SubscriptionPeriodExtensions.TryParse(request.Period, out var period))
                return BadRequest("Некорректный период. Используйте: неделя/месяц/год (week/month/year).");

            var result = await purchaseService.PurchaseAsync(userTelegramId, request.SubscriptionId, period);

            return result.Success
                ? Ok(new PurchaseSubscriptionResponse(true, result.Message, result.TransactionId, result.ExpiresAtUtc))
                : BadRequest(new PurchaseSubscriptionResponse(false, result.Message, null, null));
        }
    }
}
