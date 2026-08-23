using ArkWallet.Application.Contracts.Other;
using ArkWallet.Infrastructure.AccessControl;
using ArkWallet.Infrastructure.Wizard;
using ArkWallet.Presentation.Telegram;
using System.Diagnostics.CodeAnalysis;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

using ChatTypeDomain = ArkWallet.Domain.ValueObjects.ChatType;
using ChatTypeTelegram = Telegram.Bot.Types.Enums.ChatType;

namespace ArkWallet.Telegram
{
    [ExcludeFromCodeCoverage(Justification = "Telegram-бот: точка входа Telegram API, зависит от внешнего клиента и polling. Тестируется интеграционно.")]
    internal partial class TelegramBot(IServiceProvider serviceProvider, ILogger<TelegramBot> logger) : IMessageSender
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
                    var telegramChatType = message.Chat.Type;
                    var userId = message.From?.Id ?? 0;
                    var username = message.From?.Username ?? "без юзернейма";
                    var isGroup = telegramChatType == ChatTypeTelegram.Group || telegramChatType == ChatTypeTelegram.Supergroup;

                    logger.LogInformation(
                        "📩 Сообщение | UserId: {UserId} | Username: @{Username} | ChatId: {ChatId} | ChatType: {ChatType} | Text: {Text}",
                        userId,
                        username,
                        chatId,
                        telegramChatType,
                        messageText
                    );

                    if (isGroup && !IsAllowedGroup(chatId))
                    {                         
                        logger.LogWarning(
                            "⛔ Неавторизованная группа | ChatId: {ChatId} | UserId: {UserId} | Username: @{Username}",
                            chatId,
                            userId,
                            username
                        );
                        return;
                    }

                    string processedText = messageText;

                    if (isGroup)
                    {
                        var botMention = $"@{await GetBotUsernameAsync(botClient)}";

                        if (processedText.Contains(botMention))
                        {
                            processedText = processedText.Replace(botMention, "").Trim();
                        }

                        logger.LogInformation(
                            "🔄 Группа | Оригинал: {Original} | Обработано: {Processed} | Отправитель: {UserId}",
                            messageText,
                            processedText,
                            userId
                        );
                    }

                    await ProcessUserInput(botClient, chatId, processedText, userId, cancellationToken, telegramChatType);
                }
                else if (update.CallbackQuery is { } callbackQuery && callbackQuery.Message != null)
                {
                    var userId = callbackQuery.From.Id;
                    var chatId = callbackQuery.Message.Chat.Id;
                    var data = callbackQuery.Data ?? "без данных";

                    logger.LogInformation(
                        "🔄 Callback | UserId: {UserId} | ChatId: {ChatId} | Data: {Data}",
                        userId,
                        chatId,
                        data
                    );

                    await HandleCallbackQuery(botClient, callbackQuery, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in HandleUpdateAsync");
                Console.WriteLine($"Unhandled error: {ex.Message}");
            }
        }

        async Task HandleCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var chatType = callbackQuery.Message.Chat.Type;
            var callbackData = callbackQuery.Data;
            var messageId = callbackQuery.Message.MessageId;
            var userId = callbackQuery.From.Id;

            var chatTypeEnum = chatType switch
            {
                ChatTypeTelegram.Private => ChatTypeDomain.Private,
                ChatTypeTelegram.Group => ChatTypeDomain.Group,
                ChatTypeTelegram.Supergroup => ChatTypeDomain.Supergroup,
                _ => ChatTypeDomain.Private
            };

            logger.LogInformation(
                "📨 Callback обработка | UserId: {UserId} | ChatId: {ChatId} | Data: {Data}",
                userId,
                chatId,
                callbackData ?? "null"
            );

            try
            {
                using var scope = serviceProvider.CreateScope();
                var wizardEngine = scope.ServiceProvider.GetRequiredService<WizardEngine>();

                Console.WriteLine($"Received callback");

                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);

                if (callbackData == null) return;
                var result = await wizardEngine.ProcessInput(userId, callbackData, chatTypeEnum);

                logger.LogInformation(
                    "📤 Callback результат | UserId: {UserId} | Message: {Message} | HasButtons: {HasButtons}",
                    userId,
                    result.Message?.Length > 100 ? result.Message.Substring(0, 100) + "..." : result.Message,
                    result.Buttons != null && result.Buttons.Any()
                );

                if (IsAuthorizedUser(userId))
                {
                    if (!string.IsNullOrEmpty(result.SentFilePath))
                    {
                        await SendDocumentFromFile(chatId, result.SentFilePath, result.Message, cancellationToken);
                    }
                    else if (result.Buttons != null && result.Buttons.Any())
                    {
                        // Кнопки уже отфильтрованы в WizardEngine для групповых чатов
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
                else
                {
                    logger.LogWarning("⛔ Неавторизованный пользователь | UserId: {UserId} | ChatId: {ChatId}", userId, chatId);
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
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing callback query | UserId: {UserId} | ChatId: {ChatId}", userId, chatId);

                await botClient.EditMessageText(
                            chatId: chatId,
                            messageId: messageId,
                            text: "Ошибка в системе.",
                            cancellationToken: cancellationToken
                        );
            }
        }

        async Task ProcessUserInput(ITelegramBotClient botClient, long chatId, string input, long userId, CancellationToken cancellationToken, ChatTypeTelegram chatTypeEnum)
        {
            var chatType = chatTypeEnum switch
            {
                ChatTypeTelegram.Private => ChatTypeDomain.Private,
                ChatTypeTelegram.Group => ChatTypeDomain.Group,
                ChatTypeTelegram.Supergroup => ChatTypeDomain.Supergroup,
                _ => ChatTypeDomain.Private
            };

            logger.LogInformation(
                "📥 ProcessUserInput | UserId: {UserId} | ChatId: {ChatId} | Input: {Input}",
                userId,
                chatId,
                input.Length > 50 ? input.Substring(0, 50) + "..." : input
            );

            try
            {
                if (IsAuthorizedUser(userId))
                {
                    using var scope = serviceProvider.CreateScope();
                    var wizardEngine = scope.ServiceProvider.GetRequiredService<WizardEngine>();

                    var result = await wizardEngine.ProcessInput(userId, input, chatType);

                    logger.LogInformation(
                        "📤 ProcessUserInput результат | UserId: {UserId} | Message: {Message} | HasButtons: {HasButtons}",
                        userId,
                        result.Message?.Length > 100 ? result.Message.Substring(0, 100) + "..." : result.Message,
                        result.Buttons != null && result.Buttons.Any()
                    );

                    if (string.IsNullOrEmpty(result.Message)) return;

                    if (!string.IsNullOrEmpty(result.SentFilePath))
                    {
                        await SendDocumentFromFile(chatId, result.SentFilePath, result.Message, cancellationToken);
                    }
                    else if (result.Buttons != null && result.Buttons.Any())
                    {
                        // Кнопки уже отфильтрованы в WizardEngine для групповых чатов
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
                else
                {
                    logger.LogWarning("⛔ Неавторизованный чат | ChatId: {ChatId} | UserId: {UserId}", chatId, userId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing user input | UserId: {UserId} | ChatId: {ChatId}", userId, chatId);

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
