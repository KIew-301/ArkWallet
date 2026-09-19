namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices
{
    /// <summary>
    /// Период подписки: неделя, месяц или год.
    /// </summary>
    public enum SubscriptionPeriod
    {
        /// <summary>
        /// Неделя.
        /// </summary>
        Week,

        /// <summary>
        /// Месяц.
        /// </summary>
        Month,

        /// <summary>
        /// Год.
        /// </summary>
        Year
    }

    /// <summary>
    /// Расширения для <see cref="SubscriptionPeriod"/>.
    /// </summary>
    public static class SubscriptionPeriodExtensions
    {
        /// <summary>
        /// Возвращает длительность периода в минутах: неделя = 10080, месяц = 43200, год = 525600.
        /// </summary>
        public static int GetDurationMinutes(this SubscriptionPeriod period) => period switch
        {
            SubscriptionPeriod.Week => 7 * 24 * 60,       // 10080
            SubscriptionPeriod.Month => 30 * 24 * 60,      // 43200
            SubscriptionPeriod.Year => 365 * 24 * 60,      // 525600
            _ => throw new ArgumentOutOfRangeException(nameof(period), "Неизвестное значение SubscriptionPeriod.")
        };

        /// <summary>
        /// Отображаемое название периода на русском (винительный падеж): «неделю», «месяц», «год».
        /// </summary>
        public static string ToDisplayName(this SubscriptionPeriod period) => period switch
        {
            SubscriptionPeriod.Week => "неделю",
            SubscriptionPeriod.Month => "месяц",
            SubscriptionPeriod.Year => "год",
            _ => throw new ArgumentOutOfRangeException(nameof(period), "Неизвестное значение SubscriptionPeriod.")
        };

        /// <summary>
        /// Пытается распознать период подписки из строки (неделя/месяц/год или week/month/year).
        /// </summary>
        /// <param name="token">Текст периода.</param>
        /// <param name="period">Распознанный период.</param>
        /// <returns>true, если период распознан.</returns>
        public static bool TryParse(string? token, out SubscriptionPeriod period)
        {
            switch (token?.Trim().ToLowerInvariant())
            {
                case "week":
                case "неделя":
                    period = SubscriptionPeriod.Week;
                    return true;
                case "month":
                case "месяц":
                    period = SubscriptionPeriod.Month;
                    return true;
                case "year":
                case "год":
                    period = SubscriptionPeriod.Year;
                    return true;
                default:
                    period = default;
                    return false;
            }
        }
    }
}
