using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Entities.Configurations
{
    partial class WizardConfiguration
    {
        private void ConfigureRegistrationAdditionCommand()
        {
            Commands["/admin_create_token"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON to create a token:\n\n" +
                        "{\n" +
                        "  \"symbol\": \"ARK_001\",\n" +
                        "  \"name\": \"Ark Knight\",\n" +
                        "  \"rarity\": 3,\n" +
                        "  \"startPrice\": 100.50,\n" +
                        "  \"totalSupply\": 1000,\n" +
                        "  \"isActive\": true,\n" +
                        "  \"imageUrl\": \"https://example.com/image.png\",\n" +
                        "  \"iconUrl\": \"https://example.com/icon.png\"\n" +
                        "}"
                }
            };

            Commands["/admin_create_tokens"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send a JSON array to create multiple tokens at once:\n\n" +
                        "[\n" +
                        "  {\n" +
                        "    \"symbol\": \"ARK_001\",\n" +
                        "    \"name\": \"Ark Knight\",\n" +
                        "    \"rarity\": 3,\n" +
                        "    \"startPrice\": 100.50,\n" +
                        "    \"totalSupply\": 1000,\n" +
                        "    \"isActive\": true,\n" +
                        "    \"imageUrl\": \"https://example.com/image.png\",\n" +
                        "    \"iconUrl\": \"https://example.com/icon.png\"\n" +
                        "  }\n" +
                        "]"
                }
            };

            Commands["/admin_delete_token"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_symbol",
                    Question = "Enter token symbol to delete:"
                },
                new()
                {
                    Name = "confirm_delete",
                    Question = "Are you sure you want to PERMANENTLY delete this token and all related data (orders, trades, candles, bots, portfolios)?",
                    Buttons = new List<QuickButton>
                    {
                        new() { Text = "✅ Yes, delete", Value = "confirm" },
                        new() { Text = "❌ No, cancel", Value = "cancel" }
                    }
                }
            };

            Commands["/admin_deactivate_token"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_symbol",
                    Question = "Enter token symbol to deactivate:"
                },
                new()
                {
                    Name = "confirm_deactivate",
                    Question = "Are you sure you want to deactivate this token? It will no longer be tradable.",
                    Buttons = new List<QuickButton>
                    {
                        new() { Text = "✅ Yes, deactivate", Value = "confirm" },
                        new() { Text = "❌ No, cancel", Value = "cancel" }
                    }
                }
            };

            Commands["/admin_set_token_to_user"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON to give tokens to a trader:\n\n" +
                        "{\n" +
                        "  \"traderId\": 12345,\n" +
                        "  \"symbolId\": \"ARK_001\",\n" +
                        "  \"quantity\": 100\n" +
                        "}"
                }
            };

            Commands["/admin_add_balance_to_user"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON to add balance to a trader:\n\n" +
                        "{\n" +
                        "  \"traderId\": 12345,\n" +
                        "  \"amount\": 500\n" +
                        "}"
                }
            };

            Commands["/admin_update_token_media"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON to update token image/icon:\n\n" +
                        "{\n" +
                        "  \"symbol\": \"ARK_001\",\n" +
                        "  \"iconUrl\": \"https://example.com/icon.png\",\n" +
                        "  \"imageUrl\": \"https://example.com/image.png\"\n" +
                        "}"
                }
            };

            Commands["/admin_help"] = new List<WizardStep>
            {
                new() { Name = "request", OneStep = true }
            };

            Commands["/admin_help_trader"] = new List<WizardStep>
            {
                new() { Name = "request", OneStep = true }
            };

            Commands["/admin_help_token"] = new List<WizardStep>
            {
                new() { Name = "request", OneStep = true }
            };

            Commands["/admin_help_other"] = new List<WizardStep>
            {
                new() { Name = "request", OneStep = true }
            };

            Commands["/admin_bots_activity"] = new List<WizardStep>
            {
                new() { Name = "select_token", Question = "Select token symbol to view bots:" },
                new() { Name = "show_bots", OneStep = true }
            };

            Commands["/admin_bots_reconstruction"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON array to update bots (null = keep current):\n\n" +
                        "[\n" +
                        "  {\n" +
                        "    \"botId\": 1,\n" +
                        "    \"basePower\": 30,\n" +
                        "    \"role\": \"Buyer\",\n" +
                        "    \"isActive\": true\n" +
                        "  },\n" +
                        "  {\n" +
                        "    \"botId\": 2,\n" +
                        "    \"basePower\": null,\n" +
                        "    \"role\": null,\n" +
                        "    \"isActive\": false\n" +
                        "  }\n" +
                        "]"
                }
            };

            Commands["/admin_generate_auth_token"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON to generate auth token for a user (Admin_Main only):\n\n" +
                        "{\n" +
                        "  \"telegramId\": 123456789\n" +
                        "}"
                }
            };

            Commands["/admin_get_trader_profile"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON to get trader profile:\n\n" +
                        "{\n" +
                        "  \"telegramId\": 123456789\n" +
                        "}"
                }
            };

            Commands["/admin_get_trader_orders"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON to get trader orders (all filters optional):\n\n" +
                        "{\n" +
                        "  \"telegramId\": 123456789,\n" +
                        "  \"status\": \"Active|Filled|Cancelled|All\",\n" +
                        "  \"direction\": \"Buy|Sell|All\"\n" +
                        "}"
                }
            };

            Commands["/admin_get_trader_trades"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON to get trader trades (all filters optional):\n\n" +
                        "{\n" +
                        "  \"telegramId\": 123456789,\n" +
                        "  \"direction\": \"Buy|Sell|All\"\n" +
                        "}"
                }
            };

            Commands["/admin_get_trader_portfolio"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON to get trader portfolio (symbol optional):\n\n" +
                        "{\n" +
                        "  \"telegramId\": 123456789,\n" +
                        "  \"symbol\": \"ARK_001\"  // optional, omit for all\n" +
                        "}"
                }
            };

            Commands["/admin_broadcast"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_message",
                    Question = "Enter the broadcast message to send to all traders:"
                },
                new()
                {
                    Name = "confirm",
                    Question = "Confirm broadcast? Reply 'confirm' to send or 'cancel' to abort.",
                    Buttons = new List<QuickButton>
                    {
                        new() { Text = "Confirm", Value = "confirm" },
                        new() { Text = "Cancel", Value = "cancel" }
                    }
                }
            };

            Commands["/admin_stats"] = new List<WizardStep>
            {
                new() { Name = "set_period", OneStep = true }
            };

            Commands["/admin_get_ids"] = new List<WizardStep>
            {
                new() { Name = "request", OneStep = true }
            };

            Commands["/admin_metrics"] = new List<WizardStep>
            {
                new() { Name = "request", OneStep = true }
            };

            Commands["/admin_help_mining"] = new List<WizardStep>
            {
                new() { Name = "request", OneStep = true }
            };

            Commands["/admin_help_access"] = new List<WizardStep>
            {
                new() { Name = "request", OneStep = true }
            };

            Commands["/admin_mining_create_machine"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON to create a mining machine (name and cost are generated automatically):\n\n" +
                        "{\n" +
                        "  \"type\": \"SMAI\",\n" +
                        "  \"switchingTime\": 10,\n" +
                        "  \"reusability\": 50,\n" +
                        "  \"isActiveForSale\": true,\n" +
                        "  \"efficiency\": 1.0,\n" +
                        "  \"image\": \"https://example.com/image.png\"\n" +
                        "}"
                }
            };

            Commands["/admin_mining_create_machines"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON array to create mining machines with their rules in one transaction.\n" +
                        "Single object or array both supported. Rules are optional per machine:\n\n" +
                        "[\n" +
                        "  {\n" +
                        "    \"type\": \"SMAI\",\n" +
                        "    \"switchingTime\": 10,\n" +
                        "    \"reusability\": 50,\n" +
                        "    \"isActiveForSale\": true,\n" +
                        "    \"efficiency\": 1.0,\n" +
                        "    \"image\": \"https://example.com/image.png\",\n" +
                        "    \"rules\": [\n" +
                        "      { \"characterTokenId\": \"ARK_001\", \"miningCoefficient\": 0.9 },\n" +
                        "      { \"characterTokenId\": \"ARK_002\", \"miningCoefficient\": 0.7 }\n" +
                        "    ]\n" +
                        "  },\n" +
                        "  {\n" +
                        "    \"type\": \"MGC\",\n" +
                        "    \"switchingTime\": 30,\n" +
                        "    \"reusability\": 60,\n" +
                        "    \"isActiveForSale\": true,\n" +
                        "    \"efficiency\": 0.5,\n" +
                        "    \"image\": \"https://example.com/image2.png\"\n" +
                        "  }\n" +
                        "]"
                }
            };

            Commands["/admin_mining_delete_machines"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_ids",
                    Question = "Enter mining machine Ids to delete (comma or space separated):"
                },
                new()
                {
                    Name = "confirm_delete",
                    Question = "Are you sure you want to PERMANENTLY delete these machines and their rules?",
                    Buttons = new List<QuickButton>
                    {
                        new() { Text = "✅ Yes, delete", Value = "confirm" },
                        new() { Text = "❌ No, cancel", Value = "cancel" }
                    }
                }
            };

            Commands["/admin_mining_update_machine"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON to update a mining machine (machineId required, others optional,\n" +
                        "name and cost are regenerated automatically):\n\n" +
                        "{\n" +
                        "  \"machineId\": 1,\n" +
                        "  \"type\": \"MGC\",\n" +
                        "  \"switchingTime\": 30,\n" +
                        "  \"reusability\": 60,\n" +
                        "  \"isActiveForSale\": true,\n" +
                        "  \"efficiency\": 0.5,\n" +
                        "  \"image\": \"https://example.com/new-image.png\"\n" +
                        "}"
                }
            };

            Commands["/admin_mining_create_rule"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON array to create mining rules (machine-token pairs).\n" +
                        "Single object or array both supported:\n\n" +
                        "[\n" +
                        "  {\n" +
                        "    \"miningMachineId\": 1,\n" +
                        "    \"characterTokenId\": \"ARK_001\",\n" +
                        "    \"miningCoefficient\": 0.9\n" +
                        "  },\n" +
                        "  {\n" +
                        "    \"miningMachineId\": 2,\n" +
                        "    \"characterTokenId\": \"ARK_002\",\n" +
                        "    \"miningCoefficient\": 0.7\n" +
                        "  }\n" +
                        "]"
                }
            };

            Commands["/admin_mining_update_rule"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON to update a mining rule (machine name and cost are\n" +
                        "regenerated automatically):\n\n" +
                        "{\n" +
                        "  \"miningRuleId\": 1,\n" +
                        "  \"miningCoefficient\": 0.9\n" +
                        "}"
                }
            };

            Commands["/admin_mining_delete_machine"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_id",
                    Question = "Enter mining machine Id to delete:"
                },
                new()
                {
                    Name = "confirm_delete",
                    Question = "Are you sure you want to PERMANENTLY delete this machine and its rules?",
                    Buttons = new List<QuickButton>
                    {
                        new() { Text = "✅ Yes, delete", Value = "confirm" },
                        new() { Text = "❌ No, cancel", Value = "cancel" }
                    }
                }
            };

            Commands["/admin_mining_deactivate_machine"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_id",
                    Question = "Enter mining machine Id to deactivate:"
                },
                new()
                {
                    Name = "confirm_deactivate",
                    Question = "Are you sure you want to deactivate this machine? It will no longer be available for purchase.",
                    Buttons = new List<QuickButton>
                    {
                        new() { Text = "✅ Yes, deactivate", Value = "confirm" },
                        new() { Text = "❌ No, cancel", Value = "cancel" }
                    }
                }
            };

            Commands["/admin_mining_delete_rule"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_id",
                    Question = "Enter mining rule Id to delete:"
                },
                new()
                {
                    Name = "confirm_delete",
                    Question = "Are you sure you want to delete this mining rule?",
                    Buttons = new List<QuickButton>
                    {
                        new() { Text = "✅ Yes, delete", Value = "confirm" },
                        new() { Text = "❌ No, cancel", Value = "cancel" }
                    }
                }
            };

            Commands["/admin_mining_update_global_rule"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON to update global mining rule (symbol required, others optional):\n\n" +
                        "{\n" +
                        "  \"symbol\": \"ARK_001\",\n" +
                        "  \"currentCoefficient\": 1.05,\n" +
                        "  \"futureCoefficient\": 0.95,\n" +
                        "  \"baseTokenMiningSpeed\": 50\n" +
                        "}"
                }
            };

            Commands["/admin_mining_app_state"] = new List<WizardStep>
            {
                new() { Name = "request", OneStep = true }
            };

            Commands["/admin_access_get"] = new List<WizardStep>
            {
                new() { Name = "request", OneStep = true }
            };

            Commands["/admin_access_set"] = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question =
                        "Send JSON:\n\n" +
                        "{\n" +
                        "  \"isGlobalAccessEnabled\": true,\n" +
                        "  \"whiteList\": [123, 456],\n" +
                        "  \"blackList\": [789]\n" +
                        "}"
                }
            };
        }
    }
}
