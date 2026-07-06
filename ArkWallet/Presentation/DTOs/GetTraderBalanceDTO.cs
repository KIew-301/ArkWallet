namespace ArkWallet.Presentation.DTOs
{
    /// <summary>
    /// Запрос на получение баланса
    /// </summary>
    /// <param name="PeriodDays">Период для расчёта изменений (дней)</param>
    public record GetBalanceRequest(int PeriodDays);
    /// <summary>
    /// Ответ с данными о балансе
    /// </summary>
    /// <param name="CurrentBalance">Текущий баланс</param>
    /// <param name="ChangeAbsolute">Абсолютное изменение</param>
    /// <param name="ChangePercent">Процентное изменение</param>
    public record GetBalanceResponse(decimal CurrentBalance, decimal ChangeAbsolute, decimal ChangePercent);
}
