using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TradeServices;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Newtonsoft.Json;

namespace ArkWallet.Infrastructure.Wizard
{
    partial class WizardEngine
    {
        private const string TokenSymbolDataKey = "token_symbol";
        private const string MiningRuleIdDataKey = "mining_rule_id";

        private const string AdminHelpText =
            "Admin commands:\n\n" +
            "/admin_help_trader — Trader commands\n" +
            "/admin_help_token — Token commands\n" +
            "/admin_help_other — Other commands\n" +
            "/admin_help_mining — Mining commands\n" +
            "/admin_help_access — Access control";

        private const string AdminHelpTraderText =
            "Trader commands:\n\n" +
            "1) /admin_get_trader_profile\n" +
            "   Get trader profile as TXT file.\n" +
            "   JSON: { \"telegramId\": 123456789 }\n\n" +
            "2) /admin_get_trader_orders\n" +
            "   Get trader orders as TXT file.\n" +
            "   JSON: { \"telegramId\": 123456789, \"status\": \"Active\", \"direction\": \"Buy\" }\n" +
            "   status: Active|Filled|Cancelled|All (default: All)\n" +
            "   direction: Buy|Sell|All (default: All)\n\n" +
            "3) /admin_get_trader_trades\n" +
            "   Get trader trades as TXT file.\n" +
            "   JSON: { \"telegramId\": 123456789, \"direction\": \"Buy\" }\n" +
            "   direction: Buy|Sell|All (default: All)\n\n" +
            "4) /admin_get_trader_portfolio\n" +
            "   Get trader portfolio as TXT file.\n" +
            "   JSON: { \"telegramId\": 123456789, \"symbol\": \"ARK_001\" }\n" +
            "   symbol: optional, omit for all tokens\n\n" +
            "5) /admin_set_token_to_user\n" +
            "   Gives tokens to a trader.\n" +
            "   JSON: { \"traderId\": 12345, \"symbolId\": \"ARK_001\", \"quantity\": 100 }\n\n" +
            "6) /admin_add_balance_to_user\n" +
            "   Adds balance to a trader.\n" +
            "   JSON: { \"traderId\": 12345, \"amount\": 500 }\n\n" +
            "7) /admin_generate_auth_token\n" +
            "   Generates JWT auth token for a user (Admin_Main only).\n" +
            "   JSON: { \"telegramId\": 123456789 }";

        private const string AdminHelpTokenText =
            "Token commands:\n\n" +
            "1) /admin_create_token\n" +
            "   Creates a new token.\n" +
            "   JSON: { \"symbol\": \"ARK_001\", \"name\": \"Ark Knight\", \"rarity\": 3,\n" +
            "           \"startPrice\": 100.50, \"totalSupply\": 1000, \"isActive\": true,\n" +
            "           \"imageUrl\": \"...\", \"iconUrl\": \"...\" }\n\n" +
            "2) /admin_create_tokens\n" +
            "   Creates multiple tokens at once (content fill).\n" +
            "   JSON array: [{ \"symbol\": \"ARK_001\", \"name\": \"Ark Knight\", \"rarity\": 3,\n" +
            "                 \"startPrice\": 100.50, \"totalSupply\": 1000, \"isActive\": true,\n" +
            "                 \"imageUrl\": \"...\", \"iconUrl\": \"...\" }, ...]\n\n" +
            "3) /admin_delete_token\n" +
            "   Permanently deletes a token and all related data (orders, trades,\n" +
            "   candles, bots, portfolios). Enter symbol, then confirm.\n\n" +
            "4) /admin_deactivate_token\n" +
            "   Deactivates a token (IsActive = false). It stays in DB but is not tradable.\n" +
            "   Enter symbol, then confirm.\n\n" +
            "5) /admin_update_token_media\n" +
            "   Updates token image/icon.\n" +
            "   JSON: { \"symbol\": \"ARK_001\", \"iconUrl\": \"...\", \"imageUrl\": \"...\" }\n\n" +
            "6) /admin_bots_activity\n" +
            "   Shows market maker bots data.\n" +
            "   Select token symbol, returns bot list.\n\n" +
            "7) /admin_bots_reconstruction\n" +
            "   Updates bot parameters.\n" +
            "   JSON array: [{ \"botId\": 1, \"basePower\": 30, \"role\": \"Buyer\", \"isActive\": true }]\n" +
            "   null = keep current value";

        private const string AdminHelpOtherText =
             "Other commands:\n\n" +
             "1) /admin_broadcast\n" +
             "   Send a message to all registered traders.\n\n" +
             "2) /admin_stats\n" +
             "   Show system statistics with volume data.\n\n" +
             "3) /admin_get_ids\n" +
             "   Get list of all registered traders with Telegram IDs (excluding bots).";

        private const string AdminHelpMiningText =
            "Mining commands:\n\n" +
            "1) /admin_mining_create_machine\n" +
            "   Creates a new mining machine (rules are added separately).\n" +
            "   Name and cost are generated automatically.\n" +
            "   JSON: { \"type\": \"SMAI\", \"switchingTime\": 10,\n" +
            "           \"reusability\": 50, \"isActiveForSale\": true,\n" +
            "           \"efficiency\": 1.0, \"image\": \"https://example.com/image.png\" }\n\n" +
            "2) /admin_mining_update_machine\n" +
            "   Updates a mining machine (machineId required, others optional).\n" +
            "   Name and cost are regenerated automatically.\n" +
            "   JSON: { \"machineId\": 1, \"type\": \"MGC\", \"switchingTime\": 30,\n" +
            "           \"reusability\": 60, \"isActiveForSale\": true,\n" +
            "           \"efficiency\": 0.5, \"image\": \"https://example.com/image.png\" }\n" +
            "   null = keep current value\n\n" +
            "3) /admin_mining_create_rule\n" +
            "   Creates a mining rule (machine-token pair).\n" +
            "   JSON: { \"miningMachineId\": 1, \"characterTokenId\": \"ARK_001\",\n" +
            "           \"miningCoefficient\": 0.9 }\n\n" +
            "4) /admin_mining_update_rule\n" +
            "   Updates a mining rule coefficient.\n" +
            "   JSON: { \"miningRuleId\": 1, \"miningCoefficient\": 0.9 }\n\n" +
            "5) /admin_mining_delete_machine\n" +
            "   Permanently deletes a machine and its rules (fails if slots exist).\n" +
            "   Enter machine Id, then confirm.\n\n" +
            "6) /admin_mining_deactivate_machine\n" +
            "   Deactivates a machine (IsActiveForSale = false). Enter Id, then confirm.\n\n" +
            "7) /admin_mining_delete_rule\n" +
            "   Deletes a mining rule (fails if used by a slot). Enter Id, then confirm.\n\n" +
            "8) /admin_mining_update_global_rule\n" +
            "   Updates token global mining rule (symbol required, others optional).\n" +
            "   JSON: { \"symbol\": \"ARK_001\", \"currentCoefficient\": 1.05,\n" +
            "           \"futureCoefficient\": 0.95, \"baseTokenMiningSpeed\": 50 }\n" +
            "   Coefficients are set as a pair (both or none).\n\n" +
            "9) /admin_mining_app_state\n" +
            "   Shows all service state records (AppState).\n\n" +
            "10) /admin_mining_create_machines\n" +
            "   Creates machines with their rules in one transaction.\n" +
            "   JSON: [ { \"type\": \"SMAI\", \"switchingTime\": 10,\n" +
            "             \"reusability\": 50, \"isActiveForSale\": true,\n" +
            "             \"efficiency\": 1.0, \"image\": \"...\",\n" +
            "             \"rules\": [ { \"characterTokenId\": \"ARK_001\",\n" +
            "                          \"miningCoefficient\": 0.9 } ] } ]\n" +
            "   Rules are optional per machine. Single object also supported.\n\n" +
            "11) /admin_mining_delete_machines\n" +
            "   Permanently deletes several machines and their rules in one transaction.\n" +
            "   Enter machine Ids (comma or space separated), then confirm.";

        private const string AdminHelpAccessText =
            "Access control commands:\n\n" +
            "1) /admin_access_get\n" +
            "   Shows current setting.\n\n" +
            "2) /admin_access_set\n" +
            "   Updates setting.\n" +
            "   JSON: { \"isGlobalAccessEnabled\": true/false,\n" +
            "           \"whiteList\": [123, 456],\n" +
            "           \"blackList\": [789],\n" +
            "           \"isGroupAccessEnabled\": true/false,\n" +
            "           \"groupWhiteList\": [-100123],\n" +
            "           \"groupBlackList\": [-100789] }";

        private void ConfigureAdditionHandlers()
        {
            _config.Commands["/admin_create_token"][0].Handler = AdminHandleTokenCreate;
            _config.Commands["/admin_create_tokens"][0].Handler = AdminHandleTokensCreate;
            _config.Commands["/admin_delete_token"][0].Handler = AdminHandleDeleteTokenSetSymbol;
            _config.Commands["/admin_delete_token"][1].Handler = AdminHandleDeleteTokenConfirm;
            _config.Commands["/admin_deactivate_token"][0].Handler = AdminHandleDeactivateTokenSetSymbol;
            _config.Commands["/admin_deactivate_token"][1].Handler = AdminHandleDeactivateTokenConfirm;
            _config.Commands["/admin_set_token_to_user"][0].Handler = AdminHandleSetTokenToUser;
            _config.Commands["/admin_add_balance_to_user"][0].Handler = AdminHandleAddBalanceToUser;
            _config.Commands["/admin_update_token_media"][0].Handler = AdminHandleUpdateTokenMedia;
            _config.Commands["/admin_help"][0].Handler = AdminHandleHelp;
            _config.Commands["/admin_help_trader"][0].Handler = AdminHandleHelpTrader;
            _config.Commands["/admin_help_token"][0].Handler = AdminHandleHelpToken;
            _config.Commands["/admin_help_other"][0].Handler = AdminHandleHelpOther;
            _config.Commands["/admin_bots_activity"][0].Handler = AdminHandleBotsActivitySelectSymbol;
            _config.Commands["/admin_bots_activity"][1].Handler = AdminHandleBotsActivityShow;
            _config.Commands["/admin_bots_reconstruction"][0].Handler = AdminHandleBotsReconstruction;
            _config.Commands["/admin_generate_auth_token"][0].Handler = AdminHandleGenerateAuthToken;
            _config.Commands["/admin_get_trader_profile"][0].Handler = AdminHandleGetTraderProfile;
            _config.Commands["/admin_get_trader_orders"][0].Handler = AdminHandleGetTraderOrders;
            _config.Commands["/admin_get_trader_trades"][0].Handler = AdminHandleGetTraderTrades;
            _config.Commands["/admin_get_trader_portfolio"][0].Handler = AdminHandleGetTraderPortfolio;
            _config.Commands["/admin_broadcast"][0].Handler = AdminHandleBroadcastSetMessage;
            _config.Commands["/admin_broadcast"][1].Handler = AdminHandleBroadcastConfirm;
            _config.Commands["/admin_stats"][0].Handler = AdminHandleStats;
            _config.Commands["/admin_get_ids"][0].Handler = AdminHandleGetIds;
            _config.Commands["/admin_metrics"][0].Handler = AdminHandleMetrics;
            _config.Commands["/admin_help_mining"][0].Handler = AdminHandleHelpMining;
            _config.Commands["/admin_help_access"][0].Handler = AdminHandleHelpAccess;
            _config.Commands["/admin_mining_create_machine"][0].Handler = AdminHandleMiningCreateMachine;
            _config.Commands["/admin_mining_create_machines"][0].Handler = AdminHandleMiningCreateMachines;
            _config.Commands["/admin_mining_update_machine"][0].Handler = AdminHandleMiningUpdateMachine;
            _config.Commands["/admin_mining_create_rule"][0].Handler = AdminHandleMiningCreateRule;
            _config.Commands["/admin_mining_update_rule"][0].Handler = AdminHandleMiningUpdateRule;
            _config.Commands["/admin_mining_delete_machine"][0].Handler = AdminHandleMiningDeleteMachineSetId;
            _config.Commands["/admin_mining_delete_machine"][1].Handler = AdminHandleMiningDeleteMachineConfirm;
            _config.Commands["/admin_mining_delete_machines"][0].Handler = AdminHandleMiningDeleteMachinesSetIds;
            _config.Commands["/admin_mining_delete_machines"][1].Handler = AdminHandleMiningDeleteMachinesConfirm;
            _config.Commands["/admin_mining_deactivate_machine"][0].Handler = AdminHandleMiningDeactivateMachineSetId;
            _config.Commands["/admin_mining_deactivate_machine"][1].Handler = AdminHandleMiningDeactivateMachineConfirm;
            _config.Commands["/admin_mining_delete_rule"][0].Handler = AdminHandleMiningDeleteRuleSetId;
            _config.Commands["/admin_mining_delete_rule"][1].Handler = AdminHandleMiningDeleteRuleConfirm;
            _config.Commands["/admin_mining_update_global_rule"][0].Handler = AdminHandleMiningUpdateGlobalRule;
            _config.Commands["/admin_mining_app_state"][0].Handler = AdminHandleMiningAppState;
            _config.Commands["/admin_access_get"][0].Handler = AdminHandleAccessGet;
            _config.Commands["/admin_access_set"][0].Handler = AdminHandleAccessSet;
        }

        private Task<StepResult> AdminHandleHelp(UserSession session, string input)
            => Task.FromResult(StepResult.Ok("completed", AdminHelpText));

        private Task<StepResult> AdminHandleHelpTrader(UserSession session, string input)
            => Task.FromResult(StepResult.Ok("completed", AdminHelpTraderText));

        private Task<StepResult> AdminHandleHelpToken(UserSession session, string input)
            => Task.FromResult(StepResult.Ok("completed", AdminHelpTokenText));

        private Task<StepResult> AdminHandleHelpOther(UserSession session, string input)
            => Task.FromResult(StepResult.Ok("completed", AdminHelpOtherText));

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

        private async Task<StepResult> AdminHandleTokensCreate(UserSession session, string input)
        {
            try
            {
                var rawArray = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(input,
                    new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Decimal });
                if (rawArray == null || rawArray.Length == 0)
                    return StepResult.Error("Expected a JSON array with at least one token.");

                var messages = new List<string>();

                foreach (var rawData in rawArray)
                {
                    var normalized = NormalizeKeysToPascalCase(rawData);
                    var normalizedJson = JsonConvert.SerializeObject(normalized);
                    var command = JsonConvert.DeserializeObject<CreateTokenCommand>(normalizedJson);

                    if (command == null)
                    {
                        messages.Add($"Skipped entry: {string.Join(", ", rawData.Keys)} — failed to parse token data");
                        continue;
                    }

                    var result = await _tokenCreationServices.CreateTokenAsync(command);
                    messages.Add(result.IsSuccess
                        ? $"{command.Symbol} ({command.Name}) created"
                        : $"{command.Symbol}: {result.Message}");
                }

                return StepResult.Ok("completed", string.Join("\n", messages));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleDeleteTokenSetSymbol(UserSession session, string input)
        {
            var tokenResult = await _tokenQueryService.GetTokenInfoAsync(input);

            if (!tokenResult.TryGetData(out var tokenInfo))
                return StepResult.Error("Token not found. Check the symbol and try again.");

            session.Data[TokenSymbolDataKey] = tokenInfo.Symbol;
            return StepResult.Ok("confirm_delete");
        }

        private async Task<StepResult> AdminHandleDeleteTokenConfirm(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Deletion cancelled.");

            var symbol = session.Data[TokenSymbolDataKey]?.ToString();

            if (string.IsNullOrEmpty(symbol))
                return StepResult.Error("Token not selected.");

            var result = await _tokenDeletionService.DeleteTokenAsync(symbol);

            return result.IsSuccess
                ? StepResult.Ok("completed", $"Token {symbol} deleted.")
                : StepResult.Error(result.Message);
        }

        private async Task<StepResult> AdminHandleDeactivateTokenSetSymbol(UserSession session, string input)
        {
            var tokenResult = await _tokenQueryService.GetTokenInfoAsync(input);

            if (!tokenResult.TryGetData(out var tokenInfo))
                return StepResult.Error("Token not found. Check the symbol and try again.");

            session.Data[TokenSymbolDataKey] = tokenInfo.Symbol;
            return StepResult.Ok("confirm_deactivate");
        }

        private async Task<StepResult> AdminHandleDeactivateTokenConfirm(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Deactivation cancelled.");

            var symbol = session.Data[TokenSymbolDataKey]?.ToString();

            if (string.IsNullOrEmpty(symbol))
                return StepResult.Error("Token not selected.");

            var result = await _tokenDeletionService.DeactivateTokenAsync(symbol);

            return result.IsSuccess
                ? StepResult.Ok("completed", $"Token {symbol} deactivated.")
                : StepResult.Error(result.Message);
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
        }        private async Task<StepResult> AdminHandleSetTokenToUser(UserSession session, string input)
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

            var message = await BuildBotsMessage(symbol);
            if (message == null)
                return StepResult.Ok("completed", $"No bots found for {symbol}.");

            var refreshButton = new List<QuickButton>
            {
                new() { Text = "Refresh", Value = $"/admin_bots_activity {symbol}" }
            };

            var stepResult = StepResult.Ok("completed", message);
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

        private async Task<WizardResult> HandleQuickAdminBotsActivity(string symbolStr)
        {
            var symbol = symbolStr.ToUpper();
            var message = await BuildBotsMessage(symbol);
            if (message == null)
                return new WizardResult { Message = $"No bots found for {symbol}." };

            var buttons = new List<QuickButton>
            {
                new() { Text = "Refresh", Value = $"/admin_bots_activity {symbol}" }
            };

            return new WizardResult { Message = message, Buttons = buttons };
        }

        private async Task<string?> BuildBotsMessage(string symbol)
        {
            var botsResult = await _botQueryService.GetBotsBySymbolAsync(symbol);
            if (!botsResult.TryGetData(out var bots) || bots.Count == 0)
                return null;

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

            return sb.ToString();
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

        private async Task<StepResult> AdminHandleGetTraderProfile(UserSession session, string input)
        {
            try
            {
                var tradeData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input);
                if (tradeData == null || !tradeData.ContainsKey("telegramId"))
                    return StepResult.Error("Field \"telegramId\" is required.");

                long targetTelegramId = Convert.ToInt64(tradeData["telegramId"]);
                if (targetTelegramId <= 0)
                    return StepResult.Error("telegramId must be a positive number.");

                var profileResult = await _traderQueryService.GetTraderProfileAsync(targetTelegramId);
                if (!profileResult.TryGetData(out var profile))
                    return StepResult.Error(profileResult.Message ?? "Trader not found.");

                var snapshotResult = await _balanceSnapshotService.TakeTotalTraderBalanceSnapshot(targetTelegramId);
                decimal totalBalance = profile.Balance;
                if (snapshotResult.IsSuccess && snapshotResult.TryGetData(out var snapshot))
                    totalBalance = snapshot.totalBalance;

                var portfolioResult = await _portfolioQueryService.GetTraderTokensAsync(targetTelegramId);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"=== Trader Profile: {targetTelegramId} ===");
                sb.AppendLine();
                sb.AppendLine($"Username: {profile.Username}");
                sb.AppendLine($"Balance: {profile.Balance:F2}{Descriptor.CurrencySymbol}");
                sb.AppendLine($"Total Balance: {totalBalance:F2}{Descriptor.CurrencySymbol}");
                sb.AppendLine();

                if (portfolioResult.TryGetData(out var portfolio) && portfolio.Length > 0)
                {
                    sb.AppendLine("Portfolio:");
                    foreach (var p in portfolio)
                    {
                        var symbol = p.TokenInfo?.Symbol ?? "???";
                        var cost = p.Quantity * p.AverageBuyPrice;
                        var currentValue = p.BalanceInToken;
                        var profit = currentValue - cost;
                        sb.AppendLine($"  {symbol}: {p.Quantity} (avg: {p.AverageBuyPrice:F2}, current: {currentValue:F2}, profit: {profit:+0.00;-0.00}{Descriptor.CurrencySymbol})");
                    }
                }
                else
                {
                    sb.AppendLine("Portfolio: empty");
                }

                var positionResult = await _leadersTopByBalanceQueryService.GetTraderPositionAsync(targetTelegramId);
                if (positionResult.IsSuccess && positionResult.TryGetData(out var posData))
                {
                    sb.AppendLine();
                    sb.AppendLine($"Rank: #{posData.Position} / {posData.TotalTraders}");
                }

                return WriteTxtAndReturn(sb.ToString(), $"trader_profile_{targetTelegramId}.txt");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleGetTraderOrders(UserSession session, string input)
        {
            try
            {
                var tradeData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input);
                if (tradeData == null || !tradeData.ContainsKey("telegramId"))
                    return StepResult.Error("Field \"telegramId\" is required.");

                long targetTelegramId = Convert.ToInt64(tradeData["telegramId"]);
                if (targetTelegramId <= 0)
                    return StepResult.Error("telegramId must be a positive number.");

                string statusFilter = tradeData.TryGetValue("status", out var statusObj) ? statusObj?.ToString() ?? "All" : "All";
                string directionFilter = tradeData.TryGetValue("direction", out var dirObj) ? dirObj?.ToString() ?? "All" : "All";

                bool includeActive = statusFilter.Equals("All", StringComparison.OrdinalIgnoreCase) || statusFilter.Equals("Active", StringComparison.OrdinalIgnoreCase);
                bool includeFilled = statusFilter.Equals("All", StringComparison.OrdinalIgnoreCase) || statusFilter.Equals("Filled", StringComparison.OrdinalIgnoreCase);
                bool includeCancelled = statusFilter.Equals("All", StringComparison.OrdinalIgnoreCase) || statusFilter.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);

                var ordersResult = await _orderQueryService.GetTraderOrdersAsync(
                    targetTelegramId, includeActive, includeFilled, includeCancelled);

                if (!ordersResult.TryGetData(out var orders))
                    return StepResult.Error(ordersResult.Message ?? "Failed to get orders.");

                if (directionFilter.Equals("Buy", StringComparison.OrdinalIgnoreCase))
                    orders = orders.Where(o => o.Direction == "Buy").ToList();
                else if (directionFilter.Equals("Sell", StringComparison.OrdinalIgnoreCase))
                    orders = orders.Where(o => o.Direction == "Sell").ToList();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"=== Orders for Trader {targetTelegramId} ===");
                sb.AppendLine($"Filters: status={statusFilter}, direction={directionFilter}");
                sb.AppendLine($"Total: {orders.Count} orders");
                sb.AppendLine();

                if (orders.Count == 0)
                {
                    sb.AppendLine("No orders found.");
                }
                else
                {
                    foreach (var o in orders)
                    {
                        sb.AppendLine($"Order #{o.OrderId}");
                        sb.AppendLine($"  Symbol: {o.Symbol} | Direction: {o.Direction}");
                        sb.AppendLine($"  Price: {o.Price:F2}{Descriptor.CurrencySymbol} | Qty: {o.TotalQuantity} (filled: {o.FilledQuantity}, {o.FillPercent:F0}%)");
                        sb.AppendLine($"  Status: {o.Status}");
                        sb.AppendLine();
                    }
                }

                return WriteTxtAndReturn(sb.ToString(), $"trader_orders_{targetTelegramId}.txt");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleGetTraderTrades(UserSession session, string input)
        {
            try
            {
                var tradeData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input);
                if (tradeData == null || !tradeData.ContainsKey("telegramId"))
                    return StepResult.Error("Field \"telegramId\" is required.");

                long targetTelegramId = Convert.ToInt64(tradeData["telegramId"]);
                if (targetTelegramId <= 0)
                    return StepResult.Error("telegramId must be a positive number.");

                string directionFilter = tradeData.TryGetValue("direction", out var dirObj) ? dirObj?.ToString() ?? "All" : "All";

                var tradesResult = await _tradeQueryService.GetTraderTradesAsync(targetTelegramId, withTokenInfo: true);
                if (!tradesResult.TryGetData(out var trades))
                    return StepResult.Error(tradesResult.Message ?? "Failed to get trades.");

                if (directionFilter.Equals("Buy", StringComparison.OrdinalIgnoreCase))
                    trades = trades.Where(t => t.TraderRole == "Buyer").ToList();
                else if (directionFilter.Equals("Sell", StringComparison.OrdinalIgnoreCase))
                    trades = trades.Where(t => t.TraderRole == "Seller").ToList();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"=== Trades for Trader {targetTelegramId} ===");
                sb.AppendLine($"Filter: direction={directionFilter}");
                sb.AppendLine($"Total: {trades.Count} trades");
                sb.AppendLine();

                if (trades.Count == 0)
                {
                    sb.AppendLine("No trades found.");
                }
                else
                {
                    foreach (var t in trades)
                    {
                        var symbol = t.TokenInfo?.Symbol ?? "???";
                        sb.AppendLine($"  {t.TradeDateTime:yyyy-MM-dd HH:mm:ss UTC} | {t.TraderRole} {symbol}");
                        sb.AppendLine($"    Price: {t.ExecutionPrice:F2} | Qty: {t.Quantity} | PnL: {t.Profit:+0.00;-0.00}{Descriptor.CurrencySymbol}");
                    }
                }

                return WriteTxtAndReturn(sb.ToString(), $"trader_trades_{targetTelegramId}.txt");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleGetTraderPortfolio(UserSession session, string input)
        {
            try
            {
                var tradeData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input);
                if (tradeData == null || !tradeData.ContainsKey("telegramId"))
                    return StepResult.Error("Field \"telegramId\" is required.");

                long targetTelegramId = Convert.ToInt64(tradeData["telegramId"]);
                if (targetTelegramId <= 0)
                    return StepResult.Error("telegramId must be a positive number.");

                string? symbolFilter = tradeData.TryGetValue("symbol", out var symObj) && symObj != null
                    ? symObj.ToString() : null;

                var portfolioResult = await _portfolioQueryService.GetTraderTokensAsync(targetTelegramId);
                if (!portfolioResult.TryGetData(out var portfolio))
                    return StepResult.Error(portfolioResult.Message ?? "Failed to get portfolio.");

                if (!string.IsNullOrEmpty(symbolFilter))
                    portfolio = portfolio.Where(p => p.TokenInfo?.Symbol?.Equals(symbolFilter, StringComparison.OrdinalIgnoreCase) == true).ToArray();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"=== Portfolio for Trader {targetTelegramId} ===");
                sb.AppendLine($"Filter: symbol={symbolFilter ?? "All"}");
                sb.AppendLine($"Total: {portfolio.Length} tokens");
                sb.AppendLine();

                if (portfolio.Length == 0)
                {
                    sb.AppendLine("Portfolio is empty.");
                }
                else
                {
                    foreach (var p in portfolio)
                    {
                        var symbol = p.TokenInfo?.Symbol ?? "???";
                        var name = p.TokenInfo?.Name ?? "???";
                        var cost = p.Quantity * p.AverageBuyPrice;
                        sb.AppendLine($"  {symbol} ({name})");
                        sb.AppendLine($"    Quantity: {p.Quantity}");
                        sb.AppendLine($"    Avg Buy Price: {p.AverageBuyPrice:F2}{Descriptor.CurrencySymbol}");
                        sb.AppendLine($"    Current Value: {p.BalanceInToken:F2}{Descriptor.CurrencySymbol}");
                        sb.AppendLine($"    Cost: {cost:F2}{Descriptor.CurrencySymbol}");
                        sb.AppendLine($"    Profit: {p.ProfitPercent:+0.00;-0.00}%");
                        sb.AppendLine();
                    }
                }

                return WriteTxtAndReturn(sb.ToString(), $"trader_portfolio_{targetTelegramId}.txt");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private static StepResult WriteTxtAndReturn(string content, string fileName)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "arkwallet");
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, fileName);
            File.WriteAllText(filePath, content);

            var stepResult = StepResult.Ok("completed", $"File {fileName} ready.");
            stepResult.SentFilePath = filePath;
            return stepResult;
        }

        private Task<StepResult> AdminHandleBroadcastSetMessage(UserSession session, string input)
        {
            session.Data["broadcast_message"] = input;
            var preview = $"Message preview:\n\n{input}";
            return Task.FromResult(StepResult.Ok("confirm", preview));
        }

        private async Task<StepResult> AdminHandleBroadcastConfirm(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Broadcast cancelled.");

            var message = session.Data["broadcast_message"]?.ToString();
            if (string.IsNullOrEmpty(message))
                return StepResult.Error("No broadcast message found.");

            try
            {
                var tradersResult = await _traderQueryService.GetAllTraderIdsAsync();
                if (!tradersResult.TryGetData(out var traderIds) || traderIds.Count == 0)
                    return StepResult.Error("No traders found.");

                int sent = 0, failed = 0;
                foreach (var traderId in traderIds)
                {
                    try
                    {
                        await _messageSender.SendMessageAsync(traderId, message);
                        sent++;
                    }
                    catch
                    {
                        failed++;
                    }
                }

                return StepResult.Ok("completed", $"Broadcast sent: {sent} delivered, {failed} failed out of {traderIds.Count} total.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleStats(UserSession session, string input)
        {
            try
            {
                var periodDays = 0;
                if (int.TryParse(input, out var parsed))
                    periodDays = parsed;

                var traderCountResult = await _traderQueryService.GetTraderCountAsync();
                var totalVolumeResult = await _tradingVolumeService.GetTotalVolumeAsync(periodDays, includeBots: false);
                var volumePerTokenResult = await _tradingVolumeService.GetVolumePerTokenAsync(periodDays, includeBots: false);

                var sb = new System.Text.StringBuilder();
                var periodLabel = periodDays == 0 ? "All time" : $"Last {periodDays} days";
                sb.AppendLine($"=== System Statistics ({periodLabel}) ===");
                sb.AppendLine();

                if (traderCountResult.TryGetData(out var traderCount))
                    sb.AppendLine($"Registered traders: {traderCount}");

                if (totalVolumeResult.TryGetData(out var totalVolume))
                    sb.AppendLine($"Total volume (no bots): {totalVolume:F2}{Descriptor.CurrencySymbol}");

                sb.AppendLine();

                if (volumePerTokenResult.TryGetData(out var perToken) && perToken.Count > 0)
                {
                    sb.AppendLine("Volume per token:");
                    foreach (var (symbol, volume) in perToken)
                        sb.AppendLine($"  {symbol}: {volume:F2}{Descriptor.CurrencySymbol}");
                }
                else
                {
                    sb.AppendLine("No trade data available.");
                }

                var buttons = new List<QuickButton>
                {
                    new() { Text = "All", Value = "/admin_stats 0" },
                    new() { Text = "1d", Value = "/admin_stats 1" },
                    new() { Text = "1w", Value = "/admin_stats 7" },
                    new() { Text = "1m", Value = "/admin_stats 30" },
                    new() { Text = "6m", Value = "/admin_stats 180" },
                    new() { Text = "1y", Value = "/admin_stats 365" }
                };

                var stepResult = StepResult.Ok("completed", sb.ToString());
                stepResult.Buttons = buttons;
                return stepResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleGetIds(UserSession session, string input)
        {
            try
            {
                var result = await _traderQueryService.GetAllTradersWithoutBotsAsync();

                if (!result.TryGetData(out var traders))
                    return StepResult.Error("Failed to get trader list.");

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Registered traders (excluding bots):");
                sb.AppendLine();

                foreach (var (username, telegramId) in traders)
                    sb.AppendLine($"• {username} — {telegramId}");

                return StepResult.Ok("completed", sb.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleMetrics(UserSession session, string input)
        {
            try
            {
                var metricsText = await _metricsSnapshotService.GetMetricsTextAsync();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== ArkWallet Metrics ===");
                sb.AppendLine();
                sb.AppendLine(metricsText);
                sb.AppendLine("Полный экспорт в Prometheus-формате: /metrics (порт 5000)");

                return WriteTxtAndReturn(sb.ToString(), "arkwallet_metrics.txt");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private Task<StepResult> AdminHandleHelpMining(UserSession session, string input)
            => Task.FromResult(StepResult.Ok("completed", AdminHelpMiningText));

        private Task<StepResult> AdminHandleHelpAccess(UserSession session, string input)
            => Task.FromResult(StepResult.Ok("completed", AdminHelpAccessText));

        private async Task<StepResult> AdminHandleMiningCreateMachine(UserSession session, string input)
        {
            try
            {
                var rawData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input,
                    new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Decimal });
                if (rawData == null)
                    return StepResult.Error("Invalid JSON input");

                var normalized = NormalizeKeysToPascalCase(rawData);
                var normalizedJson = JsonConvert.SerializeObject(normalized);
                var command = JsonConvert.DeserializeObject<MiningMachineCreationCommand>(normalizedJson);

                if (command == null)
                    return StepResult.Error("Failed to parse machine creation data");

                var result = await _miningMachineCreationService.CreateMachineAsync(command);

                return result.TryGetData(out var data)
                    ? StepResult.Ok("completed", $"Machine '{data.Name}' created (Id: {data.Id}).")
                    : StepResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleMiningCreateMachines(UserSession session, string input)
        {
            try
            {
                var trimmed = input.Trim();
                List<MiningMachineCreationCommand> commands;

                if (trimmed.StartsWith('['))
                {
                    commands = JsonConvert.DeserializeObject<List<MiningMachineCreationCommand>>(trimmed,
                        new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Decimal }) ?? [];
                }
                else
                {
                    var rawData = JsonConvert.DeserializeObject<Dictionary<string, object>>(trimmed,
                        new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Decimal });
                    if (rawData == null)
                        return StepResult.Error("Invalid JSON input");

                    var normalized = NormalizeKeysToPascalCase(rawData);
                    var command = JsonConvert.DeserializeObject<MiningMachineCreationCommand>(JsonConvert.SerializeObject(normalized),
                        new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Decimal });
                    commands = command == null ? [] : [command];
                }

                if (commands.Count == 0)
                    return StepResult.Error("No valid machines found in JSON.");

                var result = await _miningMachineCreationOrchestrator.CreateMachinesAsync(commands);

                if (!result.TryGetData(out var created) || created.Count == 0)
                    return StepResult.Error(result.Message);

                return StepResult.Ok("completed",
                    $"Created {created.Count} machines (Ids: {string.Join(", ", created.Select(m => m.Id))}).");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleMiningDeleteMachinesSetIds(UserSession session, string input)
        {
            var idsText = input.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries);
            var ids = new List<long>();
            foreach (var part in idsText)
            {
                if (!long.TryParse(part, out var id))
                    return StepResult.Error($"Invalid machine Id: {part}. Use comma or space separated numbers.");
                ids.Add(id);
            }

            if (ids.Count == 0)
                return StepResult.Error("Enter at least one machine Id.");

            session.Data[MiningMachineIdsDataKey] = ids.ToArray();
            return StepResult.Ok("confirm_delete", $"Selected {ids.Count} machines for deletion.");
        }

        private async Task<StepResult> AdminHandleMiningDeleteMachinesConfirm(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Deletion cancelled.");

            var machineIds = session.Data[MiningMachineIdsDataKey] as long[]
                ?? throw new InvalidOperationException("Machines not selected.");

            var result = await _miningMachineDeletionService.DeleteMachinesAsync(machineIds);

            return result.IsSuccess
                ? StepResult.Ok("completed", $"Deleted {machineIds.Length} machines (Ids: {string.Join(", ", machineIds)}).")
                : StepResult.Error(result.Message);
        }

        private async Task<StepResult> AdminHandleMiningUpdateMachine(UserSession session, string input)
        {
            try
            {
                var rawData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input,
                    new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Decimal });
                if (rawData == null)
                    return StepResult.Error("Invalid JSON input");

                if (!rawData.TryGetValue("machineId", out var machineIdObj) || machineIdObj == null)
                    return StepResult.Error("Field \"machineId\" is required.");

                long machineId = Convert.ToInt64(machineIdObj);

                string? type = rawData.TryGetValue("type", out var typeObj) && typeObj != null
                    ? typeObj.ToString()
                    : null;
                int? switchingTime = rawData.TryGetValue("switchingTime", out var switchObj) && switchObj != null
                    ? Convert.ToInt32(switchObj)
                    : null;
                decimal? reusability = rawData.TryGetValue("reusability", out var reuseObj) && reuseObj != null
                    ? Convert.ToDecimal(reuseObj)
                    : null;
                bool? isActiveForSale = rawData.TryGetValue("isActiveForSale", out var activeObj) && activeObj != null
                    ? Convert.ToBoolean(activeObj)
                    : null;
                string? image = rawData.TryGetValue("image", out var imageObj) && imageObj != null
                    ? imageObj.ToString()
                    : null;
                decimal? efficiency = rawData.TryGetValue("efficiency", out var effObj) && effObj != null
                    ? Convert.ToDecimal(effObj)
                    : null;

                var command = new MiningMachineUpdateCommand(
                    machineId, type, switchingTime, reusability, isActiveForSale, image, efficiency);

                var result = await _miningMachineUpdateService.UpdateMachineAsync(command);

                return result.IsSuccess
                    ? StepResult.Ok("completed", $"Machine {machineId} updated.")
                    : StepResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleMiningCreateRule(UserSession session, string input)
        {
            try
            {
                var trimmed = input.Trim();

                if (trimmed.StartsWith('['))
                {
                    var commands = JsonConvert.DeserializeObject<List<MiningMachineRuleCreationCommand>>(trimmed,
                        new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Decimal });

                    if (commands == null || commands.Count == 0)
                        return StepResult.Error("No valid rules found in JSON array.");

                    var result = await _miningMachineRuleCreationService.CreateRulesAsync(commands);

                    if (!result.TryGetData(out var ids))
                        return StepResult.Error(result.Message);

                    return StepResult.Ok("completed", $"Created {ids.Count} rules (Ids: {string.Join(", ", ids)}).");
                }

                var rawData = JsonConvert.DeserializeObject<Dictionary<string, object>>(trimmed,
                    new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Decimal });
                if (rawData == null)
                    return StepResult.Error("Invalid JSON input");

                var normalized = NormalizeKeysToPascalCase(rawData);
                var normalizedJson = JsonConvert.SerializeObject(normalized);
                var command = JsonConvert.DeserializeObject<MiningMachineRuleCreationCommand>(normalizedJson);

                if (command == null)
                    return StepResult.Error("Failed to parse rule creation data");

                var singleResult = await _miningMachineRuleCreationService.CreateRuleAsync(command);

                return singleResult.TryGetData(out var ruleId)
                    ? StepResult.Ok("completed", $"Rule created (Id: {ruleId}).")
                    : StepResult.Error(singleResult.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleMiningUpdateRule(UserSession session, string input)
        {
            try
            {
                var rawData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input,
                    new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Decimal });
                if (rawData == null)
                    return StepResult.Error("Invalid JSON input");

                if (!rawData.TryGetValue("miningRuleId", out var ruleIdObj) || ruleIdObj == null)
                    return StepResult.Error("Field \"miningRuleId\" is required.");
                if (!rawData.TryGetValue("miningCoefficient", out var coeffObj) || coeffObj == null)
                    return StepResult.Error("Field \"miningCoefficient\" is required.");

                long miningRuleId = Convert.ToInt64(ruleIdObj);
                decimal miningCoefficient = Convert.ToDecimal(coeffObj);

                var command = new MiningMachineRuleUpdateCommand(miningRuleId, miningCoefficient);
                var result = await _miningMachineRuleUpdateService.UpdateRuleAsync(command);

                return result.IsSuccess
                    ? StepResult.Ok("completed", $"Rule {miningRuleId} updated.")
                    : StepResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleMiningDeleteMachineSetId(UserSession session, string input)
        {
            if (!long.TryParse(input, out var machineId))
                return StepResult.Error("Enter a valid machine Id.");

            session.Data[MiningMachineIdDataKey] = machineId;
            return StepResult.Ok("confirm_delete");
        }

        private async Task<StepResult> AdminHandleMiningDeleteMachineConfirm(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Deletion cancelled.");

            var machineId = GetSelectedMiningMachineId(session);
            var result = await _miningMachineDeletionService.DeleteMachineAsync(machineId);

            return result.IsSuccess
                ? StepResult.Ok("completed", $"Machine {machineId} deleted.")
                : StepResult.Error(result.Message);
        }

        private async Task<StepResult> AdminHandleMiningDeactivateMachineSetId(UserSession session, string input)
        {
            if (!long.TryParse(input, out var machineId))
                return StepResult.Error("Enter a valid machine Id.");

            session.Data[MiningMachineIdDataKey] = machineId;
            return StepResult.Ok("confirm_deactivate");
        }

        private async Task<StepResult> AdminHandleMiningDeactivateMachineConfirm(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Deactivation cancelled.");

            var machineId = GetSelectedMiningMachineId(session);
            var result = await _miningMachineDeletionService.DeactivateMachineAsync(machineId);

            return result.IsSuccess
                ? StepResult.Ok("completed", $"Machine {machineId} deactivated.")
                : StepResult.Error(result.Message);
        }

        private async Task<StepResult> AdminHandleMiningDeleteRuleSetId(UserSession session, string input)
        {
            if (!long.TryParse(input, out var ruleId))
                return StepResult.Error("Enter a valid rule Id.");

            session.Data[MiningRuleIdDataKey] = ruleId;
            return StepResult.Ok("confirm_delete");
        }

        private async Task<StepResult> AdminHandleMiningDeleteRuleConfirm(UserSession session, string input)
        {
            if (input != "confirm")
                return StepResult.Ok("completed", "Deletion cancelled.");

            var ruleId = session.Data[MiningRuleIdDataKey] is long id
                ? id
                : throw new InvalidOperationException("Rule not selected.");

            var result = await _miningMachineRuleDeletionService.DeleteRuleAsync(ruleId);

            return result.IsSuccess
                ? StepResult.Ok("completed", $"Rule {ruleId} deleted.")
                : StepResult.Error(result.Message);
        }

        private async Task<StepResult> AdminHandleMiningUpdateGlobalRule(UserSession session, string input)
        {
            try
            {
                var rawData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input,
                    new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Decimal });
                if (rawData == null)
                    return StepResult.Error("Invalid JSON input");

                string symbol = rawData.TryGetValue("symbol", out var symbolObj) && symbolObj != null
                    ? symbolObj.ToString() ?? string.Empty
                    : string.Empty;

                decimal? currentCoefficient = rawData.TryGetValue("currentCoefficient", out var currentObj) && currentObj != null
                    ? Convert.ToDecimal(currentObj)
                    : null;
                decimal? futureCoefficient = rawData.TryGetValue("futureCoefficient", out var futureObj) && futureObj != null
                    ? Convert.ToDecimal(futureObj)
                    : null;
                decimal? baseTokenMiningSpeed = rawData.TryGetValue("baseTokenMiningSpeed", out var speedObj) && speedObj != null
                    ? Convert.ToDecimal(speedObj)
                    : null;

                var result = await _miningGlobalRuleUpdateService.UpdateRuleAsync(
                    symbol, currentCoefficient, futureCoefficient, baseTokenMiningSpeed);

                return result.IsSuccess
                    ? StepResult.Ok("completed", $"Global rule for {symbol} updated.")
                    : StepResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return StepResult.Error($"Error: {ex.Message}");
            }
        }

        private async Task<StepResult> AdminHandleMiningAppState(UserSession session, string input)
        {
            var result = await _appStateQueryService.TakeAllAsync();

            if (!result.TryGetData(out var states) || states.Count == 0)
                return StepResult.Ok("completed", "No app state records.");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== App State ===");
            sb.AppendLine();

            foreach (var state in states)
            {
                sb.AppendLine($"{state.Key}:");
                sb.AppendLine(state.Value);
                sb.AppendLine();
            }

            return StepResult.Ok("completed", sb.ToString());
        }

        private static long GetSelectedMiningMachineId(UserSession session)
        {
            return session.Data[MiningMachineIdDataKey] is long id
                ? id
                : throw new InvalidOperationException("Machine not selected.");
        }

        private async Task<WizardResult> HandleQuickAdminStats(string periodStr)
        {
            var periodDays = 0;
            if (int.TryParse(periodStr, out var parsed))
                periodDays = parsed;

            var traderCountResult = await _traderQueryService.GetTraderCountAsync();
            var totalVolumeResult = await _tradingVolumeService.GetTotalVolumeAsync(periodDays, includeBots: false);
            var volumePerTokenResult = await _tradingVolumeService.GetVolumePerTokenAsync(periodDays, includeBots: false);

            var sb = new System.Text.StringBuilder();
            var periodLabel = periodDays == 0 ? "All time" : $"Last {periodDays} days";
            sb.AppendLine($"=== System Statistics ({periodLabel}) ===");
            sb.AppendLine();

            if (traderCountResult.TryGetData(out var traderCount))
                sb.AppendLine($"Registered traders: {traderCount}");

            if (totalVolumeResult.TryGetData(out var totalVolume))
                sb.AppendLine($"Total volume (no bots): {totalVolume:F2}{Descriptor.CurrencySymbol}");

            sb.AppendLine();

            if (volumePerTokenResult.TryGetData(out var perToken) && perToken.Count > 0)
            {
                sb.AppendLine("Volume per token:");
                foreach (var (symbol, volume) in perToken)
                    sb.AppendLine($"  {symbol}: {volume:F2}{Descriptor.CurrencySymbol}");
            }
            else
            {
                sb.AppendLine("No trade data available.");
            }

            var buttons = new List<QuickButton>
            {
                new() { Text = "All", Value = "/admin_stats 0" },
                new() { Text = "1d", Value = "/admin_stats 1" },
                new() { Text = "1w", Value = "/admin_stats 7" },
                new() { Text = "1m", Value = "/admin_stats 30" },
                new() { Text = "6m", Value = "/admin_stats 180" },
                new() { Text = "1y", Value = "/admin_stats 365" }
            };

            return new WizardResult { Message = sb.ToString(), Buttons = buttons };
        }

        private Task<StepResult> AdminHandleAccessGet(UserSession session, string input)
            => Task.FromResult(StepResult.Ok("completed", _accessControl.FormatSetting()));

        private async Task<StepResult> AdminHandleAccessSet(UserSession session, string input)
        {
            try
            {
                var data = Newtonsoft.Json.Linq.JObject.Parse(input);
                var setting = _accessControl.GetSetting();

                if (data.TryGetValue("isGlobalAccessEnabled", out var token))
                    setting.IsGlobalAccessEnabled = token.ToObject<bool>();

                if (data.TryGetValue("whiteList", out var white))
                    setting.WhiteList = white.ToObject<List<long>>();

                if (data.TryGetValue("blackList", out var black))
                    setting.BlackList = black.ToObject<List<long>>();

                if (data.TryGetValue("isGroupAccessEnabled", out var groupToken))
                    setting.IsGroupAccessEnabled = groupToken.ToObject<bool>();

                if (data.TryGetValue("groupWhiteList", out var groupWhite))
                    setting.GroupWhiteList = groupWhite.ToObject<List<long>>();

                if (data.TryGetValue("groupBlackList", out var groupBlack))
                    setting.GroupBlackList = groupBlack.ToObject<List<long>>();

                var existing = await _dbContext.AccessSettings.FindAsync("default");
                if (existing != null)
                {
                    existing.IsGlobalAccessEnabled = setting.IsGlobalAccessEnabled;
                    existing.WhiteList = setting.WhiteList;
                    existing.BlackList = setting.BlackList;
                    existing.IsGroupAccessEnabled = setting.IsGroupAccessEnabled;
                    existing.GroupWhiteList = setting.GroupWhiteList;
                    existing.GroupBlackList = setting.GroupBlackList;
                }
                else
                {
                    _dbContext.AccessSettings.Add(setting);
                }
                await _dbContext.SaveChangesAsync();

                _accessControl.UpdateSetting(setting);
                return StepResult.Ok("completed", "Access setting updated.\n\n" + _accessControl.FormatSetting());
            }
            catch (Exception ex)
            {
                return StepResult.Ok("completed", $"Error: {ex.Message}");
            }
        }
    }
}
