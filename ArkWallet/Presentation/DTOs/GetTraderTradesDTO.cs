using ArkWallet.Application.Contracts.TradeServices;

namespace ArkWallet.Presentation.DTOs
{
    /// <summary>
    /// Ответ со списком сделок
    /// </summary>
    /// <param name="Trades">Массив сделок</param>
    public record GetTradesResponse(TradeInfo[] Trades);
}
