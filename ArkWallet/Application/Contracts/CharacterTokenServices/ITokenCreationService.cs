using ArkWallet.Application.Common;
using ArkWallet.Application.Dtos;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Contracts.CharacterTokenServices
{
    /// <summary>
    /// Сервис для создания новых токенов персонажей в системе
    /// </summary>
    internal interface ITokenCreationService
    {
        /// <summary>
        /// Создает новый токен персонажа с указанными параметрами
        /// </summary>
        /// <param name="command">Команда создания токена с параметрами</param>
        /// <returns>Результат операции создания токена</returns>
        /// <remarks>
        /// <para>
        /// Выполняет проверки перед созданием:
        /// - Проверяет корректность команды
        /// - Проверяет что цена и общее количество больше 0
        /// - Проверяет отсутствие токена с таким символом
        /// </para>
        /// <para>
        /// Операция выполняется в транзакции для обеспечения целостности данных.
        /// </para>
        /// </remarks>
        Task<Result<TokenCreationData>> CreateTokenAsync(CreateTokenCommand command);
    }

    /// <summary>
    /// Команда для создания нового токена персонажа
    /// </summary>
    /// <param name="Symbol">Уникальный символ токена (например, "ARK_001")</param>
    /// <param name="Name">Название токена/персонажа</param>
    /// <param name="Rarity">Редкость персонажа</param>
    /// <param name="StartPrice">Начальная цена токена</param>
    /// <param name="TotalSupply">Общее количество выпускаемых токенов</param>
    /// <param name="IsActive">Флаг активности токена</param>
    public record CreateTokenCommand(
        string Symbol,
        string Name,
        CharacterRarity Rarity,
        decimal StartPrice,
        int TotalSupply,
        bool IsActive
    )
    {
        /// <summary>
        /// Преобразует команду в сущность доменной модели
        /// </summary>
        /// <returns>Сущность CharacterToken с данными из команды</returns>
        internal CharacterToken ToEntity()
        {
            return new()
            {
                Symbol = Symbol,
                Name = Name,
                Rarity = Rarity,
                CurrentPrice = StartPrice,
                TotalSupply = TotalSupply,
                IsActive = IsActive
            };
        }
    };

    /// <summary>
    /// Результат операции создания токена
    /// </summary>
    /// <param name="Token">DTO созданного токена (только при успехе)</param>
    public record TokenCreationData(
        TokenInfoDto? Token = null
    );
}