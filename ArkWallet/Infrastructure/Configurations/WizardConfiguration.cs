using ArkWallet.ValueObjects;

namespace ArkWallet.Entities.Configurations
{
    partial class WizardConfiguration
    {
        public Dictionary<string, List<WizardStep>> Commands { get; } = new();

        public WizardConfiguration()
        {
            ConfigureRegistrationCommand();
            ConfigureRegistrationAdditionCommand();
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

            steps =
            [
                new()
                {
                    Name = "set_direction",
                    Question = "Вы желаете КУПИТЬ или ПРОДАТЬ токен?",
                    Buttons =
                    [
                        new() {Text = "Купить", Value = "Купить"},
                        new() {Text = "Продать", Value = "Продать"}
                    ]
                },
                new()
                {
                    Name = "set_token",
                    Question = "Какой токен вы хотите выбрать? (выбрать из тех, которые у вас уже есть или введите новый)",
                },
                new()
                {
                    Name = "set_quantity",
                    Question = "Сколько вы хотите выбрать?",
                },
                new()
                {
                    Name = "set_price",
                    Question = "По какой цене вы хотите исполнить ордер? (выберите предложенные или напишите свою)",
                }
            ];

            Commands["/placeorder"] = steps;
        }
    }
}
