namespace ArkWallet.Core.MailContext.Domain.Message;

/// <summary>
/// Статус письма в почтовом ящике
/// </summary>
internal enum MailMessageStatus
{
    /// <summary>Отправлено — письмо создано и доставлено в ящик</summary>
    Sent,

    /// <summary>Прочитано — пользователь открыл письмо</summary>
    Read,

    /// <summary>Принято — пользователь принял награду из письма</summary>
    Accepted
}

/// <summary>
/// Тип письма в почтовом ящике
/// </summary>
internal enum MailType
{
    /// <summary>Уведомление без награды</summary>
    Notification,

    /// <summary>Подарок от другого участника</summary>
    Gift,

    /// <summary>Вознаграждение (награда за цель и т.п.)</summary>
    Reward
}