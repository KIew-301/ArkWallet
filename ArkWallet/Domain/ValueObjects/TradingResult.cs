using ArkWallet.Domain.Entities;

namespace ArkWallet.Domain.ValueObjects
{
    internal class TradingResult
    {
        public List<Trade> Trades { get; set; } = new();
        public List<TradeOrder> UpdatedOrders { get; set; } = new();
        public List<Trader> UpdatedTraders { get; set; } = new();
        public List<PortfolioItem> UpdatedPortfolios { get; set; } = new();
        public CharacterToken UpdatedToken { get; set; }
        public TradeOrder OrderToAdd { get; set; }
        public List<PortfolioItem> PortfoliosToAdd { get; set; } = new();
        public bool IsSuccess { get; set; }
        public string Error { get; set; }

        public static TradingResult Failed(string error) => new() { IsSuccess = false, Error = error };
    }
}
