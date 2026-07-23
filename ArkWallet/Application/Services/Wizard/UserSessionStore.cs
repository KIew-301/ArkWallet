using ArkWallet.Domain.ValueObjects;
using System.Collections.Concurrent;

namespace ArkWallet.Application.Services.Wizard
{
    public class UserSessionStore : IUserSessionStore
    {
        private readonly ConcurrentDictionary<long, UserSession> _sessions = new();

        public bool TryGet(long userId, out UserSession? session)
        {
            return _sessions.TryGetValue(userId, out session);
        }

        public void Set(long userId, UserSession session)
        {
            _sessions[userId] = session;
        }

        public bool Remove(long userId)
        {
            return _sessions.TryRemove(userId, out _);
        }
    }
}
