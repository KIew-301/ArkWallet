using ArkWallet.Application.Contracts.CharacterTokenServices;

namespace ArkWallet.Presentation.DTOs
{
    /// <summary>
    /// Ответ со списком токенов
    /// </summary>
    /// <param name="Tokens">Массив токенов</param>
    public record GetTokenListResponse(TokenInfoWithPriceChange[] Tokens);
}
