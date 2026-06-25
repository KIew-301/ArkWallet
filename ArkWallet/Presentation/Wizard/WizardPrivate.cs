using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Domain.ValueObjects;
using Newtonsoft.Json;

namespace ArkWallet.Infrastructure.Wizard
{
    partial class WizardEngine
    {
        private void ConfigureAdditionHandlers()
        {
            _config.Commands["/admincreatetoken"][0].Handler = AdminHandleTokenCreate;
            _config.Commands["/adminaddtokentouser"][0].Handler = AdminHandleAddTokenToUser;
            _config.Commands["/adminaddbalancetouser"][0].Handler = AdminHandleAddBalanceToUser;
        }

        private async Task<StepResult> AdminHandleTokenCreate(UserSession session, string input)
        {
            try
            {
                var command = JsonConvert.DeserializeObject<CreateTokenCommand>(input);
                var result = await _tokenCreationServices.CreateTokenAsync(command);
                if (result.IsSuccess)
                    return StepResult.Ok("completed", "Token create successful");
                else
                    return StepResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleAddTokenToUser(UserSession session, string input)
        {
            try
            {
                var tradeData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input);

                long traderId = Convert.ToInt64(tradeData["traderId"]);
                string? symbol = tradeData["symbolId"].ToString();
                int quantity = Convert.ToInt32(tradeData["quantity"]);

                var result = await _portfolioUpdatingService.CreateOrUpdatePortfolioAsync(traderId, symbol, quantity);

                if (result.IsSuccess)
                    return StepResult.Ok("completed", "Portfolia update successful");
                else
                    return StepResult.Error(result.ErrorMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleAddBalanceToUser(UserSession session, string input)
        {
            try
            {
                var tradeData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input);

                long traderId = Convert.ToInt64(tradeData["traderId"]);
                int amount = Convert.ToInt32(tradeData["amount"]);

                var result = await _traderBalanceUpdatingService.AddToBalanceAsync(traderId, amount);

                if (result.IsSuccess)
                    return StepResult.Ok("completed", "Balance update successful");
                else
                    return StepResult.Error(result.ErrorMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }
    }
}
