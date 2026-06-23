namespace ArkWallet.Presentation.DTOs
{
    public record LoginRequest(string InitData);
    public record LoginResponse(string Token);
}
