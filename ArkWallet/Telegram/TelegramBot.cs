using ArkWallet.Domain.Wizard;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ArkWallet.Telegram
{
    internal partial class TelegramBot
    {
        // Интерфейс для взаимодействия с ботом
        ITelegramBotClient botClient;

        // Взаимодействие с системой
        private WizardEngine _wizardEngine;

        public TelegramBot(WizardEngine wizardEngine)
        {
            _instance = this;
            _wizardEngine = wizardEngine;
        }

        public async Task Start()
        {
            ConfigurationService configurationService = new();
            await LoadConfiguration(configurationService);
            string token = await configurationService.GetToken();

            _ = LaunchBot(token);
        }

        private async Task LaunchBot(string token)
        {
            using var cts = new CancellationTokenSource();

            botClient = new TelegramBotClient(token);

            ReceiverOptions receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cts.Token
            );

            var me = await botClient.GetMe();

            await SetCommandList(CommandListType.SimpleMode);

            Console.WriteLine($"Start listening");
            Console.ReadLine();

            cts.Cancel();
        }

        static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Message is not { } message)
                return;

            // Only process text messages
            if (message.Text is not { } messageText)
                return;

            var chatId = message.Chat.Id;

            Console.WriteLine($"Received message in chat.");

            if (Instance.IsAuthorizedUser(chatId))
            {
                var (answer, buttons) = await Instance._wizardEngine.ProcessInput(chatId, messageText);

                if (!string.IsNullOrEmpty(answer))
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: answer,
                        cancellationToken: cancellationToken
                    );
                }
            }
        }

        static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }
    }
}
