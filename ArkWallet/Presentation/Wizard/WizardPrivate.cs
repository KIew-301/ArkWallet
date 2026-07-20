using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Domain.ValueObjects;
using Newtonsoft.Json;

namespace ArkWallet.Infrastructure.Wizard
{
    partial class WizardEngine
    {
        private void ConfigureAdditionHandlers()
        {
            _config.Commands["/admin_create_token"][0].Handler = AdminHandleTokenCreate;
            _config.Commands["/admin_set_token_to_user"][0].Handler = AdminHandleSetTokenToUser;
            _config.Commands["/admin_add_balance_to_user"][0].Handler = AdminHandleAddBalanceToUser;
            _config.Commands["/admin_update_token_media"][0].Handler = AdminHandleUpdateTokenMedia;
        }

        private async Task<StepResult> AdminHandleTokenCreate(UserSession session, string input)
        {
            try
            {
                var command = JsonConvert.DeserializeObject<CreateTokenCommand>(input);
                var result = await _tokenCreationServices.CreateTokenAsync(command);
                return result.IsSuccess
                    ? StepResult.Ok("completed", "Token create successful")
                    : StepResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleSetTokenToUser(UserSession session, string input)
            => await ExecuteAdminAction(async () =>
            {
                var tradeData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input);
                long traderId = Convert.ToInt64(tradeData["traderId"]);
                string? symbol = tradeData["symbolId"].ToString();
                int quantity = Convert.ToInt32(tradeData["quantity"]);
                return await _portfolioUpdatingService.CreateOrUpdatePortfolioAsync(traderId, symbol, quantity);
            }, "Portfolia update successful");

        private async Task<StepResult> AdminHandleAddBalanceToUser(UserSession session, string input)
            => await ExecuteAdminAction(async () =>
            {
                var tradeData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input);
                long traderId = Convert.ToInt64(tradeData["traderId"]);
                int amount = Convert.ToInt32(tradeData["amount"]);
                return await _traderBalanceUpdatingService.AddToBalanceAsync(traderId, amount);
            }, "Balance update successful");

        private async Task<StepResult> AdminHandleUpdateTokenMedia(UserSession session, string input)
            => await ExecuteAdminAction(async () =>
            {
                var tradeData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input);
                string symbol = tradeData["symbol"].ToString()!;
                string iconUrl = tradeData["iconUrl"].ToString()!;
                string imageUrl = tradeData["imageUrl"].ToString()!;
                return await _tokenMediaUpdateService.UpdateTokenMediaAsync(symbol, iconUrl, imageUrl);
            }, "Token media updated successfully");

        private async Task<StepResult> ExecuteAdminAction(Func<Task<Result>> action, string successMessage)
        {
            try
            {
                var result = await action();
                return result.IsSuccess
                    ? StepResult.Ok("completed", successMessage)
                    : StepResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }
    }
}
