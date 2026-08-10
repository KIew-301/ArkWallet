using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.CharacterTokenServices
{
    /// <summary>
    /// Сервис удаления и деактивации токенов персонажей
    /// </summary>
    internal interface ITokenDeletionService
    {
        /// <summary>
        /// Полное каскадное удаление токена вместе со связанными данными:
        /// портфели, ордера, сделки, свечи и маркет-мейкер боты.
        /// </summary>
        /// <param name="symbol">Символ токена</param>
        /// <returns>Результат операции удаления</returns>
        Task<Result> DeleteTokenAsync(string symbol);

        /// <summary>
        /// Мягкое отключение токена: IsActive = false. Токен остаётся в БД,
        /// но исчезает из торговли и списков активных токенов.
        /// </summary>
        /// <param name="symbol">Символ токена</param>
        /// <returns>Результат операции деактивации</returns>
        Task<Result> DeactivateTokenAsync(string symbol);
    }
}
