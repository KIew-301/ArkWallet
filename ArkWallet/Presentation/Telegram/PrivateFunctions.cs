using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Presentation.Telegram
{
    [ExcludeFromCodeCoverage(Justification = "Инфраструктурный сервис конфигурации, зависит от IConfiguration. Тестируется интеграционно.")]
    internal class ConfigurationService(IConfiguration configuration)
    {
        static readonly string telegramBotName = "Main";

        public string GetToken()
        {
            string? token = configuration[$"Telegram:BotToken:{telegramBotName}"];

            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Telegram bot token is not configured");

            return token;
        }

        public long GetPAId()
        {
            long adminChatId = long.Parse(configuration[$"Telegram:AdminId:Main"] ?? "0");
            return adminChatId;
        }

        public long GetSAId()
        {
            long adminChatId = long.Parse(configuration[$"Telegram:AdminId:Addition"] ?? "0");
            return adminChatId;
        }

        public long GetTAId()
        {
            long adminChatId = long.Parse(configuration[$"Telegram:AdminId:AdditionThird"] ?? "0");
            return adminChatId;
        }

        public HashSet<long> GetAllowedUserIds()
        {
            var ids = new HashSet<long>();
            string? raw = configuration[$"Telegram:AllowedUserIds"];

            if (string.IsNullOrWhiteSpace(raw))
                return ids;

            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (long.TryParse(part, out var id))
                    ids.Add(id);
            }

            return ids;
        }
    }
}
