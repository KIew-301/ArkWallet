using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Entities.Configurations
{
    partial class WizardConfiguration
    {
        private void ConfigureRegistrationAdditionCommand()
        {
            var steps = new List<WizardStep>
            {
                new()
                {
                    Name = "set_options",
                    Question = "json",
                }
            };

            Commands["/admin_create_token"] = steps;

            steps =
            [
                new()
                {
                    Name = "set_options",
                    Question = "json",
                }
            ];

            Commands["/admin_set_token_to_user"] = steps;

            steps =
            [
                new()
                {
                    Name = "set_options",
                    Question = "json",
                }
            ];

            Commands["/admin_add_balance_to_user"] = steps;

            steps =
            [
                new()
                {
                    Name = "set_options",
                    Question = "json",
                }
            ];

            Commands["/admin_update_token_media"] = steps;
        }
    }
}
