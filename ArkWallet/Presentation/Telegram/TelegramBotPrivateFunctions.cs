using ArkWallet.Presentation.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ArkWallet.Telegram
{
    internal partial class TelegramBot
    {
        // Рабочие Id
        long PAId;
        long SAId;
        long TAId;

        private async Task LoadConfiguration(ConfigurationService configurationService)
        {
            PAId = await configurationService.GetPAId();
            SAId = await configurationService.GetSAId();
            TAId = await configurationService.GetTAId();
        }

        private enum CommandListType
        {
            SimpleMode,
            TrainingMode,
            HelperMode,
        }

        private async Task SetCommandList(CommandListType type)
        {
            List<BotCommand> commands;

            switch (type)
            {
                case CommandListType.SimpleMode:
                    commands = new List<BotCommand>()
                    {
                        new("/start", "Зарегистрироваться."),
                        new("/placeorder", "Открыть ордер."),
                        new("/cancelorder", "Отменить ордер."),
                        new("/cancelallorders", "Отменить все активные ордера."),
                        new("/getprofile", "Получить данные профиля."),
                        new("/gettokeninfo", "Получить информацию о токене."),
                    };
                    break;

                default:
                    commands = new List<BotCommand>();
                    break;
            }

            await botClient.SetMyCommands(
                commands: commands,
                scope: BotCommandScope.Default(),
                languageCode: null);
        }

        private string AssembleCaption(string caption)
        {
            string newCaption = caption ?? "📊 Данные системы";
            newCaption += $"\n📅 {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

            return newCaption;
        }

        private bool IsAuthorizedUser(long Id)
        {
            bool result = Id == PAId || Id == SAId || Id == TAId;
            return result;
        }

        public async Task SendMessageToAdmin(string message)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            try
            {
                await botClient.SendMessage(
                    chatId: PAId,
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
                chatId: PAId,
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
                        chatId: PAId,
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

    }
}
