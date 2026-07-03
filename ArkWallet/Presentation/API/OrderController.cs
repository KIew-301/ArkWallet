using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ArkWallet.Presentation.API
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class OrdersController(
        IOrderQueryService orderQueryService, 
        IOrderCreationService orderCreationService,
        IOrderCancellationService orderCancellationService) : ControllerBase
    {
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
