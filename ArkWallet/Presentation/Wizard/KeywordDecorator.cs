using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.SuggestionServices;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Presentation.Wizard
{
    internal class ButtonDecorator(ArkWalletDbContext dbContext, IPriceSuggestionService priceSuggestionService, IPortfolioQueryService portfolioQueryService) : IButtonDecorator
    {
        public async Task<List<QuickButton>> DecorateButtonsAsync(string stepName, List<QuickButton> baseKeyword, UserSession session)
        {
            return session.CurrentCommand switch
            {
                "/placeorder" => stepName switch
                {
                    "set_token" => await DecorateTokenQuestion(baseKeyword, session),
                    "set_price" => await DecoratePriceQuestion(baseKeyword, session),
                    _ => baseKeyword
                },
                "/cancelorder" => stepName switch
                {
                    "select_order_to_cancel" => await DecorateSelectOrderToCancel(baseKeyword, session),
                    _ => baseKeyword
                },
                _ => baseKeyword
            };
        }

        private async Task<List<QuickButton>> DecorateTokenQuestion(List<QuickButton> baseKeyword, UserSession session)
        {
            baseKeyword = [];
            var portfolioQueryResult = await portfolioQueryService.GetTraderTokensAsync(session.Id);

            if (!portfolioQueryResult.TryGetData(out var portfolioItems))
                return baseKeyword;

            foreach (var token in portfolioItems)
                baseKeyword.Add(new() { Text = token.Symbol, Value = token.Symbol });

            return baseKeyword;
        }

        private async Task<List<QuickButton>> DecoratePriceQuestion(List<QuickButton> baseKeyword, UserSession session)
        {
            baseKeyword = [];

            string direction = session.Data["set_direction"].ToString().ToLower();
            int quantity = (int)session.Data["set_quantity"];
            string symbol = session.Data["set_token"].ToString().ToUpper();

            if (direction == "купить")
            {
                var priceList = await priceSuggestionService.GetBuyPriceSuggestionsAsync(session.Id, symbol, quantity);
                priceList = priceList.OrderBy(p => p.Price).ToList();

                foreach (var item in priceList)
                {
                    baseKeyword.Add(new() { Text = item.Price.ToString("F2"), Value = item.Price.ToString("F2") });
                }
            }
            else
            {
                var priceList = await priceSuggestionService.GetSellPriceSuggestionsAsync(symbol);
                priceList = priceList.OrderByDescending(p => p.Price).ToList();

                foreach (var item in priceList)
                {
                    baseKeyword.Add(new() { Text = item.Price.ToString("F2"), Value = item.Price.ToString("F2") });
                }
            }

            return baseKeyword;
        }

        private async Task<List<QuickButton>> DecorateSelectOrderToCancel(List<QuickButton> baseKeyword, UserSession session)
        {
            baseKeyword = [];
            var orders = await dbContext.TradeOrders
                .Where(o => o.TraderTelegramId == session.Id && o.Status == OrderStatus.Active)
                .ToArrayAsync();

            foreach (var order in orders)
            {
                string answer = $"" +
                    $"{(order.Type == OrderType.Buy ? "купит" : "продать")} " +
                    $"{order.CharacterTokenId} " +
                    $"{order.Quantity} шт. " +
                    $"по {order.Price:F2}";

                baseKeyword.Add(new() { Text = answer, Value = order.Id });
            }

            return baseKeyword;
        }
    }
}
