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
        private const string Buy = "купить";
        private const string Sell = "продать";
        private const string DefaultAction = "выбрать";

        public async Task<string> DecorateQuestionAsync(string stepName, string baseQuestion, UserSession session)
        {
            return stepName switch
            {
                "set_quantity" => await DecorateQuantityQuestion(session),
                "set_price" => await DecoratePriceQuestion(session),
                "set_token" => await DecorateTokenQuestion(session),
                _ => baseQuestion
            };
        }

        private async Task<string> DecorateTokenQuestion(UserSession session)
        {
            var actionWord = GetActionWord(session, DefaultAction);

            var tokensResult = await tokenQueryService.GetAllActiveTokensAsync();

            if (!tokensResult.TryGetData(out var tokens) || tokens.Count == 0)
                return $"Какой токен вы хотите {actionWord}? (выберите или напишите)\n\n💎 Токенов на бирже нет\n";

            var symbols = string.Join(" ", tokens.Select(t => t.TokenInfo.Symbol));
            return $"Какой токен вы хотите {actionWord}? (выберите или напишите)\n\n💎 Доступные токены: {symbols}\n";
        }

        private async Task<string> DecorateQuantityQuestion(UserSession session)
        {
            var symbol = session.Data["set_token"]?.ToString();
            var direction = session.Data["set_direction"]?.ToString()?.ToLower();
            if (string.IsNullOrEmpty(symbol))
                return "Выберите токен.";

            var actionWord = GetActionWord(session, DefaultAction);

            var tokenResult = await tokenQueryService.GetTokenInfoAsync(symbol);
            var currentPrice = tokenResult.TryGetData(out var tokenData) ? tokenData.CurrentPrice : 0m;

            if (direction == Buy)
            {
                var profileResult = await traderQueryService.GetTraderProfileAsync(session.Id);
                var balance = profileResult.TryGetData(out var profile) ? profile.Balance : 0m;

                return $"Сколько вы хотите {actionWord}? (выберите или напишите)\n\n💎 Токен: {symbol}\n" +
                       $"💰 Текущая цена: {currentPrice:F2}{Descriptor.CurrencySymbol}\n" +
                       $"💳 Общий баланс: {balance:F2}{Descriptor.CurrencySymbol}\n";
            }
            else
            {
                var portfolioQueryResult = await portfolioQueryService.GetTokenBalanceAsync(session.Id, symbol);

                if (portfolioQueryResult.TryGetData(out var data))
                    return $"Сколько вы хотите {actionWord}? (выберите или напишите)\n\n💎 Токен: {symbol}\n" +
                           $"💰 Текущая цена: {currentPrice:F2}{Descriptor.CurrencySymbol}\n" +
                           $"📦 Всего в портфеле: {data.Quantity} шт\n";
                else
                    return $"Сколько вы хотите {actionWord}? (выберите или напишите)\n\n💎 Токен: {symbol}\n" +
                       $"💰 Текущая цена: {currentPrice:F2}{Descriptor.CurrencySymbol}\n" +
                       $"📦 Всего в портфеле: 0 шт\n";
            }
        }

        private async Task<string> DecoratePriceQuestion(UserSession session)
        {
            var symbol = session.Data["set_token"]?.ToString();
            if (string.IsNullOrEmpty(symbol))
                return "Выберите токен.";

            var actionWord = GetActionWord(session, "исполнить");

            var tokenResult = await tokenQueryService.GetTokenInfoAsync(symbol);
            var currentPrice = tokenResult.TryGetData(out var tokenData) ? tokenData.CurrentPrice : 0m;

            return $"По какой цене вы хотите {actionWord}? (выберите или напишите свою)\n\n💎 Токен: {symbol}\n" +
                   $"📊 Текущая цена: {currentPrice:F2}{Descriptor.CurrencySymbol}";
        }

        private static string GetActionWord(UserSession session, string defaultValue)
        {
            var direction = session.Data["set_direction"]?.ToString()?.ToLower();
            if (direction == Buy)
                return Buy;
            if (direction == Sell)
                return Sell;
            return defaultValue;
        }
    }
}
