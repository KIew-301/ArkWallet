using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.TraderServices;

/// <summary>
/// Сервис для получения данных о трейдерах
/// </summary>
public interface ITraderQueryService
{
    /// <summary>
    /// Возвращает профиль трейдера по Telegram ID
    /// </summary>
    /// <param name="traderTelegramId">Telegram ID трейдера</param>
    /// <returns>Данные профиля трейдера или null, если трейдер не найден</returns>
    Task<Result<TraderProfileInfo>> GetTraderProfileAsync(long traderTelegramId);
}

/// <summary>
/// DTO с данными профиля трейдера
/// </summary>
/// <param name="Username">Имя пользователя</param>
/// <param name="Balance">Текущий баланс</param>
public record TraderProfileInfo(string Username, decimal Balance);
