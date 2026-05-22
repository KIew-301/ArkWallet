using Microsoft.Extensions.Configuration;

namespace ArkWallet.Presentation.Telegram
{
    public interface IConfigurationService
    {
        Task<string> GetToken();
        Task<long> GetPAId();
        Task<long> GetSAId();
        Task<long> GetTAId();
    }

    internal class ConfigurationService : IConfigurationService
    {
        static readonly string telegramBotName = "Main";

        public async Task<string> GetToken()
        {
            try
            {
                var configuration = new ConfigurationBuilder()
                    .AddUserSecrets<Program>()
                    .Build();

                string? token = configuration[$"Telegram:BotToken:{telegramBotName}"];

                if (string.IsNullOrEmpty(token))
                    throw new Exception($"TError");

                return token;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки конфигурации: {ex.Message}");
                return null;
            }
        }

        public async Task<long> GetPAId()
        {
            try
            {
                var configuration = new ConfigurationBuilder()
                    .AddUserSecrets<Program>()
                    .Build();

                long adminChatId = long.Parse(configuration[$"Telegram:AdminId:Main"]);

                if (adminChatId == 0)
                    throw new Exception($"AError");

                return adminChatId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки конфигурации: {ex.Message}");
                return -1;
            }
        }

        public async Task<long> GetSAId()
        {
            try
            {
                var configuration = new ConfigurationBuilder()
                    .AddUserSecrets<Program>()
                    .Build();

                long adminChatId = long.Parse(configuration[$"Telegram:AdminId:Addition"]);

                if (adminChatId == 0)
                    throw new Exception($"AError");

                return adminChatId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки конфигурации: {ex.Message}");
                return -1;
            }
        }

        public async Task<long> GetTAId()
        {
            try
            {
                var configuration = new ConfigurationBuilder()
                    .AddUserSecrets<Program>()
                    .Build();

                long adminChatId = long.Parse(configuration[$"Telegram:AdminId:AdditionThird"]);

                if (adminChatId == 0)
                    throw new Exception($"AError");

                return adminChatId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки конфигурации: {ex.Message}");
                return -1;
            }
        }
    }
}
