using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Services.Other;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Presentation.Wizard
{
    [ExcludeFromCodeCoverage]
    internal class QuestionDecorator(ArkWalletDbContext dbContext, IPortfolioQueryService portfolioQueryService, ReserveCalculationService reserveCalculationService) : IQuestionDecorator
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
            var portfolioQueryResult = await portfolioQueryService.GetTraderTokensAsync(session.Id);

            if (!portfolioQueryResult.TryGetData(out var portfolioItems))
                return $"{baseQuestion}\n\n💎 У вас есть: 0\n";
            else
                return $"{baseQuestion}\n\n💎 У вас есть: {string.Join(" ", portfolioItems.Select(t => t.TokenInfo?.Symbol ?? "???"))}\n";
        }

        private async Task<string> DecorateQuantityQuestion(string baseQuestion, UserSession session)
        {
            var symbol = session.Data["set_token"]?.ToString();
            var direction = session.Data["set_direction"]?.ToString()?.ToLower();
            var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == symbol);

            if (direction == "купить")
            {
                var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == session.Id);
                var balance = trader.Balance;

                return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                       $"💰 Текущая цена: {token.CurrentPrice:F2}\n" +
                       $"💳 Общий баланс: {balance:F2}\n";
            }
            else
            {
                var portfolioQueryResult = await portfolioQueryService.GetTokenBalanceAsync(session.Id, symbol);


                if (portfolioQueryResult.TryGetData(out var data))
                    return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                           $"💰 Текущая цена: {token.CurrentPrice:F2}\n" +
                           $"📦 Всего в портфеле: {data.Quantity} шт\n";
                else
                    return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                       $"💰 Текущая цена: {token.CurrentPrice:F2}\n" +
                       $"📦 Всего в портфеле: 0 шт\n";
            }
        }

        private async Task<string> DecoratePriceQuestion(string baseQuestion, UserSession session)
        {
            var symbol = session.Data["set_token"]?.ToString();

            var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == symbol);
            return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                   $"📊 Текущая цена: {token.CurrentPrice:F2}";
        }
    }
}
