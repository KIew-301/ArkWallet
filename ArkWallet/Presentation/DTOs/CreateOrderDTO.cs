using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Presentation.DTOs
{
    public record CreateOrderRequest(string Symbol, decimal Price, decimal Quantity, string Direction);
    public record CreateOrderResponse(bool IsSuccess, string Message);
}