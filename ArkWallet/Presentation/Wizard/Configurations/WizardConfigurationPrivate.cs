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

            var botsActivitySteps = new List<WizardStep>
            {
                new() { Name = "select_token", Question = "Select token symbol to view bots:" },
                new() { Name = "show_bots", OneStep = true }
            };

            Commands["/admin_bots_activity"] = botsActivitySteps;

            var botsReconstructionStep = new List<WizardStep>
            {
                new() { Name = "set_options", Question = "json" }
            };

            Commands["/admin_bots_reconstruction"] = botsReconstructionStep;
        }
    }
}
