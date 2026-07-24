using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.SuggestionServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Domain.ValueObjects;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Presentation.Wizard
{
    [ExcludeFromCodeCoverage(Justification = "UI-декоратор: форматирование кнопок для Telegram-интерфейса. Не содержит бизнес-логики.")]
    internal class ButtonDecorator(IOrderQueryService orderQueryService, IPriceSuggestionService priceSuggestionService, IQuantitySuggestionService quantitySuggestionService, ITokenQueryService tokenQueryService) : IButtonDecorator
    {
        public async Task<List<QuickButton>> DecorateButtonsAsync(string stepName, List<QuickButton> baseKeyword, UserSession session)
        {
            return session.CurrentCommand switch
            {
                "/place_order" => stepName switch
                {
                    "set_token" => await DecorateTokenQuestion(),
                    "set_quantity" => await DecorateQuantityQuestion(session),
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
                    "select_token" => await DecorateTokenQuestion(),
                    _ => baseKeyword
                },
                "/get_price_history" => stepName switch
                {
                    "select_token" => await DecorateTokenQuestion(),
                    _ => baseKeyword
                },
                "/get_order_book" => stepName switch
                {
                    "select_token" => await DecorateTokenQuestion(),
                    _ => baseKeyword
                },
                _ => baseKeyword
            };
        }

        private async Task<List<QuickButton>> DecorateTokenQuestion()
        {
            var tokensResult = await tokenQueryService.GetAllActiveTokensAsync();

            if (!tokensResult.TryGetData(out var tokens))
                return [];

            return tokens.Select(token => new QuickButton { Text = token.TokenInfo.Symbol, Value = token.TokenInfo.Symbol }).ToList();
        }

        private async Task<List<QuickButton>> DecorateQuantityQuestion(UserSession session)
        {
            var direction = session.Data["set_direction"]?.ToString()?.ToLower();
            var symbol = session.Data["set_token"]?.ToString()?.ToUpper();

            if (string.IsNullOrEmpty(direction) || string.IsNullOrEmpty(symbol))
                return [];

            List<QuantitySuggestionDto> suggestions;

            if (direction == "купить")
                suggestions = await quantitySuggestionService.GetBuyQuantitySuggestionsAsync(session.Id, symbol);
            else
                suggestions = await quantitySuggestionService.GetSellQuantitySuggestionsAsync(session.Id, symbol);

            return suggestions.Select(s => new QuickButton
            {
                Text = $"{s.Quantity} шт.",
                Value = s.Quantity.ToString()
            }).ToList();
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
                    baseKeyword.Add(new() { Text = $"{item.Price:F2}{Descriptor.CurrencySymbol}", Value = item.Price.ToString("F2") });
                }
            }
            else
            {
                var priceList = await priceSuggestionService.GetSellPriceSuggestionsAsync(symbol);
                priceList = priceList.OrderByDescending(p => p.Price).ToList();

                foreach (var item in priceList)
                {
                    baseKeyword.Add(new() { Text = $"{item.Price:F2}{Descriptor.CurrencySymbol}", Value = item.Price.ToString("F2") });
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
                string answer = $"{(order.Direction == "Buy" ? "купит" : "продать")} {order.Symbol} {order.TotalQuantity} шт. по {order.Price:F2}{Descriptor.CurrencySymbol}";
                baseKeyword.Add(new() { Text = answer, Value = order.OrderId });
            }

            return baseKeyword;
        }
    }
}
