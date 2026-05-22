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

            Commands["/admincreatetoken"] = steps;

            steps =
            [
                new()
                {
                    Name = "set_options",
                    Question = "json",
                }
            ];

            Commands["/adminaddtokentouser"] = steps;

            steps =
            [
                new()
                {
                    Name = "set_options",
                    Question = "json",
                }
            ];

            Commands["/adminaddbalancetouser"] = steps;
        }
    }
}
