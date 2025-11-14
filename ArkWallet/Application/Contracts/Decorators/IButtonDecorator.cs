using ArkWallet.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Application.Contracts.Decorators
{
    public interface IButtonDecorator
    {
        Task<List<QuickButton>> DecorateButtonsAsync(string stepName, List<QuickButton> baseButtons, long traderId, Dictionary<string, object> sessionData);
    }

}
