namespace ArkWallet.Application.Common;

internal static class BotFilter
{
    private const long BotIdMin = 100;
    private const long BotIdMax = 1000;

    public static bool IsBot(long traderId) => traderId >= BotIdMin && traderId <= BotIdMax;

    public static bool IsBotBotTrade(long buyerId, long sellerId) => IsBot(buyerId) && IsBot(sellerId);
}
