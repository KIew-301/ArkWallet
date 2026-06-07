using ArkWallet.Application.Contracts.TradeOrderServices;
using Microsoft.AspNetCore.Mvc;

namespace ArkWallet.Presentation.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController(IOrderValidationService orderValidationService) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderCommand request)
        {
            return Ok("Заказ успешно создан!"); // Заглушка для демонстрации
        }
    }
}
