using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.SuggestionServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Domain.ValueObjects;
using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Presentation.Wizard
{
    [ExcludeFromCodeCoverage(Justification = "UI-декоратор: форматирование кнопок для Telegram-интерфейса. Не содержит бизнес-логики.")]
    internal class ButtonDecorator(IOrderQueryService orderQueryService, IPriceSuggestionService priceSuggestionService, IPortfolioQueryService portfolioQueryService, ITokenQueryService tokenQueryService) : IButtonDecorator
    {
        public async Task<List<QuickButton>> DecorateButtonsAsync(string stepName, List<QuickButton> baseKeyword, UserSession session)
        {
            return session.CurrentCommand switch
            {
                "/place_order" => stepName switch
                {
                    "set_token" => await DecorateTokenQuestion(baseKeyword, session),
                    "set_price" => await DecoratePriceQuestion(baseKeyword, session),
                    _ => baseKeyword
                },
                "/cancel_order" => stepName switch
                {
                    "select_order_to_cancel" => await DecorateSelectOrderToCancel(baseKeyword, session),
                    _ => baseKeyword
                },
                "/get_token_info" => stepName switch
                {
                    "select_token" => await DecorateTokenQuestion(baseKeyword, session),
                    _ => baseKeyword
                },
                "/get_price_history" => stepName switch
                {
                    "select_token" => await DecorateTokenQuestion(baseKeyword, session),
                    _ => baseKeyword
                },
                _ => baseKeyword
            };
        }

        private async Task<List<QuickButton>> DecorateTokenQuestion(List<QuickButton> baseKeyword, UserSession session)
        {
            baseKeyword = [];
            var tokensResult = await tokenQueryService.GetAllActiveTokensAsync();

            if (!tokensResult.TryGetData(out var tokens))
                return baseKeyword;

            foreach (var token in tokens)
                baseKeyword.Add(new() { Text = token.TokenInfo.Symbol, Value = token.TokenInfo.Symbol });

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
            var result = await orderQueryService.GetTraderOrdersAsync(
                session.Id,
                includeActive: true,
                includeFilled: false,
                includeCancelled: false);

            if (!result.TryGetData(out var orders))
                return baseKeyword;

            foreach (var order in orders)
            {
                string answer = $"{(order.Direction == "Buy" ? "купит" : "продать")} {order.Symbol} {order.TotalQuantity} шт. по {order.Price:F2}";
                baseKeyword.Add(new() { Text = answer, Value = order.OrderId });
            }

            return baseKeyword;
        }
    }
}
