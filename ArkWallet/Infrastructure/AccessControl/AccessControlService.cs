using ArkWallet.Infrastructure.Data;

namespace ArkWallet.Infrastructure.AccessControl;

public class AccessControlService
{
    private readonly object _lock = new();
    private AccessSetting _setting = AccessSetting.Create();
    private HashSet<long> _adminIds = new();

    public IReadOnlyCollection<long> AdminIds => _adminIds;

    public void LoadFromConfiguration(IEnumerable<long> adminIds)
    {
        lock (_lock)
        {
            _adminIds = new HashSet<long>(adminIds);
        }
    }

    public void LoadFromDb(AccessSetting setting)
    {
        lock (_lock)
        {
            _setting = setting;
        }
    }

    public AccessSetting GetSetting()
    {
        lock (_lock)
        {
            return _setting;
        }
    }

    public void UpdateSetting(AccessSetting setting)
    {
        lock (_lock)
        {
            _setting = setting;
        }
    }

    public bool IsAdmin(long userId)
    {
        lock (_lock)
        {
            return _adminIds.Contains(userId);
        }
    }

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

    public string FormatSetting()
    {
        lock (_lock)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Access Setting ===");
            sb.AppendLine($"Global access: {(_setting.IsGlobalAccessEnabled ? "ON" : "OFF")}");
            sb.AppendLine($"White list ({_setting.WhiteList.Count}): {string.Join(", ", _setting.WhiteList)}");
            sb.AppendLine($"Black list ({_setting.BlackList.Count}): {string.Join(", ", _setting.BlackList)}");
            return sb.ToString();
        }
    }
}
