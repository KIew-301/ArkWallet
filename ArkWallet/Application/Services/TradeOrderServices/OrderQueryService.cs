using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Services.TradeOrderServices
{
    internal class OrderQueryService : IOrderQueryService
    {
        readonly IUnitOfWork _unitOfWork;

        public OrderQueryService(
            IUnitOfWork unitOfWork
            )
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<OrderDto>> GetActiveOrdersAsync(long traderId)
        {
            var orders = await _unitOfWork.Orders.GetPendingByTraderAsync(traderId);
            return orders.Select(OrderDto.FromEntity).ToList();
        }

        public async Task<OrderDto?> GetOrderByIdAsync(string orderId)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            return OrderDto.FromEntity(order);
        }

        public async Task<List<OrderDto>> GetOrdersAsync(long traderId)
        {
            var orders = await _unitOfWork.Orders.GetByTraderAsync(traderId);
            return orders.Select(OrderDto.FromEntity).ToList();
        }
    }
}
