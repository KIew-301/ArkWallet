using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Application.Services;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using Microsoft.CodeAnalysis;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Model;

namespace ArkWallet.Infrastructure.Wizard
{
    partial class WizardEngine
    {
        private async Task<StepResult> HandleSetName(UserSession session, string input)
        {
            await _traderRegistrationService.RegisterTraderAsync(session.Id, input);
            return StepResult.Ok("completed", "Отлично! Вы успешно зарегистрированы!");
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

            if (!result.IsSuccess)
                return StepResult.Error(result.ErrorMessage);

            var orderDescription = result.Order.GetDesctiption();
            var closedOrders = result.ClosesOrder?
                .Select(o => (o.OwnerId, $"Ордер {o.GetDesctiption()} успешно исполнен"))
                .ToDictionary();

            var message = result.IsFilled
                ? $"Ордер {orderDescription} успешно выставлен и уже исполнен."
                : $"Ордер {orderDescription} успешно выставлен.";

            return StepResult.Ok("completed", message, closedOrders);
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
    }
}
