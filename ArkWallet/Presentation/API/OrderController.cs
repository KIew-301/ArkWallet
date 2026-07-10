using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace ArkWallet.Presentation.API
{
    /// <summary>
    /// Контроллер для управления ордерами
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class OrdersController(
        IOrderQueryService orderQueryService, 
        IOrderCreationService orderCreationService,
        IOrderCancellationService orderCancellationService) : ControllerBase
    {
        /// <summary>
        /// Получение списка ордеров текущего пользователя
        /// </summary>
        /// <param name="request">Параметры фильтрации по статусам</param>
        /// <returns>Список ордеров</returns>
        /// <response code="200">Список ордеров успешно получен</response>
        /// <response code="401">Пользователь не авторизован</response>
        /// <response code="400">Ошибка получения данных</response>
        [ProducesResponseType(typeof(GetOrdersResponse), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(400)]
        [Authorize]
        [HttpGet("order")]
        public async Task<IActionResult> GetOrders([FromQuery] GetOrdersRequest request)
        {
            if (!long.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userTelegramId))
                return Unauthorized();

            var result = await orderQueryService.GetTraderOrdersAsync(
                userTelegramId, request.IncludeActive, request.IncludeFilled, request.IncludeCancelled, true);

            if (!result.TryGetData(out var data))
                return BadRequest(result.Message);

            return Ok(new GetOrdersResponse(data.ToArray()));
        }

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
