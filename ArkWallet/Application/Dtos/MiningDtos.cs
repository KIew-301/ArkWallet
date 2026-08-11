using ArkWallet.Domain.Engines;

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
    /// <param name="TokensMiningData">Данные майнинга по токенам</param>
    public record MiningMachineData(
        long Id,
        string Name,
        string Type,
        decimal MaxProfit,
        int SwitchingTime,
        decimal Reusability,
        decimal Cost,
        List<TokensMiningData> TokensMiningData);

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
    /// <param name="ActiveTokenMiningData">Данные активного токена</param>
    /// <param name="TokensMiningData">Данные майнинга по остальным токенам</param>
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
        List<TokensMiningData> TokensMiningData);

    /// <summary>
    /// Глобальное правило майнинга токена
    /// </summary>
    /// <param name="TokenInfo">Информация о токене</param>
    /// <param name="CurrentStatus">Текущий статус прибыльности</param>
    /// <param name="FutureStatus">Будущий статус прибыльности</param>
    /// <param name="BaseMiningSpeed">Базовая скорость майнинга</param>
    /// <param name="BaseProfit">Базовая прибыль (скорость * текущая цена)</param>
    public record TokensMiningRules(
        TokenInfoDto TokenInfo,
        MiningStatus CurrentStatus,
        MiningStatus FutureStatus,
        decimal BaseMiningSpeed,
        decimal BaseProfit);
}
