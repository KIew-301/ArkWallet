using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Domain.Exceptions
{
    [ExcludeFromCodeCoverage(Justification = "Simple domain exception class with no business logic")]
    public class DomainException : Exception
    {
        public DomainException(string message) : base(message)
        {
        }

        public DomainException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
