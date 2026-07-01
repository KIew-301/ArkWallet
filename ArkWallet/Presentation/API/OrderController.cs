using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ArkWallet.Presentation.API
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class OrdersController(IOrderQueryService orderQueryService) : ControllerBase
    {
        [Authorize]
        [HttpGet("order")]
        public async Task<IActionResult> Order([FromBody] GetOrdersRequest request)
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
