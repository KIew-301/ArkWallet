namespace ArkWallet.Application.Dtos
{
    /// <summary>
    /// Данные майнинга одного токена на машине
    /// </summary>
    /// <param name="TokenIcon">Иконка токена</param>
    /// <param name="Symbol">Символ токена</param>
    /// <param name="MiningSpeed">Скорость майнинга</param>
    /// <param name="Profit">Прибыль (скорость майнинга * текущая цена)</param>
    public record TokensMiningData(
        string TokenIcon,
        string Symbol,
        decimal MiningSpeed,
        decimal Profit);

    /// <summary>
    /// Данные майнинга активного токена слота
    /// </summary>
    /// <param name="TokenIcon">Иконка токена</param>
    /// <param name="Symbol">Символ токена</param>
    /// <param name="MiningSpeed">Скорость майнинга</param>
    /// <param name="Profit">Прибыль (скорость майнинга * текущая цена)</param>
    public record ActiveTokenMiningData(
        string TokenIcon,
        string Symbol,
        decimal MiningSpeed,
        decimal Profit)
    {
        /// <summary>Пустые данные активного токена (слот без токена)</summary>
        public static ActiveTokenMiningData Empty() => new(string.Empty, string.Empty, 0m, 0m);
    }

    /// <summary>
    /// Данные майнинг-машины для продажи
    /// </summary>
    /// <param name="Id">Идентификатор машины</param>
    /// <param name="Name">Название</param>
    /// <param name="Type">Тип (строкой)</param>
    /// <param name="MaxProfit">Максимальная прибыль по всем токенам</param>
    /// <param name="SwitchingTime">Время переключения в минутах</param>
    /// <param name="Reusability">Процент возврата</param>
    /// <param name="Cost">Цена покупки</param>
    /// <param name="EffectiveTokensMiningData">Данные майнинга по эффективным токенам (коэффициент машины 0.85–1)</param>
    /// <param name="StableTokensMiningData">Данные майнинга по стабильным токенам (коэффициент машины 0.65–0.85)</param>
    public record MiningMachineData(
        long Id,
        string Name,
        string Type,
        decimal MaxProfit,
        int SwitchingTime,
        decimal Reusability,
        decimal Cost,
        List<TokensMiningData> EffectiveTokensMiningData,
        List<TokensMiningData> StableTokensMiningData);

    /// <summary>
    /// Данные слота майнинг-машины трейдера
    /// </summary>
    /// <param name="Id">Идентификатор слота</param>
    /// <param name="Name">Название машины</param>
    /// <param name="Type">Тип машины (строкой)</param>
    /// <param name="Status">Статус слота (строкой)</param>
    /// <param name="TokensAmountCollected">Накопленные токены</param>
    /// <param name="SwitchingPercent">Процент завершения переключения</param>
    /// <param name="SwitchingTime">Время переключения в минутах</param>
    /// <param name="Cost">Цена продажи слота</param>
    /// <param name="ActiveTokenMiningData">Данные активного токена (не входит в группы)</param>
    /// <param name="EffectiveTokensMiningData">Данные майнинга по эффективным токенам (коэффициент машины 0.85–1)</param>
    /// <param name="StableTokensMiningData">Данные майнинга по стабильным токенам (коэффициент машины 0.65–0.85)</param>
    public record MiningMachineSlotData(
        long Id,
        string Name,
        string Type,
        string Status,
        decimal TokensAmountCollected,
        decimal SwitchingPercent,
        int SwitchingTime,
        decimal Cost,
        ActiveTokenMiningData ActiveTokenMiningData,
        List<TokensMiningData> EffectiveTokensMiningData,
        List<TokensMiningData> StableTokensMiningData);

    /// <summary>
    /// Глобальное правило майнинга токена
    /// </summary>
    /// <param name="TokenInfo">Информация о токене</param>
    /// <param name="CurrentMiningStatus">Текущий статус прибыльности (строкой)</param>
    /// <param name="FutureMiningStatus">Будущий статус прибыльности (строкой)</param>
    /// <param name="BaseTokenMiningSpeed">Базовая скорость добычи токена</param>
    /// <param name="BaseProfit">Базовая прибыль (скорость * текущая цена)</param>
    public record TokensMiningRuleData(
        TokenInfoDto TokenInfo,
        string CurrentMiningStatus,
        string FutureMiningStatus,
        decimal BaseTokenMiningSpeed,
        decimal BaseProfit);
}
