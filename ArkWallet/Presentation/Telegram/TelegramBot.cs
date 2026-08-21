using ArkWallet.Application.Contracts.Other;
using ArkWallet.Infrastructure.AccessControl;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Wizard;
using ArkWallet.Presentation.Telegram;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ArkWallet.Telegram
{
    [ExcludeFromCodeCoverage(Justification = "Telegram-бот: точка входа Telegram API, зависит от внешнего клиента и polling. Тестируется интеграционно.")]
    internal partial class TelegramBot(IServiceProvider serviceProvider) : IMessageSender
    {
        // Интерфейс для взаимодействия с ботом
        ITelegramBotClient botClient = null!;

        public async Task Start()
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var accessControl = serviceProvider.GetRequiredService<AccessControlService>();
            ConfigurationService configurationService = new(configuration);
            LoadConfiguration(configurationService, accessControl);
            string token = configurationService.GetToken();

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
            await Task.Delay(Timeout.Infinite, cts.Token).ConfigureAwait(false);
        }

        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
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
                Console.WriteLine($"Unhandled error: {ex.Message}");
            }
        }

        async Task HandleCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var callbackData = callbackQuery.Data;
            var messageId = callbackQuery.Message.MessageId;

            try
            {
                using var scope = serviceProvider.CreateScope();
                var wizardEngine = scope.ServiceProvider.GetRequiredService<WizardEngine>();

                Console.WriteLine($"Received callback");

                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);

                if (callbackData == null) return;
                var result = await wizardEngine.ProcessInput(chatId, callbackData);

                if (IsAuthorizedUser(chatId))
                {
                    if (!string.IsNullOrEmpty(result.SentFilePath))
                    {
                        await SendDocumentFromFile(chatId, result.SentFilePath, result.Message, cancellationToken);
                    }
                    else if (result.Buttons != null && result.Buttons.Any())
                    {
                        var inlineButtons = result.Buttons.Select(btn =>
                            new[] { InlineKeyboardButton.WithCallbackData(btn.Text, btn.Value ?? btn.Text) }
                        );
                        var inlineMarkup = new InlineKeyboardMarkup(inlineButtons);

                        await botClient.EditMessageText(
                            chatId: chatId,
                            messageId: messageId,
                            text: result.Message,
                            replyMarkup: inlineMarkup,
                            cancellationToken: cancellationToken
                        );
                    }
                    else
                    {
                        await botClient.EditMessageText(
                            chatId: chatId,
                            messageId: messageId,
                            text: result.Message,
                            cancellationToken: cancellationToken
                        );
                    }
                }
            }
            catch (ApiRequestException apiEx) when (apiEx.Message.Contains("message is not modified"))
            {
                await botClient.AnswerCallbackQuery(
                    callbackQuery.Id,
                    text: "Данные уже актуальны",
                    cancellationToken: cancellationToken
                );
            }
            catch
            {
                await botClient.EditMessageText(
                            chatId: chatId,
                            messageId: messageId,
                            text: "Ошибка в системе.",
                            cancellationToken: cancellationToken
                        );
            }
        }

        async Task ProcessUserInput(ITelegramBotClient botClient, long chatId, string input, CancellationToken cancellationToken)
        {
            try
            {
                if (IsAuthorizedUser(chatId))
                {
                    using var scope = serviceProvider.CreateScope();
                    var wizardEngine = scope.ServiceProvider.GetRequiredService<WizardEngine>();

                    var result = await wizardEngine.ProcessInput(chatId, input);

                    if (string.IsNullOrEmpty(result.Message)) return;

                    if (!string.IsNullOrEmpty(result.SentFilePath))
                    {
                        await SendDocumentFromFile(chatId, result.SentFilePath, result.Message, cancellationToken);
                    }
                    else if (result.Buttons != null && result.Buttons.Any())
                    {
                        var inlineButtons = result.Buttons.Select(btn =>
                            new[] { InlineKeyboardButton.WithCallbackData(btn.Text, btn.Value ?? btn.Text) }
                        );
                        var inlineMarkup = new InlineKeyboardMarkup(inlineButtons);

                        await botClient.SendMessage(
                            chatId: chatId,
                            text: result.Message,
                            replyMarkup: inlineMarkup,
                            cancellationToken: cancellationToken
                        );
                    }
                    else
                    {
                        await botClient.SendMessage(
                            chatId: chatId,
                            text: result.Message,
                            cancellationToken: cancellationToken
                        );
                    }
                }
            }
            catch
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Ошибка в системе.",
                    cancellationToken: cancellationToken
                );
            }
        }

        private async Task SendDocumentFromFile(long chatId, string filePath, string? caption, CancellationToken cancellationToken)
        {
            try
            {
                using var fileStream = File.OpenRead(filePath);
                var fileName = Path.GetFileName(filePath);

                await botClient.SendDocument(
                    chatId: chatId,
                    document: InputFile.FromStream(fileStream, fileName),
                    caption: caption,
                    cancellationToken: cancellationToken
                );

                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending file: {ex.Message}");
                await botClient.SendMessage(
                    chatId: chatId,
                    text: caption ?? "Данные получены.",
                    cancellationToken: cancellationToken
                );
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
