using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Core.General.Domain.Exceptions
{
    /// <summary>Исключение домена</summary>
    [ExcludeFromCodeCoverage(Justification = "Simple domain exception class with no business logic")]
    public class DomainException : Exception
    {
        /// <summary>Создает новое исключение домена</summary>
        /// <param name="message">Сообщение об ошибке</param>
        public DomainException(string message) : base(message)
        {
        }

        /// <summary>Создает новое исключение домена с указанием причины</summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <param name="innerException">Внутреннее исключение</param>
        public DomainException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
