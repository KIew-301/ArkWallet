using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MailServices;

/// <summary>
/// Сервис регистрации писем в почтовых ящиках пользователей
/// </summary>
public interface IMailMessageService
{
    /// <summary>
    /// Создаёт письмо для пользователя и отправляет уведомление (если уведомления включены)
    /// </summary>
    Task<Result<MailCreateResult>> CreateAsync(MailCreateCommand command);

    /// <summary>
    /// Пакетно создаёт письма для пользователей и отправляет уведомления (если уведомления включены)
    /// </summary>
    Task<Result<List<MailCreateResult>>> CreateManyAsync(IReadOnlyList<MailCreateCommand> commands);
}

/// <summary>
/// Результат создания письма
/// </summary>
public record MailCreateResult(long Id);
