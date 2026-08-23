using ArkWallet.Infrastructure.Data;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Infrastructure.AccessControl;

/// <summary>
/// Thread-safe service for managing user access control (whitelist, blacklist, global access).
/// Shared between Telegram bot and API layer.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Infrastructure service: thin wrapper over in-memory state with thread synchronization. Tested through integration.")]
public class AccessControlService
{
    private readonly object _lock = new();
    private AccessSetting _setting = AccessSetting.Create();
    private HashSet<long> _adminIds = new();

    /// <summary>Admin user IDs that bypass all access checks.</summary>
    public IReadOnlyCollection<long> AdminIds => _adminIds;

    /// <summary>Loads admin IDs from configuration at startup.</summary>
    public void LoadFromConfiguration(IEnumerable<long> adminIds)
    {
        lock (_lock)
        {
            _adminIds = new HashSet<long>(adminIds);
        }
    }

    /// <summary>Loads access settings from database at startup.</summary>
    public void LoadFromDb(AccessSetting setting)
    {
        lock (_lock)
        {
            _setting = setting;
        }
    }

    /// <summary>Returns current access settings snapshot.</summary>
    public AccessSetting GetSetting()
    {
        lock (_lock)
        {
            return _setting;
        }
    }

    /// <summary>Updates access settings in memory (caller persists to DB).</summary>
    public void UpdateSetting(AccessSetting setting)
    {
        lock (_lock)
        {
            _setting = setting;
        }
    }

    /// <summary>Returns true if user is in admin list.</summary>
    public bool IsAdmin(long userId)
    {
        lock (_lock)
        {
            return _adminIds.Contains(userId);
        }
    }

    /// <summary>Returns true if user is authorized (admin, whitelisted, or global access enabled).</summary>
    public bool IsAuthorized(long userId)
    {
        lock (_lock)
        {
            if (_adminIds.Contains(userId))
                return true;

            if (_setting.WhiteList.Contains(userId))
                return true;

            if (_setting.BlackList.Contains(userId))
                return false;

            return _setting.IsGlobalAccessEnabled;
        }
    }

    /// <summary>Returns true if group chat is allowed (whitelisted, or group access enabled and not blacklisted).</summary>
    public bool IsGroupAuthorized(long chatId)
    {
        lock (_lock)
        {
            if (_setting.GroupWhiteList.Contains(chatId))
                return true;

            if (_setting.GroupBlackList.Contains(chatId))
                return false;

            return _setting.IsGroupAccessEnabled;
        }
    }

    /// <summary>Returns formatted string of current access settings for display.</summary>
    public string FormatSetting()
    {
        lock (_lock)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Access Setting ===");
            sb.AppendLine($"Global access: {(_setting.IsGlobalAccessEnabled ? "ON" : "OFF")}");
            sb.AppendLine($"White list ({_setting.WhiteList.Count}): {string.Join(", ", _setting.WhiteList)}");
            sb.AppendLine($"Black list ({_setting.BlackList.Count}): {string.Join(", ", _setting.BlackList)}");
            sb.AppendLine($"Group access: {(_setting.IsGroupAccessEnabled ? "ON" : "OFF")}");
            sb.AppendLine($"Group white list ({_setting.GroupWhiteList.Count}): {string.Join(", ", _setting.GroupWhiteList)}");
            sb.AppendLine($"Group black list ({_setting.GroupBlackList.Count}): {string.Join(", ", _setting.GroupBlackList)}");
            return sb.ToString();
        }
    }
}
