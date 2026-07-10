using ArkWallet.Application.Common;
using ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Contracts.MarketMaker;

/// <summary>
/// Сервис для регистрации ботов-маркетмейкеров как трейдеров в системе
/// </summary>
public interface IMarketMakerBotRegistrationService
{
    /// <summary>
    /// Регистрирует нового бота-маркетмейкера как трейдера
    /// </summary>
    /// <param name="telegramFakeId">Ложный телеграмм Id для регистрации</param>
    /// <param name="symbol">Символ токена, с которым работает бот</param>
    /// <param name="botRole">Роль бота (Buyer/Seller)</param>
    /// <param name="initialPower">Начальная мощность бота</param>
    /// <returns>Результат операции с данными созданного бота</returns>
    /// <remarks>
    /// <para>
    /// Выполняет:
    /// - Создание трейдера для бота (через ITraderRegistrationService)
    /// - Создание сущности MarketMakerBot
    /// - Возвращает данные бота с привязкой к трейдеру
    /// </para>
    /// </remarks>
    Task<Result<MarketMakerBotRegistrationData>> RegisterBotAsync(int telegramFakeId, string symbol, BotRole botRole, decimal initialPower = 50);
}

/// <summary>
/// Данные о зарегистрированном боте
/// </summary>
/// <param name="BotId">ID бота</param>
/// <param name="TraderId">ID трейдера, связанного с ботом</param>
public record MarketMakerBotRegistrationData(
    long BotId,
    long TraderId
);