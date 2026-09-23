using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ArkWallet.Presentation.API
{
    /// <summary>
    /// Контроллер для чтения информации об ордерах пользователя
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/v1/orders")]
    public class OrdersQueryController(IOrderQueryService orderQueryService) : ControllerBase
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
    }
}
