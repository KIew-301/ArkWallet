using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Domain.Entities;
using System.Reflection;

namespace ArkWallet.Application.Contracts.PortfolioServices
{
    /// <summary>
    /// Сервис для запросов к портфелю трейдера
    /// </summary>
    public interface IPortfolioQueryService
    {
        /// <summary>
        /// Получает общий баланс токена в портфеле трейдера
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <returns>DTO с балансом токена или null если токен отсутствует в портфеле</returns>
        Task<Result<PortfolioItemInfo>> GetTokenBalanceAsync(long traderId, string symbol);

        /// <summary>
        /// Получает список всех токенов в портфеле трейдера
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <returns>Список DTO с балансами всех токенов портфеля</returns>
        /// <remarks>
        /// Возвращает пустой список если портфель трейдера пуст.
        /// </remarks>
        Task<Result<PortfolioItemInfo[]>> GetTraderTokensAsync(long traderId);
    }

    /// <summary>
    /// DTO с информацией о токене в портфеле для отображения на клиенте
    /// </summary>
    /// <param name="Quantity">Количество токенов в портфеле</param>
    /// <param name="AverageBuyPrice">Средняя цена покупки токена</param>
    /// <param name="BalanceInToken">Общая стоимость токенов в портфеле по текущей цене (Quantity * CurrentPrice)</param>
    /// <param name="ProfitPercent">Процент прибыли/убытка относительно средней цены покупки ((BalanceInToken / Cost) * 100 - 100)</param>
    /// <param name="TokenInfo">Информация о токене (символ, название, иконка и др.)</param>
    public record PortfolioItemInfo(
        decimal Quantity,
        decimal AverageBuyPrice,
        decimal BalanceInToken,
        decimal ProfitPercent,
        TokenInfo? TokenInfo
    )
    {
        internal static PortfolioItemInfo FromEntity(PortfolioItem item)
        {
            if (item == null)
                throw new Exception($"{MethodBase.GetCurrentMethod()?.Name} - item не может быть null");

            if (item.CharacterToken == null)
                throw new Exception($"{MethodBase.GetCurrentMethod()?.Name} - item.CharacterToken не может быть null");

            var balanceInToken = item.Quantity * item.CharacterToken.CurrentPrice;
            var cost = item.Quantity * item.AverageBuyPrice;
            var profitPercent = balanceInToken / cost * 100 - 100;

            var tokenInfo = item.CharacterToken != null
                ? TokenInfo.FromEntity(item.CharacterToken)
                : null;

            return new(
                item.Quantity,
                item.AverageBuyPrice,
                balanceInToken,
                profitPercent,
                tokenInfo
            );
        }
    }
}