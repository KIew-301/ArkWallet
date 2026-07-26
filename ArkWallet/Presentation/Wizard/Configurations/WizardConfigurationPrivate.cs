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
        }
    }
}
