using ArkWallet.Domain.ValueObjects;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Infrastructure.Wizard
{
    [ExcludeFromCodeCoverage(Justification = "Хранилище сессий пользователей Wizard — инфраструктурный компонент, управляет состоянием Telegram-бота. Тестируется интеграционно.")]
    public class UserSessionStore
    {
        private readonly ConcurrentDictionary<long, UserSession> _sessions = new();

        public ConcurrentDictionary<long, UserSession> Sessions => _sessions;
    }
}
