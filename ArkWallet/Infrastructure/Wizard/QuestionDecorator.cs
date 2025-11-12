using ArkWallet.ValueObjects;
using ArkWallet.Repositories;

namespace ArkWallet.Infrastructure.Wizard
{
    internal class QuestionDecorator
    {
        private readonly TraderRepository _traderRepo;
        private readonly CharacterTokenRepository _tokenRepo;
        private readonly PortfolioItemRepository _portfolioRepo;

        public QuestionDecorator (TraderRepository traderRepo,
            CharacterTokenRepository tokenRepo,
            PortfolioItemRepository portfolioRepo)
        {
            _traderRepo = traderRepo;
            _tokenRepo = tokenRepo;
            _portfolioRepo = portfolioRepo;
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

            var token = await _tokenRepo.GetByIdAsync(symbol);
            var trader = await _traderRepo.GetByIdAsync(session.Id);

            if (direction == "купить")
            {
                return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                       $"💰 Цена: {token.CurrentPrice:F2}\n" +
                       $"💵 Доступно: {trader.Balance / token.CurrentPrice:F1} шт";
            }
            else
            {
                var item = await _portfolioRepo.GetBySymbolAndOwnerAsync(session.Id, symbol);
                return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                       $"📦 В портфеле: {item.Quantity} шт";
            }
        }

        private async Task<string> DecoratePriceQuestion(string baseQuestion, UserSession session)
        {
            var symbol = session.Data["set_token"]?.ToString();
            if (string.IsNullOrEmpty(symbol)) return baseQuestion;

            var token = await _tokenRepo.GetByIdAsync(symbol);
            return $"{baseQuestion}\n\n💎 Токен: {symbol}\n" +
                   $"📊 Текущая цена: {token.CurrentPrice:F2}";
        }
    }
}
