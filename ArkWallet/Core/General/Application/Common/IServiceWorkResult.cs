namespace ArkWallet.Core.General.Application.Common
{
    /// <summary>Результат операции с данными: успешный или неуспешный.</summary>
    public record Result<T>
    {
        /// <summary>Флаг успешного выполнения операции.</summary>
        public bool IsSuccess { get; }
        /// <summary>Сообщение результата (пустое при успехе, описание ошибки при неудаче).</summary>
        public string Message { get; }
        private T? _data { get; }

        private Result(bool isSuccess, string message, T? data)
        {
            IsSuccess = isSuccess;
            Message = message;
            _data = data;
        }

        /// <summary>Создаёт успешный результат с данными.</summary>
        public static Result<T> Ok(T data) => new(true, "Success", data);
        /// <summary>Создаёт неуспешный результат с сообщением об ошибке.</summary>
        public static Result<T> Fail(string message) => new(false, message, default);

        /// <summary>Получает данные при успешном результате; возвращает true при успехе.</summary>
        public bool TryGetData(out T data)
        {
            if (IsSuccess && _data is not null)
            {
                data = _data;
                return true;
            }

            data = default!;
            return false;
        }
    }

    /// <summary>Результат операции без данных: только флаг успеха и сообщение.</summary>
    public record Result(bool IsSuccess, string Message)
    {
        /// <summary>Создаёт успешный результат без дополнительных данных.</summary>
        public static Result Ok() => new(true, "Success");
        /// <summary>Создаёт неуспешный результат с сообщением об ошибке.</summary>
        public static Result Fail(string message) => new(false, message);
    }
}
