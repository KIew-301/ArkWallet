using ArkWallet.Infrastructure.Data;
using System.Text.Json;

namespace ArkWallet.Tests.InfrastructureTests;

public class AppStateTest
{
    [Fact]
    public void Create_StoresSerializedValue()
    {
        var state = AppState.Create("testKey", 42);

        Assert.Equal("testKey", state.Key);
        Assert.Equal(42, JsonSerializer.Deserialize<int>(state.Value)!);
    }

    [Fact]
    public void Create_WithStringValue_StoresCorrectly()
    {
        var state = AppState.Create("name", "hello");

        Assert.Equal("hello", JsonSerializer.Deserialize<string>(state.Value)!);
    }

    [Fact]
    public void Create_WithBooleanValue_StoresCorrectly()
    {
        var state = AppState.Create("flag", true);

        Assert.True(JsonSerializer.Deserialize<bool>(state.Value)!);
    }

    [Fact]
    public void Create_WithDecimalValue_StoresCorrectly()
    {
        var state = AppState.Create("amount", 123.45m);

        Assert.Equal(123.45m, JsonSerializer.Deserialize<decimal>(state.Value)!);
    }

    [Fact]
    public void Create_WithDateTimeValue_StoresCorrectly()
    {
        var now = new DateTime(2025, 6, 15, 12, 30, 0, DateTimeKind.Utc);
        var state = AppState.Create("date", now);

        Assert.Equal(now, JsonSerializer.Deserialize<DateTime>(state.Value)!);
    }

    [Fact]
    public void Update_OverwritesPreviousValue()
    {
        var state = AppState.Create("counter", 1);

        state.Update(99);

        Assert.Equal(99, JsonSerializer.Deserialize<int>(state.Value)!);
    }

    [Fact]
    public void Update_ChangesType()
    {
        var state = AppState.Create("flex", 42);

        state.Update("now a string");

        Assert.Equal("now a string", JsonSerializer.Deserialize<string>(state.Value)!);
    }

    private static readonly int[] IntArray3 = [1, 2, 3];

    [Fact]
    public void GetValue_DeserializesComplexObject()
    {
        var data = new List<int> { 1, 2, 3 };
        var state = AppState.Create("list", data);

        var result = JsonSerializer.Deserialize<List<int>>(state.Value)!;

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(IntArray3, result);
    }
}
