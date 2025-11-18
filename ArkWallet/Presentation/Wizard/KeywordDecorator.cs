using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.SuggestionServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Presentation.Wizard
{
    internal class ButtonDecorator : IButtonDecorator
    {
        private readonly IPriceSuggestionService _priceSuggestionService;
        private readonly IPortfolioQueryService _portfolioQueryService;
        private readonly IOrderQueryService _orderQueryService;

        public ButtonDecorator(
            IPriceSuggestionService priceSuggestionService,
            IPortfolioQueryService portfolioQueryService,
            IOrderQueryService orderQueryService
            )
        {
            _priceSuggestionService = priceSuggestionService;
            _portfolioQueryService = portfolioQueryService;
            _orderQueryService = orderQueryService;
        }

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
            var tokens = await _portfolioQueryService.GetTraderTokensAsync(session.Id);

            foreach (var token in tokens)
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
                var priceList = await _priceSuggestionService.GetBuyPriceSuggestionsAsync(session.Id, symbol, quantity);
                priceList = priceList.OrderBy(p => p.Price).ToList();

                foreach (var item in priceList)
                {
                    baseKeyword.Add(new() { Text = item.Price.ToString("F2"), Value = item.Price.ToString("F2") });
                }
            }
            else
            {
                var priceList = await _priceSuggestionService.GetSellPriceSuggestionsAsync(session.Id, symbol, quantity);
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
            var orders = await _orderQueryService.GetActiveOrdersAsync(session.Id);

            foreach (var order in orders)
            {
                string answer = $"" +
                    $"{order.Direction} " +
                    $"{order.Symbol} " +
                    $"{order.Quantity} шт. " +
                    $"по {order.Price:F2}";

                baseKeyword.Add(new() { Text = answer, Value = order.Id });
            }

            return baseKeyword;
        }
    }
}
