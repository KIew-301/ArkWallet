using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Domain.ValueObjects;
using Microsoft.CodeAnalysis;

namespace ArkWallet.Infrastructure.Wizard
{
    partial class WizardEngine
    {
        private async Task<StepResult> HandleSetName(UserSession session, string input)
        {
            var result = await _traderRegistrationService.RegisterTraderAsync(session.Id, input);

            if (result.IsSuccess)
                return StepResult.Ok("completed", "Отлично! Вы успешно зарегистрированы!");
            else
                return StepResult.Ok("completed", result.Message);
        }

        private async Task<StepResult> HandleSetDirection(UserSession session, string input)
        {
            input = input.ToLower();

            var validation = _orderValidationService.ValidateDirection(
                input
            );

            if (!validation.IsValid)
                return StepResult.Error(validation.Message);

            session.Data.Add(session.CurrentStep, input);
            return StepResult.Ok("set_token");
        }

        private async Task<StepResult> HandleSetToken(UserSession session, string input)
        {
            string direction = session.Data["set_direction"].ToString();

            var validation = await _orderValidationService.ValidateTokenAsync(
                session.Id,
                input,
                direction
            );

            if (!validation.IsValid)
                return StepResult.Error(validation.Message);

            session.Data.Add(session.CurrentStep, input.ToUpper());
            return StepResult.Ok("set_quantity");
        }

        private async Task<StepResult> HandleSetTokenQuantity(UserSession session, string input)
        {
            string direction = session.Data["set_direction"].ToString().ToLower();
            string symbol = session.Data["set_token"].ToString().ToUpper();

            if (!int.TryParse(input, out var quantity))
                return StepResult.Error("Необходимо ввести целое число.");

            var validation = _orderValidationService.ValidateQuantity(
                quantity
            );

            if (!validation.IsValid)
                return StepResult.Error(validation.Message);

            session.Data.Add(session.CurrentStep, quantity);
            return StepResult.Ok("set_price");
        }

        private async Task<StepResult> HandleSetTokenPrice(UserSession session, string input)
        {
            if (!decimal.TryParse(input, out decimal price))
                return StepResult.Error("Необходимо ввести число (допустимо не целое).");

            string direction = session.Data["set_direction"].ToString().ToLower();
            int quantity = (int)session.Data["set_quantity"];
            string symbol = session.Data["set_token"].ToString().ToUpper();

            var validation = _orderValidationService.ValidatePrice(
                price
            );

            if (!validation.IsValid)
                return StepResult.Error(validation.Message);

            validation = await _orderValidationService.ValidateOrderCreationAsync(
                session.Id,
                symbol,
                direction,
                quantity,
                price
            );

            var command = new CreateOrderCommand(
                session.Id,
                direction,
                symbol,
                quantity,
                price
            );

            var result = await _orderCreationService.CreateOrderAsync(command);

            if (!result.TryGetData(out var data))
                return StepResult.Error(result.Message);

            var orderDescription = data.Order.GetDesctiption();

            var message = data.IsFilled
                ? $"Ордер {orderDescription} успешно выставлен и уже исполнен."
                : $"Ордер {orderDescription} успешно выставлен.";

            return StepResult.Ok("completed", message);
        }

        public async Task<StepResult> HandleSelectOrderToCancel(UserSession session, string input)
        {
            var validation = await _orderValidationService.ValidateOrderCancellationAsync(
                session.Id,
                input
            );

            if (!validation.IsValid)
                return StepResult.Error(validation.Message);

            session.Data[session.CurrentStep] = input;
            return StepResult.Ok("confirm_cancellation");
        }

        public async Task<StepResult> HandleConfirmCancellation(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Отмена не подтверждена");

            var orderId = session.Data["select_order_to_cancel"]?.ToString();

            if (string.IsNullOrEmpty(orderId))
                return StepResult.Error("Ордер не найден");

            var result = await _cancelOrderService.CancelOrderAsync(session.Id, orderId);

            return result.IsSuccess
                ? StepResult.Ok("completed", result.Message)
                : StepResult.Error(result.Message);
        }

        public async Task<StepResult> HandleConfirmCancellationAllOrders(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Отмена не подтверждена");

            var result = await _cancelOrderService.CancelAllOrderAsync(session.Id);

            return result.IsSuccess
                ? StepResult.Ok("completed", result.Message)
                : StepResult.Error(result.Message);
        }

        public async Task<StepResult> HandleGetProfile(UserSession session, string input)
        {
            var profileResult = await _traderQueryService.GetTraderProfileAsync(session.Id);
            var portfolioQueryResult = await _portfolioQueryService.GetTraderTokensAsync(session.Id);

            if (!profileResult.TryGetData(out var profile))
                return StepResult.Ok("completed", profileResult.Message ?? "Данные профиля не найдены.");

            if (!portfolioQueryResult.TryGetData(out var portfolioInfo))
                return StepResult.Ok("completed", "Данные профиля не найдены.");

            string Indent(int count) => new(' ', count);

            var result = $"{profile.Username}!\n" +
                $"{Indent(3)}Баланс: {profile.Balance:F2}\n" +
                $"{Indent(3)}Портфель:\n";

            if (portfolioInfo == null || portfolioInfo.Length <= 0)
            {
                result += $"{Indent(6)}Не владеет токенами".PadLeft(3);
                return StepResult.Ok("completed", result);
            }

            result += string.Join("\n", portfolioInfo.Select(p => $"{Indent(6)}{p.TokenInfo?.Symbol ?? "???"} - {p.Quantity} шт."));

            return StepResult.Ok("completed", result);
        }
    }
}
