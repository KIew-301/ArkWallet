namespace ArkWallet.Presentation.DTOs
{
    public record GetBalanceRequest(int PeriodDays);
    public record GetBalanceResponse(decimal CurrentBalance, decimal ChangeAbsolute, decimal ChangePercent);
}
