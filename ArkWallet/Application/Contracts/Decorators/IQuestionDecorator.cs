using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Application.Contracts.Decorators
{
    public interface IQuestionDecorator
    {
        Task<string> DecorateQuestionAsync(string stepName, string baseQuestion, long traderId, Dictionary<string, object> sessionData);
    }
}
