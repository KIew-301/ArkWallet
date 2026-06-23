namespace ArkWallet.Presentation.DTOs
{
    public record GetTradesResponse(TradeItem[] Trades);
    public record TradeItem(string Symbol, string TraderRole, decimal ExecutionPrice, decimal Quantity, decimal Profit, DateTime TradeDateTime);
}
