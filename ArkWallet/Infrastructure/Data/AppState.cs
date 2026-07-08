using System.ComponentModel.DataAnnotations;
using System.Text.Json;

internal class AppState(string key, string value)
{
    [Key]
    public string Key { get; private set; } = key;

    public string Value { get; private set; } = value;

    public static AppState Create<T>(string key, T value)
    {
        return new(key, JsonSerializer.Serialize(value));
    }

    public void UpdateValue<T>(T value)
    {
        Value = JsonSerializer.Serialize(value);
    }

    public T? GetValue<T>()
    {
        return JsonSerializer.Deserialize<T>(Value);
    }
}