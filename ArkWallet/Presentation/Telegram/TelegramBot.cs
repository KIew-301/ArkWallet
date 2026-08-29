using ArkWallet.Application.Contracts.Other;
using ArkWallet.Infrastructure.AccessControl;
using ArkWallet.Infrastructure.Wizard;
using ArkWallet.Presentation.Telegram;
using System.Collections.Concurrent;
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

        // Обработка обновлений: Telegram.Bot выполняет handler'ы строго последовательно,
        // поэтому одна зависшая отправка блокировала всех пользователей. Планируем работу
        // в фоне с ограниченной параллельностью и сериализацией по чату — это безопасно
        // для состояния WizardEngine (на пользователя) и сохраняет порядок сообщений.
        static readonly int MaxConcurrentUpdates = 20;
        static readonly SemaphoreSlim UpdateConcurrency = new(MaxConcurrentUpdates, MaxConcurrentUpdates);
        readonly ConcurrentDictionary<long, SemaphoreSlim> _chatLocks = new();

        void ScheduleUpdate(long chatId, Func<Task> handler)
        {
            _ = Task.Run(async () =>
            {
                var chatLock = _chatLocks.GetOrAdd(chatId, _ => new SemaphoreSlim(1, 1));

                await UpdateConcurrency.WaitAsync();
                await chatLock.WaitAsync();
                try
                {
                    await handler();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled error in scheduled update for chat {ChatId}", chatId);
                }
                finally
                {
                    chatLock.Release();
                    UpdateConcurrency.Release();
                }
            });
        }

        // Надёжность отправки: короткий таймаут попытки + повтор с экспоненциальной задержкой.
        // Bot API иногда перестаёт отвечать на burst команд, поэтому каждый запрос
        // не должен блокировать обработку обновлений на весь HttpClient.Timeout (100с).
        static readonly TimeSpan SendAttemptTimeout = TimeSpan.FromSeconds(15);
        const int MaxSendAttempts = 3;

        async Task<T> SendWithRetryAsync<T>(Func<ITelegramBotClient, CancellationToken, Task<T>> send,
            CancellationToken updateToken)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(updateToken);
                    attemptCts.CancelAfter(SendAttemptTimeout);
                    return await send(botClient, attemptCts.Token);
                }
                catch (Exception ex) when (attempt < MaxSendAttempts && IsTransientFailure(ex) && !updateToken.IsCancellationRequested)
                {
                    TimeSpan delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt - 1), 4));

                    if (ex is ApiRequestException api && api.ErrorCode == 429)
                        delay = TimeSpan.FromSeconds(Math.Max(delay.TotalSeconds, api.Parameters?.RetryAfter ?? 2));

                    await Task.Delay(delay, updateToken);
                }
            }
        }

        Task SendWithRetryAsync(Func<ITelegramBotClient, CancellationToken, Task> send, CancellationToken updateToken)
            => SendWithRetryAsync<bool>(
                async (client, ct) =>
                {
                    await send(client, ct);
                    return true;
                },
                updateToken);

        static bool IsTransientFailure(Exception ex)
        {
            if (ex is OperationCanceledException)
                return true;

            if (ex is not RequestException requestException)
                return false;

            return requestException is not ApiRequestException apiException
                || apiException.ErrorCode is 429 or 500 or 502 or 503 or 504;
        }

        async Task SendMessageWithRetryAsync(long chatId, string text, InlineKeyboardMarkup? replyMarkup, CancellationToken updateToken)
        {
            await SendWithRetryAsync<Message>(
                (client, ct) => replyMarkup is null
                    ? client.SendMessage(chatId: chatId, text: text, cancellationToken: ct)
                    : client.SendMessage(chatId: chatId, text: text, replyMarkup: replyMarkup, cancellationToken: ct),
                updateToken);
        }

        async Task EditMessageWithRetryAsync(long chatId, int messageId, string text, InlineKeyboardMarkup? replyMarkup, CancellationToken updateToken)
        {
            try
            {
                await SendWithRetryAsync<Message>(
                    (client, ct) => replyMarkup is null
                        ? client.EditMessageText(chatId: chatId, messageId: messageId, text: text, cancellationToken: ct)
                        : client.EditMessageText(chatId: chatId, messageId: messageId, text: text, replyMarkup: replyMarkup, cancellationToken: ct),
                    updateToken);
            }
            catch (ApiRequestException apiEx) when (apiEx.Message.Contains("message is not modified"))
            {
                logger.LogDebug("Message {MessageId} was not modified", messageId);
            }
        }

        async Task AnswerCallbackWithRetryAsync(string callbackQueryId, CancellationToken updateToken)
        {
            await SendWithRetryAsync((client, ct) =>
                client.AnswerCallbackQuery(callbackQueryId, cancellationToken: ct), updateToken);
        }

        async Task SafeSendTextAsync(long chatId, string text, CancellationToken updateToken)
        {
            try
            {
                await SendMessageWithRetryAsync(chatId, text, null, updateToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Не удалось отправить сообщение в чат {ChatId}", chatId);
            }
        }

        async Task SafeEditMessageAsync(long chatId, int messageId, string text, InlineKeyboardMarkup? replyMarkup, CancellationToken updateToken)
        {
            try
            {
                await EditMessageWithRetryAsync(chatId, messageId, text, replyMarkup, updateToken);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Не удалось отредактировать сообщение {MessageId}", messageId);
            }
        }

        public async Task Start()
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var accessControl = serviceProvider.GetRequiredService<AccessControlService>();
            ConfigurationService configurationService = new(configuration);
            LoadConfiguration(configurationService, accessControl);
            string token = configurationService.GetToken();
            string? baseUrl = configurationService.GetBaseUrl();

            _ = LaunchBot(token, baseUrl);
        }

        private async Task LaunchBot(string token, string? baseUrl)
        {
            try
            {
                var options = new TelegramBotClientOptions(token, baseUrl: baseUrl);
                botClient = new TelegramBotClient(options);

                ReceiverOptions receiverOptions = new ReceiverOptions
                {
                    AllowedUpdates = Array.Empty<UpdateType>()
                };

                botClient.StartReceiving(
                    HandleUpdateAsync,
                    HandleErrorAsync,
                    receiverOptions
                );

                var me = await botClient.GetMe();
                Console.WriteLine($"Bot connected: @{me.Username} (ID: {me.Id})");

                await SetCommandList(CommandListType.SimpleMode);

                Console.WriteLine($"Start listening");
                await Task.Delay(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start Telegram bot");
                Console.WriteLine($"Bot start failed: {ex.Message}");
            }
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
                    long? replyToUserId = message.ReplyToMessage?.From?.Id;

                    logger.LogInformation(
                        "📩 Сообщение | UserId: {UserId} | Username: @{Username} | ChatId: {ChatId} | ChatType: {ChatType} | ReplyTo: {ReplyTo} | Text: {Text}",
                        userId,
                        username,
                        chatId,
                        telegramChatType,
                        replyToUserId,
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

                    ScheduleUpdate(chatId, () =>
                        ProcessUserInput(botClient, chatId, processedText, userId, cancellationToken, telegramChatType, replyToUserId));
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

                    ScheduleUpdate(chatId, () => HandleCallbackQuery(botClient, callbackQuery, cancellationToken));
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

                await AnswerCallbackWithRetryAsync(callbackQuery.Id, cancellationToken);

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

                        await EditMessageWithRetryAsync(chatId, messageId, result.Message, inlineMarkup, cancellationToken);
                    }
                    else
                    {
                        await EditMessageWithRetryAsync(chatId, messageId, result.Message, null, cancellationToken);
                    }
                }
                else
                {
                    logger.LogWarning("⛔ Неавторизованный пользователь | UserId: {UserId} | ChatId: {ChatId}", userId, chatId);
                }
            }
            catch (ApiRequestException apiEx) when (apiEx.Message.Contains("message is not modified"))
            {
                await AnswerCallbackWithRetryAsync(callbackQuery.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing callback query | UserId: {UserId} | ChatId: {ChatId}", userId, chatId);

                await SafeEditMessageAsync(chatId, messageId, "Ошибка в системе.", null, cancellationToken);
            }
        }

        async Task ProcessUserInput(ITelegramBotClient botClient, long chatId, string input, long userId, CancellationToken cancellationToken, ChatTypeTelegram chatTypeEnum, long? replyToUserId = null)
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

                    var result = await wizardEngine.ProcessInput(userId, input, chatType, replyToUserId);

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

                        await SendMessageWithRetryAsync(chatId, result.Message, inlineMarkup, cancellationToken);
                    }
                    else
                    {
                        await SendMessageWithRetryAsync(chatId, result.Message, null, cancellationToken);
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

                await SafeSendTextAsync(chatId, "Ошибка в системе.", cancellationToken);
            }
        }

        private async Task SendDocumentFromFile(long chatId, string filePath, string? caption, CancellationToken cancellationToken)
        {
            try
            {
                using var fileStream = File.OpenRead(filePath);
                var fileName = Path.GetFileName(filePath);

                await SendWithRetryAsync<Message>(
                    (client, ct) => client.SendDocument(
                        chatId: chatId,
                        document: InputFile.FromStream(fileStream, fileName),
                        caption: caption,
                        cancellationToken: ct
                    ),
                    cancellationToken
                );

                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending file: {ex.Message}");
                await SafeSendTextAsync(chatId, caption ?? "Данные получены.", cancellationToken);
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
