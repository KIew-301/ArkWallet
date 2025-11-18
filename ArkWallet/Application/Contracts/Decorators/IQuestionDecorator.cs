using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Contracts.Decorators
{
    public interface IQuestionDecorator
    {
        Task<string> DecorateQuestionAsync(string stepName, string baseQuestion, UserSession session);
    }
}
