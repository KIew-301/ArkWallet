using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Contracts.TradeOrderServices
{
    public interface IOrderQueryService
    {
        Task<List<OrderDto>> GetActiveOrdersAsync(long traderId);
        Task<List<OrderDto>> GetOrdersAsync(long traderId);
        Task<OrderDto?> GetOrderByIdAsync(string orderId);
    }
}
