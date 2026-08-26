using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Infrastructure.AccessControl;
using ArkWallet.Presentation.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ArkWallet.Telegram
{
    internal partial class TelegramBot : IMessageSender
    {
        private AccessControlService _accessControl = null!;
        private long _primaryAdminId;
        private string? _botUsername;

        private void LoadConfiguration(ConfigurationService configurationService, AccessControlService accessControl)
        {
            _primaryAdminId = configurationService.GetPAId();
            long SAId = configurationService.GetSAId();
            long TAId = configurationService.GetTAId();
            _accessControl = accessControl;
            _accessControl.LoadFromConfiguration(new HashSet<long> { _primaryAdminId, SAId, TAId });
        }

        private enum CommandListType
        {
            SimpleMode,
            TrainingMode,
            HelperMode,
        }

        private async Task SetCommandList(CommandListType type)
        {
            List<BotCommand> privateCommands;
            List<BotCommand> groupCommands;

            switch (type)
            {
                case CommandListType.SimpleMode:
                    privateCommands = new List<BotCommand>()
                    {
                        new("/start", "Зарегистрироваться."),
                        new("/place_order", "Открыть ордер."),
                        new("/cancel_order", "Отменить ордер."),
                        new("/cancel_all_orders", "Отменить все активные ордера."),
                        new("/get_profile", "Получить данные профиля."),
                        new("/get_token_info", "Получить информацию о токене."),
                        new("/get_price_history", "Получить историю цен токена."),
                        new("/get_order_book", "Получить стакан ордеров токена."),
                        new("/get_orders", "Мои активные ордера."),
                        new("/get_tokens", "Все токены с ценами."),
                        new("/get_trades", "Мои последние сделки."),
                        new("/get_tops", "Рейтинг трейдеров."),
                        new("/top", "Топ-10 трейдеров (быстро)."),
                        new("/trades", "Последние 10 сделок (быстро)."),
                        new("/mining_rules", "Правила майнинга."),
                        new("/mining_machines", "Майнеры на продажу."),
                        new("/mining_slots", "Мои слоты."),
                        new("/mining_buy", "Купить майнер."),
                        new("/mining_switch", "Сменить токен слота."),
                        new("/mining_take", "Забрать токены."),
                        new("/mining_sell", "Продать слот."),
                    };

                    groupCommands = new List<BotCommand>()
                    {
                        new("/get_profile", "Получить данные профиля."),
                        new("/get_tokens", "Все токены с ценами."),
                        new("/get_orders", "Мои активные ордера."),
                        new("/mining_rules", "Правила майнинга."),
                        new("/mining_machines", "Майнеры на продажу."),
                        new("/mining_slots", "Мои слоты."),
                    };
                    break;

                default:
                    privateCommands = new List<BotCommand>();
                    groupCommands = new List<BotCommand>();
                    break;
            }

            await botClient.SetMyCommands(
                commands: privateCommands,
                scope: BotCommandScope.AllPrivateChats(),
                languageCode: null);

            await botClient.SetMyCommands(
                commands: groupCommands,
                scope: BotCommandScope.AllGroupChats(),
                languageCode: null);
        }

        private string AssembleCaption(string caption)
        {
            string newCaption = caption ?? "📊 Данные системы";
            newCaption += $"\n📅 {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

            return newCaption;
        }

        private bool IsAuthorizedUser(long Id)
            => _accessControl.IsAuthorized(Id);

        private bool IsAllowedGroup(long Id)
            => _accessControl.IsGroupAuthorized(Id);

        private async Task<string> GetBotUsernameAsync(ITelegramBotClient botClient)
            => _botUsername ??= (await botClient.GetMe()).Username ?? string.Empty;

        public async Task SendMessageToAdmin(string message)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            try
            {
                await botClient.SendMessage(
                    chatId: _primaryAdminId,
                    text: message,
                    cancellationToken: cts.Token
                );
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Вышло время отправки сообщения");
            }

        }

        public async Task SendMessageToUser(long chatId, string message)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            try
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: message,
                    cancellationToken: cts.Token
                );
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Вышло время отправки сообщения");
            }

        }

        public async Task SendMessageWithMedia(string message, List<MemoryStream> streams)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            var mediaGroup = new List<IAlbumInputMedia>();

            for (int i = 0; i < streams.Count; i++)
            {
                // Перематываем поток на начало
                streams[i].Seek(0, SeekOrigin.Begin);

                // Создаем медиа-объект
                var media = new InputMediaPhoto(
                        media: new InputFileStream(streams[i], $"image_{i}.png")
                    )
                {
                    // Подпись будет только у первого изображения
                    Caption = i == 0 ? message : null
                };

                mediaGroup.Add(media);
            }

            await botClient.SendMediaGroup(
                chatId: _primaryAdminId,
                media: mediaGroup,
                cancellationToken: cts.Token
            );
        }

        public async Task SendJsonFile(string jsonData, string caption = null)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            try
            {
                string tempFile = Path.GetRandomFileName() + ".json";
                await File.WriteAllTextAsync(tempFile, jsonData);

                // Формируем сообщение
                caption = AssembleCaption(caption);

                // Отправляем файл
                using (var fileStream = File.OpenRead(tempFile))
                {
                    await botClient.SendDocument(
                        chatId: _primaryAdminId,
                        document: InputFile.FromStream(fileStream, "data.json"),
                        caption: caption,
                        cancellationToken: cts.Token
                    );
                }

                File.Delete(tempFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки: {ex.Message}");
            }
        }

        public async Task SendMessageAsync(long chatId, string message)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: message,
                    cancellationToken: cts.Token
                );
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("SendMessageAsync timed out");
            }
        }

    }
}
