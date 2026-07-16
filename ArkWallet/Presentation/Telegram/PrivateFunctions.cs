using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Presentation.Telegram
{
    [ExcludeFromCodeCoverage(Justification = "Инфраструктурный сервис конфигурации, зависит от IConfiguration. Тестируется интеграционно.")]
    internal class ConfigurationService
    {
        private readonly IConfiguration _configuration;
        static readonly string telegramBotName = "Main";

        public ConfigurationService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<string?> GetToken()
        {
            string? token = _configuration[$"Telegram:BotToken:{telegramBotName}"];
            return Task.FromResult(token);
        }

        public Task<long> GetPAId()
        {
            var value = _configuration["Telegram:AdminId:Main"];
            return Task.FromResult(long.TryParse(value, out var id) ? id : -1);
        }

        public Task<long> GetSAId()
        {
            var value = _configuration["Telegram:AdminId:Addition"];
            return Task.FromResult(long.TryParse(value, out var id) ? id : -1);
        }

        public Task<long> GetTAId()
        {
            var value = _configuration["Telegram:AdminId:AdditionThird"];
            return Task.FromResult(long.TryParse(value, out var id) ? id : -1);
        }
    }
}
