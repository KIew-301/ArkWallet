using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.ValueObjects;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Presentation.Wizard
{
    [ExcludeFromCodeCoverage(Justification = "UI-декоратор: форматирование текста вопросов для Telegram-интерфейса. Не содержит бизнес-логики.")]
    internal class QuestionDecorator(ITokenQueryService tokenQueryService, ITraderQueryService traderQueryService, IPortfolioQueryService portfolioQueryService) : IQuestionDecorator
    {
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
            var tokensResult = await tokenQueryService.GetAllActiveTokensAsync();

            if (!tokensResult.TryGetData(out var tokens) || tokens.Count == 0)
                return $"{baseQuestion}\n\n💎 Токенов на бирже нет\n";

            var symbols = string.Join(" ", tokens.Select(t => t.TokenInfo.Symbol));
            return $"{baseQuestion}\n\n💎 Доступные токены: {symbols}\n";
        }

        private async Task<string> DecorateQuantityQuestion(string baseQuestion, UserSession session)
        {
            var symbol = session.Data["set_token"]?.ToString();
            var direction = session.Data["set_direction"]?.ToString()?.ToLower();
            var tokenResult = await tokenQueryService.GetTokenInfoAsync(symbol);
            var currentPrice = tokenResult.TryGetData(out var tokenData) ? tokenData.CurrentPrice : 0m;

            if (direction == "купить")
            {
                var profileResult = await traderQueryService.GetTraderProfileAsync(session.Id);
                var balance = profileResult.TryGetData(out var profile) ? profile.Balance : 0m;

                return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                       $"💰 Текущая цена: {currentPrice:F2}\n" +
                       $"💳 Общий баланс: {balance:F2}\n";
            }
            else
            {
                var portfolioQueryResult = await portfolioQueryService.GetTokenBalanceAsync(session.Id, symbol);

                if (portfolioQueryResult.TryGetData(out var data))
                    return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                           $"💰 Текущая цена: {currentPrice:F2}\n" +
                           $"📦 Всего в портфеле: {data.Quantity} шт\n";
                else
                    return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                       $"💰 Текущая цена: {currentPrice:F2}\n" +
                       $"📦 Всего в портфеле: 0 шт\n";
            }
        }

        private async Task<string> DecoratePriceQuestion(string baseQuestion, UserSession session)
        {
            var symbol = session.Data["set_token"]?.ToString();

            var tokenResult = await tokenQueryService.GetTokenInfoAsync(symbol);
            var currentPrice = tokenResult.TryGetData(out var tokenData) ? tokenData.CurrentPrice : 0m;

            return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                   $"📊 Текущая цена: {currentPrice:F2}";
        }
    }
}
