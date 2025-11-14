using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Application.Common
{
    public record ValidationResult(bool IsValid, string? Message = null)
    {
        public static ValidationResult Success() => new(true);
        public static ValidationResult Failed(string message) => new(false, message);
    }
}
