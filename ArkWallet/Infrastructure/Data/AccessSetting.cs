using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Infrastructure.Data;

public class AccessSetting
{
    [Key]
    public string Key { get; set; } = "default";

    public bool IsGlobalAccessEnabled { get; set; } = true;

    public List<long> WhiteList { get; set; } = new();

    public List<long> BlackList { get; set; } = new();

    public static AccessSetting Create() => new();
}
