namespace ArkWallet.Domain.ValueObjects;

/// <summary>
/// Статический класс с описаниями валюты, используемой в приложении
/// </summary>
public static class Descriptor
{
    /// <summary>Символ валюты для отображения в UI</summary>
    public const string CurrencySymbol = "₽";

    /// <summary>Название валюты</summary>
    public const string CurrencyName = "руб.";

    /// <summary>Формат баланса: {amount}{symbol}</summary>
    public static string FormatBalance(decimal amount) => $"{amount:F2}{CurrencySymbol}";
}
