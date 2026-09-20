using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ArkWallet.Presentation.API
{
    /// <summary>
    /// Контроллер для создания ордеров
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/v1/orders")]
    public class OrdersCreationController(IOrderCreationService orderCreationService) : ControllerBase
    {
        /// <summary>
        /// Создание нового ордера
        /// </summary>
        /// <param name="request">Параметры ордера</param>
        /// <returns>ID созданного ордера и статус исполнения</returns>
        /// <response code="200">Ордер успешно создан</response>
        /// <response code="401">Пользователь не авторизован</response>
        /// <response code="400">Ошибка создания ордера</response>
        [ProducesResponseType(typeof(CreateOrderResponse), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(400)]
        [Authorize]
        [HttpPost("order")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (!long.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userTelegramId))
                return Unauthorized();

            var command = new CreateOrderCommand(
                userTelegramId, request.Direction, request.Symbol, request.Quantity, request.Price);

            var result = await orderCreationService.CreateOrderAsync(command);

            if (!result.TryGetData(out var data))
                return BadRequest(result.Message);

            return Ok(new CreateOrderResponse(data.Order.Id, data.IsFilled));
        }
    }
}
