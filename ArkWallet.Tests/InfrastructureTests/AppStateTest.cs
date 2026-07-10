using ArkWallet.Infrastructure.Data;

namespace ArkWallet.Tests.InfrastructureTests;

public class AppStateTest
{
    [Fact]
    public void Create_StoresSerializedValue()
    {
        var state = AppState.Create("testKey", 42);

        Assert.Equal("testKey", state.Key);
        Assert.Equal(42, state.GetValue<int>());
    }

    [Fact]
    public void Create_WithStringValue_StoresCorrectly()
    {
        var state = AppState.Create("name", "hello");

        Assert.Equal("hello", state.GetValue<string>());
    }

    [Fact]
    public void Create_WithBooleanValue_StoresCorrectly()
    {
        var state = AppState.Create("flag", true);

        Assert.True(state.GetValue<bool>());
    }

    [Fact]
    public void Create_WithDecimalValue_StoresCorrectly()
    {
        var state = AppState.Create("amount", 123.45m);

        Assert.Equal(123.45m, state.GetValue<decimal>());
    }

    [Fact]
    public void Create_WithDateTimeValue_StoresCorrectly()
    {
        var now = new DateTime(2025, 6, 15, 12, 30, 0, DateTimeKind.Utc);
        var state = AppState.Create("date", now);

        Assert.Equal(now, state.GetValue<DateTime>());
    }

    [Fact]
    public void UpdateValue_OverwritesPreviousValue()
    {
        var state = AppState.Create("counter", 1);

        state.UpdateValue(99);

        Assert.Equal(99, state.GetValue<int>());
    }

    [Fact]
    public void UpdateValue_ChangesType()
    {
        var state = AppState.Create("flex", 42);

        state.UpdateValue("now a string");

        Assert.Equal("now a string", state.GetValue<string>());
    }

    [Fact]
    public void GetValue_DeserializesComplexObject()
    {
        var data = new List<int> { 1, 2, 3 };
        var state = AppState.Create("list", data);

        var result = state.GetValue<List<int>>();

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }
}
