using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ArkWallet.Presentation.API
{
    /// <summary>
    /// Контроллер для отмены ордеров
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/v1/orders")]
    public class OrdersCancellationController(IOrderCancellationService orderCancellationService) : ControllerBase
    {
        /// <summary>
        /// Отмена конкретного ордера
        /// </summary>
        /// <param name="orderId">ID ордера для отмены</param>
        /// <returns>Сообщение об успешной отмене</returns>
        /// <response code="200">Ордер успешно отменён</response>
        /// <response code="401">Пользователь не авторизован</response>
        /// <response code="400">Ошибка отмены ордера</response>
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(400)]
        [Authorize]
        [HttpDelete("order/{orderId}")]
        public async Task<IActionResult> CancelOrder(string orderId)
        {
            if (!long.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userTelegramId))
                return Unauthorized();

            var result = await orderCancellationService.CancelOrderAsync(userTelegramId, orderId);

            if (!result.IsSuccess)
                return BadRequest(result.Message);

            return Ok(new { Message = "Ордер успешно отменён" });
        }

        /// <summary>
        /// Отмена всех активных ордеров текущего пользователя
        /// </summary>
        /// <returns>Сообщение об успешной отмене</returns>
        /// <response code="200">Все ордера успешно отменены</response>
        /// <response code="401">Пользователь не авторизован</response>
        /// <response code="400">Ошибка отмены ордеров</response>
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(400)]
        [Authorize]
        [HttpDelete("orders")]
        public async Task<IActionResult> CancelAllOrders()
        {
            if (!long.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userTelegramId))
                return Unauthorized();

            var result = await orderCancellationService.CancelAllOrderAsync(userTelegramId);

            if (!result.IsSuccess)
                return BadRequest(result.Message);

            return Ok(new { Message = "Все ордера успешно отменены" });
        }
    }
}
