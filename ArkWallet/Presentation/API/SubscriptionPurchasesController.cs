using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace ArkWallet.Presentation.API
{
    /// <summary>
    /// Контроллер для покупки подписок
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "API-контроллер: только маршрутизация HTTP-запросов к сервисам. Не содержит бизнес-логики, тестируется интеграционно.")]
    [ApiController]
    [Route("api/v1/subscriptions")]
    public class SubscriptionPurchasesController(
        IPurchaseService purchaseService) : ControllerBase
    {
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