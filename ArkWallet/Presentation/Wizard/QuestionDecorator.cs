using ArkWallet.Application.Contracts;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using Newtonsoft.Json.Linq;

namespace ArkWallet.Presentation.Wizard
{
    internal class QuestionDecorator : IQuestionDecorator
    {
        private readonly IOrderQueryService _orderQueryService;
        private readonly IPortfolioQueryService _portfolioQueryService;
        private readonly ITokenQueryService _tokenQueryService;
        private readonly ITraderQueryService _traderQueryService;

        public QuestionDecorator (
            IOrderQueryService orderQueryService,
            IPortfolioQueryService portfolioQueryService,
            ITokenQueryService tokenQueryService,
            ITraderQueryService traderQueryService
            )
        {
            _orderQueryService = orderQueryService;
            _portfolioQueryService = portfolioQueryService;
            _tokenQueryService = tokenQueryService;
            _traderQueryService = traderQueryService;
        }

        public async Task<string> DecorateQuestionAsync(string stepName, string baseQuestion, UserSession session)
        {
            return stepName switch
            {
                "set_quantity" => await DecorateQuantityQuestion(baseQuestion, session),
                "set_price" => await DecoratePriceQuestion(baseQuestion, session),
                "set_token" => await DecorateTokenQuestion(baseQuestion, session),
                _ => baseQuestion
            };
        }

        private async Task<string> DecorateTokenQuestion(string baseQuestion, UserSession session)
        {
            var tokens = await _portfolioQueryService.GetTraderTokensAsync(session.Id);
            return $"{baseQuestion}\n\n💎 У вас есть: {string.Join(" ", tokens.Select(t => t.Symbol))}\n";
        }

        private async Task<string> DecorateQuantityQuestion(string baseQuestion, UserSession session)
        {
            var symbol = session.Data["set_token"]?.ToString();
            var direction = session.Data["set_direction"]?.ToString()?.ToLower();
            var token = await _tokenQueryService.GetTokenInfoAsync(symbol);

            if (direction == "купить")
            {
                var balance = await _traderQueryService.GetTraderBalanceAsync(session.Id);
                var availableBalance = await _traderQueryService.GetTraderAvailableBalanceAsync(session.Id);

                return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                       $"💰 Текущая цена: {token.CurrentPrice:F2}\n" +
                       $"💳 Общий баланс: {balance:F2}\n" +
                       $"🔄 Доступно: {availableBalance:F2}";
            }
            else
            {
                var tokenBalance = await _portfolioQueryService.GetTokenBalanceAsync(session.Id, symbol);
                var availableTokenBalance = await _portfolioQueryService.GetAvailableTokenBalanceAsync(session.Id, symbol);

                return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                       $"💰 Текущая цена: {token.CurrentPrice:F2}\n" +
                       $"📦 Всего в портфеле: {tokenBalance.Quantity} шт\n" +
                       $"🔄 Доступно для продажи: {availableTokenBalance.Quantity} шт\n";
            }
        }

        private async Task<string> DecoratePriceQuestion(string baseQuestion, UserSession session)
        {
            var symbol = session.Data["set_token"]?.ToString();

            var token = await _tokenQueryService.GetTokenInfoAsync(symbol);
            return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                   $"📊 Текущая цена: {token.CurrentPrice:F2}";
        }
    }
}
