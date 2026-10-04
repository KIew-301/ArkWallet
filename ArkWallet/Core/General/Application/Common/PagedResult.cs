namespace ArkWallet.Core.General.Application.Common
{
    /// <summary>Постраничная выборка данных.</summary>
    /// <typeparam name="T">Тип элемента выборки.</typeparam>
    /// <param name="Items">Элементы текущей страницы.</param>
    /// <param name="Page">Номер страницы (начиная с 1).</param>
    /// <param name="PageSize">Размер страницы.</param>
    /// <param name="TotalCount">Всего записей, попавших в выборку.</param>
    public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
    {
        /// <summary>Количество страниц по всей выборке.</summary>
        public int TotalPages => PageSize <= 0
            ? 0
            : (int)Math.Ceiling(TotalCount / (double)PageSize);

        /// <summary>Есть ли следующая страница.</summary>
        public bool HasNext => Page < TotalPages;

        /// <summary>Есть ли предыдущая страница.</summary>
        public bool HasPrevious => Page > 1;
    }
}
