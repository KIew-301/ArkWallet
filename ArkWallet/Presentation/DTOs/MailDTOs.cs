using ArkWallet.Core.MailContext.Application.Contracts.MailServices;

namespace ArkWallet.Presentation.DTOs
{
    /// <summary>
    /// Ответ со страницей писем трейдера
    /// </summary>
    /// <param name="Items">Письма текущей страницы, свежие первыми</param>
    /// <param name="Page">Номер текущей страницы (начиная с 1)</param>
    /// <param name="PageSize">Размер страницы</param>
    /// <param name="TotalCount">Всего писем по выбранному фильтру</param>
    /// <param name="TotalPages">Всего страниц по выбранному фильтру</param>
    /// <param name="HasNext">Есть ли следующая страница</param>
    /// <param name="HasPrevious">Есть ли предыдущая страница</param>
    public record GetMailsResponse(
        MailInfo[] Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages,
        bool HasNext,
        bool HasPrevious);

    /// <summary>
    /// Ответ со счётчиками писем трейдера для вкладок фильтра
    /// </summary>
    /// <param name="UnreadCount">Количество непрочитанных писем</param>
    /// <param name="RewardCount">Количество писем с доступной наградой</param>
    /// <param name="TotalCount">Общее количество писем</param>
    public record GetMailCountersResponse(int UnreadCount, int RewardCount, int TotalCount);

    /// <summary>
    /// Ответ на принятие наград во всех письмах
    /// </summary>
    /// <param name="AcceptedCount">Количество писем, где награда принята</param>
    public record AcceptAllMailsResponse(int AcceptedCount);
}