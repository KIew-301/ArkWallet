using ArkWallet.Core.General.Application.Common;

namespace ArkWallet.Core.MailContext.Application.Contracts.MailServices;

/// <summary>
/// Сервис регистрации писем в почтовых ящиках пользователей
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// Создаёт письмо для пользователя и отправляет уведомление (если уведомления включены)
    /// </summary>
    Task<Result<MailCreateResult>> CreateAsync(CreateCommand command);

    /// <summary>
    /// Пакетно создаёт письма для пользователей и отправляет уведомления (если уведомления включены)
    /// </summary>
    Task<Result<List<MailCreateResult>>> CreateManyAsync(IReadOnlyList<CreateCommand> commands);
}

/// <summary>
/// Результат создания письма
/// </summary>
public record MailCreateResult(long Id);
