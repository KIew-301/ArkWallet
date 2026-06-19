using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Application.Services.PortfolioServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Tests
{
    internal class OrderCancellationServiceTest
    {
        private record TestTrader(long TelegramId, string Name);
        private record TestToken(string Symbol, string Name, CharacterRarity Rarity, int TotalSupply, int CurrentPrice, bool IsActive);
        private record TestOrder(long TraderId, string Direction, string Symbol, int Quantity, decimal Price);
        private record TestPortfolio(long TraderId, string Symbol, int Quantity);

        [Fact]
        public async Task CancelOrderAsync_CancelLongOrder_ReturnSuccess()
        {
            var traderRecord = new TestTrader(101, "First");
            var tokenRecord = new TestToken("ZZZ", "Тест-валюта", CharacterRarity.FourStar, 1000, 10000, true);
            var orderRecord = new TestOrder(traderRecord.TelegramId, "купить", tokenRecord.Symbol, 5, 100);
            var startBalance = 1000;

            var db = DbTest.CreateDbContext();
            db.Database.EnsureCreated();

            var traderRegistrationService = new TraderRegistrationService(db);
            var tokenCreationService = new TokenCreationService(db);
            var orderCancellationService = new OrderCancelService(db);
            var portfolioUpdatingService = new PortfolioUpdatingService(db);
            var tradingEngine = new TradingEngine();
            var tradingService = new OrderCreationService(db, tradingEngine, null);

            await traderRegistrationService.RegisterTraderAsync(traderRecord.TelegramId, traderRecord.Name);
            await tokenCreationService.CreateTokenAsync(new CreateTokenCommand(
                tokenRecord.Symbol, tokenRecord.Name, tokenRecord.Rarity, tokenRecord.TotalSupply, tokenRecord.CurrentPrice, tokenRecord.IsActive));

            var result1 = await tradingService.CreateOrderAsync(new CreateOrderCommand(
                orderRecord.TraderId, orderRecord.Direction, orderRecord.Symbol, orderRecord.Quantity, orderRecord.Price));

            var result2 = await orderCancellationService.CancelOrderAsync(orderRecord.TraderId, result1.Order.Id);

            var trader = await db.Traders
                .FirstOrDefaultAsync(t => t.TelegramId == traderRecord.TelegramId);

            Assert.True(result2.IsSuccess);
            Assert.Equal(1000, trader.Balance);
        }
    }
}
