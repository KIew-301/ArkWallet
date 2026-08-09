namespace ArkWallet.Application.Common
{
    /// <summary>Результат валидации: флаг успеха и сообщение об ошибке.</summary>
    public record ValidationResult(bool IsValid, string? Message = null)
    {
        public static ValidationResult Success() => new(true);

        /// <summary>Создаёт неуспешный результат валидации с сообщением.</summary>
        public static ValidationResult Failed(string? message) => new(false, message);
    }
}
