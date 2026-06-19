using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Services.Other;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Presentation.Wizard
{
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
            var tokens = await portfolioQueryService.GetTraderTokensAsync(session.Id);
            return $"{baseQuestion}\n\n💎 У вас есть: {string.Join(" ", tokens.Select(t => t.Symbol))}\n";
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
                var tokenBalance = await portfolioQueryService.GetTokenBalanceAsync(session.Id, symbol);

                return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                       $"💰 Текущая цена: {token.CurrentPrice:F2}\n" +
                       $"📦 Всего в портфеле: {tokenBalance.Quantity} шт\n";
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
