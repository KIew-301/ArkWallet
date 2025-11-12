using ArkWallet.Entities;
using ArkWallet.ValueObjects;
using Microsoft.CodeAnalysis;

namespace ArkWallet.Infrastructure.Wizard
{
    partial class WizardEngine
    {
        private async Task<StepResult> HandleSetName(UserSession session, string input)
        {
            await AddNewTrader(session.Id, input);
            return StepResult.Ok("completed", "Отлично! Вы успешно зарегистрированы!");
        }

        private async Task<StepResult> HandleSetDirection(UserSession session, string input)
        {
            var answer = input.ToLower();

            if (answer != "купить" && answer != "продать")
            {
                return StepResult.Error($"Необходимо ввести КУПИТЬ или ПРОДАТЬ.");
            }

            return StepResult.Ok("set_token");
        }

        private async Task<StepResult> HandleSetToken(UserSession session, string input)
        {
            CharacterToken? token = 
                await _uow.Tokens.GetByIdAsync(input.ToUpper());
            Trader? trader = 
                await _uow.Traders.GetByIdAsync(session.Id);
            List<PortfolioItem> items = 
                await _uow.Portfolios.GetByTraderAsync(session.Id);

            string direction = session.Data["set_direction"].ToString().ToLower();

            if (token == null)
            {
                return StepResult.Error("Такого токена не существует.");
            } 
            else if (direction == "продать" && !items.Any(i => i.CharacterTokenId == token.Symbol))
            {
                return StepResult.Error($"Вы не владеете токеном {token.Symbol}.");
            }

            return StepResult.Ok("set_quantity");
        }

        private async Task<StepResult> HandleSetTokenQuantity(UserSession session, string input)
        {
            string direction = session.Data["set_direction"].ToString().ToLower();
            string symbol = session.Data["set_token"].ToString().ToUpper();

            if (symbol == null)
                return StepResult.Error("Ошибка получения токена.");
            if (direction == null)
                return StepResult.Error("Ошибка получения направления.");

            Trader? trader =
                await _uow.Traders.GetByIdAsync(session.Id);
            PortfolioItem? item =
                await _uow.Portfolios.GetByTraderAndSymbolAsync(session.Id, symbol);
            CharacterToken? token =
                await _uow.Tokens.GetByIdAsync(symbol);

            if (trader == null)
                return StepResult.Error("Ошибка получения данных о трейдере.");
            if (token == null)
                return StepResult.Error("Ошибка получения данных о трейдере.");
            if (item == null || item.CharacterToken == null)
                return StepResult.Error("Ошибка в получении портфеля пользователя.");

            if (!int.TryParse(input, out int quantity))
                return StepResult.Error("Необходимо ввести целое число.");

            if (direction == "купить")
            {
                if (trader.Balance < quantity * token.CurrentPrice)
                {
                    return StepResult.Error($"Недостаточно средств для покупки такого количества токенов.");
                }
            }
            else
            {
                if (item.Quantity < quantity)
                {
                    return StepResult.Error($"Недостаточно токенов {item.CharacterToken.Symbol} в портфеле.");
                }
            }

            return StepResult.Ok("set_price");
        }

        private async Task<StepResult> HandleSetTokenPrice(UserSession session, string input)
        {
            if (!decimal.TryParse(input, out decimal price))
            {
                return StepResult.Error("Необходимо ввести число (допустимо не целок).");
            }

            if (price <= 0)
            {
                return StepResult.Error("Цена должна быть равно 0 или быть больше.");
            }

            session.Data.Add("set_price", input);

            var result = await AddNewOrder(session);

            if (result.IsFailed)
                return StepResult.Ok("completed", "Ошибка обработки ордера");
            else if (result.IsFilled)
                return StepResult.Ok("completed",
                    $"Ордер [{session.Data["set_direction"].ToString()} " +
                    $"токен {result.Order.CharacterTokenId} " +
                    $"в количестве {result.Order.Quantity} " +
                    $"по цене {result.Order.Price:F2}] " +
                    $"успешно выставлен и уже исполнен."
                    );
            else
                return StepResult.Ok("completed",
                    $"Ордер [{session.Data["set_direction"].ToString()} " +
                    $"токен {result.Order.CharacterTokenId} " +
                    $"в количестве {result.Order.Quantity} " +
                    $"по цене {result.Order.Price:F2}] " +
                    $"успешно выставлен."
                    );
        }
    }
}
