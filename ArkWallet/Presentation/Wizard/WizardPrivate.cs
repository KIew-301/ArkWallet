using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Domain.ValueObjects;
using Newtonsoft.Json;

namespace ArkWallet.Infrastructure.Wizard
{
    partial class WizardEngine
    {
        private const string TokenSymbolDataKey = "token_symbol";

        private const string AdminHelpText =
            "Admin commands help:\n\n" +
            "1) /admin_create_token\n" +
            "   Creates a new token.\n" +
            "   JSON fields:\n" +
            "     Symbol (string) — token symbol, e.g. \"ARK_001\"\n" +
            "     Name (string) — token name\n" +
            "     Rarity (int) — rarity: 1=1star..6=6star\n" +
            "     StartPrice (decimal) — initial price > 0\n" +
            "     TotalSupply (int) — total supply > 0\n" +
            "     IsActive (bool) — active flag\n" +
            "     ImageUrl (string) — token image URL\n" +
            "     IconUrl (string) — token icon URL\n\n" +
            "2) /admin_set_token_to_user\n" +
            "   Gives tokens to a trader.\n" +
            "   JSON fields:\n" +
            "     traderId (long) — Telegram user ID\n" +
            "     symbolId (string) — token symbol\n" +
            "     quantity (int) — token amount\n\n" +
            "3) /admin_add_balance_to_user\n" +
            "   Adds balance to a trader.\n" +
            "   JSON fields:\n" +
            "     traderId (long) — Telegram user ID\n" +
            "     amount (int) — amount to add\n\n" +
            "4) /admin_update_token_media\n" +
            "   Updates token image/icon.\n" +
            "   JSON fields:\n" +
            "     symbol (string) — token symbol\n" +
            "     iconUrl (string) — new icon URL\n" +
            "     imageUrl (string) — new image URL\n\n" +
            "5) /admin_bots_activity\n" +
            "   Shows market maker bots data.\n" +
            "   First step: select token symbol.\n" +
            "   Returns JSON list of bots.\n\n" +
            "6) /admin_bots_reconstruction\n" +
            "   Updates bot parameters.\n" +
            "   JSON fields (null = keep current):\n" +
            "     botId (int) — bot ID (required)\n" +
            "     basePower (decimal|null) — new base power\n" +
            "     role (string|null) — \"Buyer\" or \"Seller\"\n" +
            "     isActive (bool|null) — active flag\n\n" +
            "7) /admin_generate_auth_token\n" +
            "   Generates JWT auth token for a user.\n" +
            "   Admin_Main only.\n" +
            "   JSON fields:\n" +
            "     telegramId (long) — Telegram user ID";

        private void ConfigureAdditionHandlers()
        {
            _config.Commands["/admin_create_token"][0].Handler = AdminHandleTokenCreate;
            _config.Commands["/admin_set_token_to_user"][0].Handler = AdminHandleSetTokenToUser;
            _config.Commands["/admin_add_balance_to_user"][0].Handler = AdminHandleAddBalanceToUser;
            _config.Commands["/admin_update_token_media"][0].Handler = AdminHandleUpdateTokenMedia;
            _config.Commands["/admin_help"][0].Handler = AdminHandleHelp;
            _config.Commands["/admin_bots_activity"][0].Handler = AdminHandleBotsActivitySelectSymbol;
            _config.Commands["/admin_bots_activity"][1].Handler = AdminHandleBotsActivityShow;
            _config.Commands["/admin_bots_reconstruction"][0].Handler = AdminHandleBotsReconstruction;
            _config.Commands["/admin_generate_auth_token"][0].Handler = AdminHandleGenerateAuthToken;
        }

        private Task<StepResult> AdminHandleHelp(UserSession session, string input)
            => Task.FromResult(StepResult.Ok("completed", AdminHelpText));

        private async Task<StepResult> AdminHandleTokenCreate(UserSession session, string input)
        {
            try
            {
                var rawData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input,
                    new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Decimal });
                if (rawData == null)
                    return StepResult.Error("Invalid JSON input");

                var normalized = NormalizeKeysToPascalCase(rawData);
                var normalizedJson = JsonConvert.SerializeObject(normalized);
                var command = JsonConvert.DeserializeObject<CreateTokenCommand>(normalizedJson);

                if (command == null)
                    return StepResult.Error("Failed to parse token creation data");

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

        private static Dictionary<string, object> NormalizeKeysToPascalCase(Dictionary<string, object> data)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var kvp in data)
            {
                var pascalKey = char.ToUpperInvariant(kvp.Key[0]) + kvp.Key[1..];
                result[pascalKey] = kvp.Value;
            }
            return result;
        }

        private async Task<StepResult> AdminHandleSetTokenToUser(UserSession session, string input)
            => await ExecuteAdminAction(async () =>
            {
                var tradeData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input);
                if (tradeData == null)
                    return Result.Fail("Invalid input data");

                long traderId = Convert.ToInt64(tradeData["traderId"]);
                string? symbol = tradeData["symbolId"]?.ToString();
                int quantity = Convert.ToInt32(tradeData["quantity"]);
                return await _portfolioUpdatingService.CreateOrUpdatePortfolioAsync(traderId, symbol ?? string.Empty, quantity);
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
                if (tradeData == null)
                    return Result.Fail("Invalid input data");

                string symbol = tradeData["symbol"]?.ToString() ?? string.Empty;
                string iconUrl = tradeData["iconUrl"]?.ToString() ?? string.Empty;
                string imageUrl = tradeData["imageUrl"]?.ToString() ?? string.Empty;
                return await _tokenMediaUpdateService.UpdateTokenMediaAsync(symbol, iconUrl, imageUrl);
            }, "Token media updated successfully");

        private static async Task<StepResult> ExecuteAdminAction(Func<Task<Result>> action, string successMessage)
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

        private async Task<StepResult> AdminHandleBotsActivitySelectSymbol(UserSession session, string input)
            => await ValidateAndStoreToken(session, input, "show_bots");

        private async Task<StepResult> AdminHandleBotsActivityShow(UserSession session, string input)
        {
            var symbol = session.Data[TokenSymbolDataKey]?.ToString();
            if (string.IsNullOrEmpty(symbol))
                return StepResult.Ok("completed", "Token not selected.");

            var botsResult = await _botQueryService.GetBotsBySymbolAsync(symbol);
            if (!botsResult.TryGetData(out var bots) || bots.Count == 0)
                return StepResult.Ok("completed", $"No bots found for {symbol}.");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Bots for {symbol} (count: {bots.Count}):\n");

            foreach (var bot in bots)
            {
                sb.AppendLine($"Bot #{bot.Id}:");
                sb.AppendLine($"  Symbol: {bot.Symbol}");
                sb.AppendLine($"  TraderId: {bot.TraderId}");
                sb.AppendLine($"  BasePower: {bot.BasePower}");
                sb.AppendLine($"  Role: {bot.Role}");
                sb.AppendLine($"  NextPowerChange: {bot.NextPowerChange:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine($"  NextRebalance: {bot.NextRebalance:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine($"  IsActive: {bot.IsActive}");
                sb.AppendLine($"  CreatedAt: {bot.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine();
            }

            var refreshButton = new List<QuickButton>
            {
                new() { Text = "Refresh", Value = $"/admin_bots_activity {symbol}" }
            };

            var stepResult = StepResult.Ok("completed", sb.ToString());
            stepResult.Buttons = refreshButton;
            return stepResult;
        }

        private async Task<StepResult> AdminHandleBotsReconstruction(UserSession session, string input)
        {
            try
            {
                var rawData = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(input);
                if (rawData == null || rawData.Length == 0)
                    return StepResult.Error("Expected JSON array with at least one bot entry.");

                var messages = new List<string>();

                foreach (var entry in rawData)
                {
                    if (!entry.TryGetValue("botId", out var botIdObj))
                    {
                        messages.Add("Skipped entry: botId is required");
                        continue;
                    }

                    long botId = Convert.ToInt64(botIdObj);
                    decimal? basePower = entry.TryGetValue("basePower", out var bpObj) && bpObj != null
                        ? Convert.ToDecimal(bpObj)
                        : null;
                    string? role = entry.TryGetValue("role", out var roleObj) && roleObj != null
                        ? roleObj.ToString()
                        : null;
                    bool? isActive = entry.TryGetValue("isActive", out var activeObj) && activeObj != null
                        ? Convert.ToBoolean(activeObj)
                        : null;

                    var updateResult = await _botQueryService.UpdateBotAsync(botId, basePower, role, isActive);
                    messages.Add(updateResult.IsSuccess
                        ? $"Bot #{botId} updated"
                        : $"Bot #{botId}: {updateResult.Message}");
                }

                return StepResult.Ok("completed", string.Join("\n", messages));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<(string?, List<QuickButton>?)> HandleQuickAdminBotsActivity(string symbolStr)
        {
            var symbol = symbolStr.ToUpper();
            var botsResult = await _botQueryService.GetBotsBySymbolAsync(symbol);
            if (!botsResult.TryGetData(out var bots) || bots.Count == 0)
                return ($"No bots found for {symbol}.", null);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Bots for {symbol} (count: {bots.Count}):\n");

            foreach (var bot in bots)
            {
                sb.AppendLine($"Bot #{bot.Id}:");
                sb.AppendLine($"  Symbol: {bot.Symbol}");
                sb.AppendLine($"  TraderId: {bot.TraderId}");
                sb.AppendLine($"  BasePower: {bot.BasePower}");
                sb.AppendLine($"  Role: {bot.Role}");
                sb.AppendLine($"  NextPowerChange: {bot.NextPowerChange:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine($"  NextRebalance: {bot.NextRebalance:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine($"  IsActive: {bot.IsActive}");
                sb.AppendLine($"  CreatedAt: {bot.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine();
            }

            var buttons = new List<QuickButton>
            {
                new() { Text = "Refresh", Value = $"/admin_bots_activity {symbol}" }
            };

            return (sb.ToString(), buttons);
        }

        private async Task<StepResult> AdminHandleGenerateAuthToken(UserSession session, string input)
        {
            if (session.Id != _primaryAdminId)
                return StepResult.Error("This command is available to Admin_Main only.");

            try
            {
                var tradeData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input);
                if (tradeData == null || !tradeData.ContainsKey("telegramId"))
                    return StepResult.Error("Field \"telegramId\" is required.");

                long targetTelegramId = Convert.ToInt64(tradeData["telegramId"]);

                if (targetTelegramId <= 0)
                    return StepResult.Error("telegramId must be a positive number.");

                var isRegistered = await _traderRegistrationService.CheckTraderAlreadyRegistered(targetTelegramId);
                if (!isRegistered)
                {
                    var regResult = await _traderRegistrationService.RegisterTraderAsync(targetTelegramId, $"User_{targetTelegramId}");
                    if (!regResult.IsSuccess)
                        return StepResult.Error($"Failed to register user: {regResult.Message}");
                }

                var token = _tokenService.GenerateToken(targetTelegramId);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Auth token for user {targetTelegramId}:");
                sb.AppendLine();
                sb.AppendLine(token);

                return StepResult.Ok("completed", sb.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }
    }
}
