namespace ArkWallet.Tests.HelpTools;

internal class TestTimeProvider : TimeProvider
{
    public DateTimeOffset DateTimeOffsetNow { get; set; } = new DateTimeOffset(2026, 1, 1, 0, 0, 0, new TimeSpan());
    public DateTimeOffset Now => DateTimeOffsetNow;

    public override DateTimeOffset GetUtcNow()
    {
        return DateTimeOffsetNow;
    }

    public void SkipInSeconds(int seconds)
    {
        DateTimeOffsetNow = DateTimeOffsetNow.AddSeconds(seconds);
    }
}
