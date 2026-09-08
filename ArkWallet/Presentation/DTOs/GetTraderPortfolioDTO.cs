using ArkWallet.Core.PortfolioContext.Application.Contracts.PortfolioServices;
using ArkWallet.Core.General.Application.Dtos;

namespace ArkWallet.Presentation.DTOs
{
    /// <summary>
    /// Ответ со списком токенов в портфеле
    /// </summary>
    /// <param name="Items">Массив токенов с количеством</param>
    public record GetPortfolioResponse(PortfolioItemInfo[] Items);
}
