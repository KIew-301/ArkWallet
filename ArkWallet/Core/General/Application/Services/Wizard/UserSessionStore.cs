using ArkWallet.Core.General.Domain.ValueObjects;
using System.Collections.Concurrent;

namespace ArkWallet.Core.General.Application.Services.Wizard
{
    /// <summary>Хранит пользовательские сессии мастера в памяти (ConcurrentDictionary).</summary>
    public class UserSessionStore : IUserSessionStore
    {
        private readonly ConcurrentDictionary<long, UserSession> _sessions = new();

        /// <summary>Возвращает сессию пользователя, если она существует.</summary>
        public bool TryGet(long userId, out UserSession? session)
        {
            return _sessions.TryGetValue(userId, out session);
        }

        /// <summary>Сохраняет сессию пользователя.</summary>
        public void Set(long userId, UserSession session)
        {
            _sessions[userId] = session;
        }

        /// <summary>Удаляет сессию пользователя и возвращает true, если она была.</summary>
        public bool Remove(long userId)
        {
            return _sessions.TryRemove(userId, out _);
        }
    }
}
