namespace ArkWallet.Application.Common
{
    public record Result<T>
    {
        public bool IsSuccess { get; }
        public string Message { get; }
        private T? _data { get; }

        private Result(bool isSuccess, string message, T? data)
        {
            IsSuccess = isSuccess;
            Message = message;
            _data = data;
        }

        public static Result<T> Ok(T data) => new(true, "Success", data);
        public static Result<T> Fail(string message) => new(false, message, default);

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

    public record Result(bool IsSuccess, string Message)
    {
        public static Result Ok() => new(true, "Success");
        public static Result Fail(string message) => new(false, message);
    }
}
