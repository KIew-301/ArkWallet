using ArkWallet.Domain.ValueObjects;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Entities.Configurations
{
    [ExcludeFromCodeCoverage(Justification = "Конфигурация шагов wizard, статические данные без логики. Тестируется через интеграционные сценарии.")]
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
                    Question = "Какой токен вы хотите купить/продать? (выберите или напишите)",
                },
                new()
                {
                    Name = "set_quantity",
                    Question = "Сколько вы хотите купить/продать? (выберите или напишите)",
                },
                new()
                {
                    Name = "set_price",
                    Question = "По какой цене вы хотите исполнить ордер? (выберите или напишите свою)",
                }
            ];

            Commands["/place_order"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "select_order_to_cancel",
                    Question = "Какой ордер хотите отменить?"
                },
                new()
                {
                    Name = "confirm_cancellation",
                    Question = "Вы уверены что хотите отменить ордер?",
                    Buttons = [
                        new() { Text = "✅ Да, отменить", Value = "confirm" },
                        new() { Text = "❌ Нет, оставить", Value = "cancel" }
                    ]
                }
            };

            Commands["/cancel_order"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "confirm_cancellation",
                    Question = "Вы уверены что хотите отменить все активные ордера?",
                    Buttons = [
                        new() { Text = "✅ Да, отменить", Value = "confirm" },
                        new() { Text = "❌ Нет, оставить", Value = "cancel" }
                    ]
                }
            };

            Commands["/cancel_all_orders"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "request",
                    OneStep = true
                },
            };

            Commands["/get_profile"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "select_token",
                    Question = "Какой токен вы хотите посмотреть?",
                },
                new()
                {
                    Name = "show_info",
                    OneStep = true
                },
            };

            Commands["/get_token_info"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "select_token",
                    Question = "Какой токен вы хотите посмотреть?",
                },
                new()
                {
                    Name = "set_timeframe",
                    Question = "Какой шаг свечи (в минутах)?",
                    Buttons =
                    [
                        new() {Text = "5 мин", Value = "5"},
                        new() {Text = "15 мин", Value = "15"},
                        new() {Text = "1 час", Value = "60"},
                        new() {Text = "4 часа", Value = "240"},
                        new() {Text = "1 день", Value = "1440"}
                    ]
                },
                new()
                {
                    Name = "set_limit",
                    Question = "Сколько записей показать?",
                    Buttons =
                    [
                        new() {Text = "10", Value = "10"},
                        new() {Text = "25", Value = "25"},
                        new() {Text = "50", Value = "50"},
                        new() {Text = "100", Value = "100"}
                    ]
                },
            };

            Commands["/get_price_history"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "select_token",
                    Question = "Какой токен вы хотите посмотреть в стакане?",
                },
                new()
                {
                    Name = "set_buy_count",
                    Question = "Сколько ордеров на покупку показать?",
                    Buttons = CreateCountButtons()
                },
                new()
                {
                    Name = "set_sell_count",
                    Question = "Сколько ордеров на продажу показать?",
                    Buttons = CreateCountButtons()
                },
            };

            Commands["/get_order_book"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "request",
                    OneStep = true
                },
            };

            Commands["/get_orders"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "request",
                    OneStep = true
                },
            };

            Commands["/get_tokens"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "set_limit",
                    Question = "Сколько последних сделок показать?",
                    Buttons =
                    [
                        new() {Text = "5", Value = "5"},
                        new() {Text = "10", Value = "10"},
                        new() {Text = "25", Value = "25"},
                        new() {Text = "50", Value = "50"}
                    ]
                },
            };

            Commands["/get_trades"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "set_limit",
                    Question = "Сколько трейдеров показать в рейтинге?",
                    Buttons =
                    [
                        new() {Text = "5", Value = "5"},
                        new() {Text = "10", Value = "10"},
                        new() {Text = "15", Value = "15"},
                        new() {Text = "20", Value = "20"}
                    ]
                },
            };

            Commands["/get_tops"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "request",
                    OneStep = true
                },
            };

            Commands["/mining_rules"] = new List<WizardStep>
            {
                new()
                {
                    Name = "request",
                    OneStep = true
                },
            };

            Commands["/mining_machines"] = new List<WizardStep>
            {
                new()
                {
                    Name = "request",
                    OneStep = true
                },
            };

            Commands["/mining_slots"] = new List<WizardStep>
            {
                new()
                {
                    Name = "request",
                    OneStep = true
                },
            };

            Commands["/mining_take_all"] = new List<WizardStep>
            {
                new()
                {
                    Name = "request",
                    OneStep = true
                },
            };

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "select_machine",
                    Question = "Введите идентификатор машины для покупки:"
                },
                new()
                {
                    Name = "confirm_buy",
                    Question = "Подтвердите покупку машины:",
                    Buttons =
                    [
                        new() { Text = "✅ Да, купить", Value = "confirm" },
                        new() { Text = "❌ Нет, отменить", Value = "cancel" }
                    ]
                },
            };

            Commands["/mining_buy"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "select_slot",
                    Question = "Введите идентификатор слота для переключения:"
                },
                new()
                {
                    Name = "select_token",
                    Question = "На какой токен переключить майнинг? (напишите символ)"
                },
                new()
                {
                    Name = "confirm_switch",
                    Question = "Подтвердите переключение майнинга:",
                    Buttons =
                    [
                        new() { Text = "✅ Да, переключить", Value = "confirm" },
                        new() { Text = "❌ Нет, отменить", Value = "cancel" }
                    ]
                },
            };

            Commands["/mining_switch"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "select_slot",
                    Question = "Введите идентификатор слота для снятия токенов:"
                },
                new()
                {
                    Name = "confirm_take",
                    Question = "Подтвердите снятие собранных токенов:",
                    Buttons =
                    [
                        new() { Text = "✅ Да, снять", Value = "confirm" },
                        new() { Text = "❌ Нет, отменить", Value = "cancel" }
                    ]
                },
            };

            Commands["/mining_take"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "select_slot",
                    Question = "Введите идентификатор слота для продажи:"
                },
                new()
                {
                    Name = "confirm_sell",
                    Question = "Подтвердите продажу слота:",
                    Buttons =
                    [
                        new() { Text = "✅ Да, продать", Value = "confirm" },
                        new() { Text = "❌ Нет, отменить", Value = "cancel" }
                    ]
                },
            };

            Commands["/mining_sell"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "request",
                    OneStep = true
                },
            };

            Commands["/get_gifts_list"] = steps;

            steps = new List<WizardStep>
            {
                new()
                {
                    Name = "request",
                    OneStep = true
                },
            };

            Commands["/collect_all_gifts"] = steps;
        }

        private static List<QuickButton> CreateCountButtons()
        {
            return new List<QuickButton>
            {
                new() {Text = "5", Value = "5"},
                new() {Text = "10", Value = "10"},
                new() {Text = "25", Value = "25"},
                new() {Text = "50", Value = "50"}
            };
        }
    }
}
