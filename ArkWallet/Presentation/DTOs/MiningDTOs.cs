using ArkWallet.Application.Dtos;

namespace ArkWallet.Presentation.DTOs
{
    /// <summary>
    /// Ответ с глобальными правилами майнинга токенов
    /// </summary>
    /// <param name="Rules">Массив правил майнинга</param>
    public record GetMiningRulesResponse(TokensMiningRules[] Rules);

    /// <summary>
    /// Ответ со списком майнинг-машин
    /// </summary>
    /// <param name="Machines">Массив майнинг-машин</param>
    public record GetMiningMachinesResponse(MiningMachineData[] Machines);

    /// <summary>
    /// Ответ со списком слотов майнинг-машин трейдера
    /// </summary>
    /// <param name="Slots">Массив слотов майнинг-машин</param>
    public record GetMiningSlotsResponse(MiningMachineSlotData[] Slots);

    /// <summary>
    /// Запрос на покупку майнинг-машины
    /// </summary>
    /// <param name="MachineId">Идентификатор машины</param>
    public record BuyMiningMachineRequest(long MachineId);

    /// <summary>
    /// Ответ на покупку майнинг-машины
    /// </summary>
    /// <param name="SlotId">Идентификатор созданного слота</param>
    public record BuyMiningMachineResponse(long SlotId);

    /// <summary>
    /// Запрос на переключение слота на другой токен
    /// </summary>
    /// <param name="MiningMachineSlotId">Идентификатор слота</param>
    /// <param name="Symbol">Символ токена</param>
    public record SwitchMiningTokenRequest(long MiningMachineSlotId, string Symbol);

    /// <summary>
    /// Запрос на снятие токенов с одной машины
    /// </summary>
    /// <param name="MiningMachineSlotId">Идентификатор слота машины</param>
    public record TakeMiningTokensRequest(long MiningMachineSlotId);
}
