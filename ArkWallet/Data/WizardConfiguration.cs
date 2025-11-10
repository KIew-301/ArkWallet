using ArkWallet.ValueObjects;

namespace ArkWallet.Data
{
    internal class WizardConfiguration
    {
        public Dictionary<string, List<WizardStep>> Commands { get; } = new();

        public WizardConfiguration()
        {
            ConfigureRegistrationCommand();
        }

        private void ConfigureRegistrationCommand()
        {
            var steps = new List<WizardStep>
            {
                new()
                {
                    Name = "set_name",
                    Question = "Как вас будут звать?",
                }
            };

            Commands["/start"] = steps;
        }
    }
}
