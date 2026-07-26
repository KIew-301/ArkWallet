using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Entities.Configurations
{
    partial class WizardConfiguration
    {
        private void ConfigureRegistrationAdditionCommand()
        {
            var jsonStep = new List<WizardStep>
            {
                new() { Name = "set_options", Question = "json" }
            };

            Commands["/admin_create_token"] = jsonStep;
            Commands["/admin_set_token_to_user"] = jsonStep;
            Commands["/admin_add_balance_to_user"] = jsonStep;
            Commands["/admin_update_token_media"] = jsonStep;

            var helpStep = new List<WizardStep>
            {
                new() { Name = "request", OneStep = true }
            };

            Commands["/admin_help"] = helpStep;
        }
    }
}
