using ArkWallet.ValueObjects;
using ArkWallet.Repositories;
using ArkWallet.Contracts;

namespace ArkWallet.Infrastructure.Wizard
{
    internal class QuestionDecorator
    {
        private readonly IUnitOfWork _uow;

        public QuestionDecorator (IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<string> Decorate(string stepName, string baseQuestion, UserSession session)
        {
            return stepName switch
            {
                "set_quantity" => await DecorateQuantityQuestion(baseQuestion, session),
                "set_price" => await DecoratePriceQuestion(baseQuestion, session),
                _ => baseQuestion
            };
        }

        private async Task<string> DecorateQuantityQuestion(string baseQuestion, UserSession session)
        {
            var symbol = session.Data["set_token"]?.ToString();
            var direction = session.Data["set_direction"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(symbol)) return baseQuestion;

            var token = await _uow.Tokens.GetByIdAsync(symbol);
            var trader = await _uow.Traders.GetByIdAsync(session.Id);

            if (direction == "купить")
            {
                return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                       $"💰 Цена: {token.CurrentPrice:F2}\n" +
                       $"💵 Доступно: {trader.Balance / token.CurrentPrice:F1} шт";
            }
            else
            {
                var item = await _uow.Portfolios.GetByTraderAndSymbolAsync(session.Id, symbol);
                return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                       $"📦 В портфеле: {item.Quantity} шт";
            }
        }

        private async Task<string> DecoratePriceQuestion(string baseQuestion, UserSession session)
        {
            var symbol = session.Data["set_token"]?.ToString();
            if (string.IsNullOrEmpty(symbol)) return baseQuestion;

            var token = await _uow.Tokens.GetByIdAsync(symbol);
            return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                   $"📊 Текущая цена: {token.CurrentPrice:F2}";
        }
    }
}
