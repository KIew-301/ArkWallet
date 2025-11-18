using ArkWallet.Infrastructure.Wizard;
using ArkWallet.Presentation.Telegram;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

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
            try
            {
                if (update.Message is { } message && message.Text is { } messageText)
                {
                    var chatId = message.Chat.Id;
                    Console.WriteLine($"Received text message");
                    await ProcessUserInput(botClient, chatId, messageText, cancellationToken);
                }

                else if (update.CallbackQuery is { } callbackQuery)
                {
                    await HandleCallbackQuery(botClient, callbackQuery, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await Instance.SendMessageToAdmin($"{ex.Message}\n{ex.StackTrace}");
                Console.WriteLine($"{ex.Message}\n{ex.StackTrace}");
            }
        }

        static async Task HandleCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var callbackData = callbackQuery.Data;
            var messageId = callbackQuery.Message.MessageId;

            try
            {
                Console.WriteLine($"Received callback");

                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);

                var (answer, buttons, addition) = await Instance._wizardEngine.ProcessInput(chatId, callbackData);

                if (Instance.IsAuthorizedUser(chatId))
                {
                    if (buttons != null && buttons.Any())
                    {
                        var inlineButtons = buttons.Select(btn =>
                            new[] { InlineKeyboardButton.WithCallbackData(btn.Text, btn.Value ?? btn.Text) }
                        );
                        var inlineMarkup = new InlineKeyboardMarkup(inlineButtons);

                        await botClient.EditMessageText(
                            chatId: chatId,
                            messageId: messageId,
                            text: answer,
                            replyMarkup: inlineMarkup,
                            cancellationToken: cancellationToken
                        );
                    }
                    else
                    {
                        await botClient.EditMessageText(
                            chatId: chatId,
                            messageId: messageId,
                            text: answer,
                            cancellationToken: cancellationToken
                        );
                    }

                    if (addition != null && addition.Count > 0)
                        foreach (var add in addition)
                            await Instance.SendMessageToUser(add.Key, add.Value);
                }
            }
            catch
            {
                await botClient.EditMessageText(
                            chatId: chatId,
                            messageId: messageId,
                            text: "Ошибка в системе.",
                            cancellationToken: cancellationToken
                        );

                throw;
            }
        }

        static async Task ProcessUserInput(ITelegramBotClient botClient, long chatId, string input, CancellationToken cancellationToken)
        {
            try
            {
                if (Instance.IsAuthorizedUser(chatId))
                {
                    var (answer, buttons, addition) = await Instance._wizardEngine.ProcessInput(chatId, input);

                    if (string.IsNullOrEmpty(answer)) return;

                    if (buttons != null && buttons.Any())
                    {
                        var inlineButtons = buttons.Select(btn =>
                            new[] { InlineKeyboardButton.WithCallbackData(btn.Text, btn.Value ?? btn.Text) }
                        );
                        var inlineMarkup = new InlineKeyboardMarkup(inlineButtons);

                        await botClient.SendMessage(
                            chatId: chatId,
                            text: answer,
                            replyMarkup: inlineMarkup,
                            cancellationToken: cancellationToken
                        );
                    }
                    else
                    {
                        await botClient.SendMessage(
                            chatId: chatId,
                            text: answer,
                            cancellationToken: cancellationToken
                        );
                    }

                    if (addition != null && addition.Count > 0)
                        foreach (var add in addition)
                            await Instance.SendMessageToUser(add.Key, add.Value);
                }
            }
            catch
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Ошибка в системе.",
                    cancellationToken: cancellationToken
                );

                throw;
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
