using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Infrastructure.Data;

/// <summary>
/// Single-row entity storing global access control configuration.
/// </summary>
public class AccessSetting
{
    /// <summary>Singleton key.</summary>
    [Key]
    public string Key { get; set; } = "default";

    /// <summary>If true, all non-blacklisted users are allowed.</summary>
    public bool IsGlobalAccessEnabled { get; set; } = true;

    /// <summary>User IDs explicitly allowed regardless of global flag.</summary>
    public List<long> WhiteList { get; set; } = new();

    /// <summary>User IDs explicitly denied regardless of other settings.</summary>
    public List<long> BlackList { get; set; } = new();

    /// <summary>If true, all non-blacklisted groups are allowed.</summary>
    public bool IsGroupAccessEnabled { get; set; }

    /// <summary>Group chat IDs explicitly allowed.</summary>
    public List<long> GroupWhiteList { get; set; } = new();

    /// <summary>Group chat IDs explicitly denied regardless of other settings.</summary>
    public List<long> GroupBlackList { get; set; } = new();

    /// <summary>Creates a default AccessSetting instance.</summary>
    public static AccessSetting Create() => new();
}
