using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Contracts.Decorators
{
    public interface IButtonDecorator
    {
        Task<List<QuickButton>> DecorateButtonsAsync(string stepName, List<QuickButton> baseKeyword, UserSession session);
    }

}
