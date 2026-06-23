namespace ArkWallet.Presentation.DTOs
{
    public record GetTokenListResponse(TokenItem[] Tokens);
    public record TokenItem(string Symbol, string TokenName, decimal Price, decimal DailyChangePercent, string IconUrl, string ImageUrl);
}
