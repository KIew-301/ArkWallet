using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.General.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.Core.General.Application.Common;

public class ServiceErrorHandlerTest
{
    private static readonly ILogger<ServiceErrorHandlerTest> Logger = NullLogger<ServiceErrorHandlerTest>.Instance;

    [Fact]
    public async Task ExecuteAsync_SuccessfulAction_ReturnsOk()
    {
        var result = await ServiceErrorHandler.ExecuteAsync(
            () => Task.FromResult(Result<int>.Ok(42)),
            Logger, "test");

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(42, data);
    }

    [Fact]
    public async Task ExecuteAsync_FailedAction_ReturnsFail()
    {
        var result = await ServiceErrorHandler.ExecuteAsync(
            () => Task.FromResult(Result<int>.Fail("error")),
            Logger, "test");

        Assert.False(result.IsSuccess);
        Assert.Equal("error", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_DomainException_ReturnsFail()
    {
        var result = await ServiceErrorHandler.ExecuteAsync<int>(
            () => throw new DomainException("business error"),
            Logger, "test");

        Assert.False(result.IsSuccess);
        Assert.Equal("business error", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_GeneralException_ReturnsFailWithInnerMessage()
    {
        var inner = new InvalidOperationException("inner error");
        var result = await ServiceErrorHandler.ExecuteAsync<int>(
            () => throw new Exception("outer", inner),
            Logger, "test");

        Assert.False(result.IsSuccess);
        Assert.Equal("inner error", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_GeneralException_NoInner_ReturnsOuterMessage()
    {
        var result = await ServiceErrorHandler.ExecuteAsync<int>(
            () => throw new Exception("error"),
            Logger, "test");

        Assert.False(result.IsSuccess);
        Assert.Equal("error", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Result_SuccessfulAction_ReturnsOk()
    {
        var result = await ServiceErrorHandler.ExecuteAsync(
            () => Task.FromResult(Result.Ok()),
            Logger, "test");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_Result_DomainException_ReturnsFail()
    {
        var result = await ServiceErrorHandler.ExecuteAsync(
            () => throw new DomainException("business error"),
            Logger, "test");

        Assert.False(result.IsSuccess);
        Assert.Equal("business error", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Result_GeneralException_ReturnsFail()
    {
        var result = await ServiceErrorHandler.ExecuteAsync(
            () => throw new Exception("error"),
            Logger, "test");

        Assert.False(result.IsSuccess);
        Assert.Equal("error", result.Message);
    }

    [Fact]
    public void Execute_Sync_SuccessfulAction_ReturnsOk()
    {
        var result = ServiceErrorHandler.Execute(
            () => Result<int>.Ok(42),
            Logger, "test");

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(42, data);
    }

    [Fact]
    public void Execute_Sync_DomainException_ReturnsFail()
    {
        var result = ServiceErrorHandler.Execute<int>(
            () => throw new DomainException("business error"),
            Logger, "test");

        Assert.False(result.IsSuccess);
        Assert.Equal("business error", result.Message);
    }

    [Fact]
    public void Execute_Sync_GeneralException_ReturnsFail()
    {
        var result = ServiceErrorHandler.Execute<int>(
            () => throw new Exception("error"),
            Logger, "test");

        Assert.False(result.IsSuccess);
        Assert.Equal("error", result.Message);
    }

    /// <summary>
    /// Отказ доменного правила раньше уходил только в Result, и в логе оставалось голое сообщение
    /// без контекста. Из-за этого нехватка баланса у конкретного бота приходилось искать по коду,
    /// хотя домен уже знает трейдера, сумму и остаток. Фиксирует требование: причина обязана
    /// попасть в лог вместе с именем контекста.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_DomainException_IsLoggedWithContext(bool genericOverload)
    {
        var recorder = new RecordingLogger<ServiceErrorHandlerTest>();

        if (genericOverload)
        {
            var result = await ServiceErrorHandler.ExecuteAsync(
                () => throw new DomainException("Insufficient balance: trader 726 требуется 3128647"),
                recorder, "OrderCreationService");
            Assert.False(result.IsSuccess);
        }
        else
        {
            var result = await ServiceErrorHandler.ExecuteAsync<int>(
                () => throw new DomainException("Insufficient balance: trader 726 требуется 3128647"),
                recorder, "OrderCreationService");
            Assert.False(result.IsSuccess);
        }

        var entry = Assert.Single(recorder.Entries);
        Assert.Contains("Insufficient balance", entry.Message);
        Assert.Contains("726", entry.Message);
        Assert.Contains("OrderCreationService", entry.Message);
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    [Fact]
    public void Execute_Sync_DomainException_IsLoggedWithContext()
    {
        var recorder = new RecordingLogger<ServiceErrorHandlerTest>();

        var result = ServiceErrorHandler.Execute<int>(
            () => throw new DomainException("Insufficient balance: trader 726 требуется 3128647"),
            recorder, "BotOrchestrator");

        Assert.False(result.IsSuccess);
        var entry = Assert.Single(recorder.Entries);
        Assert.Contains("726", entry.Message);
        Assert.Contains("BotOrchestrator", entry.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Success_LogsNothing()
    {
        var recorder = new RecordingLogger<ServiceErrorHandlerTest>();

        await ServiceErrorHandler.ExecuteAsync(
            () => Task.FromResult(Result<int>.Ok(1)), recorder, "ctx");
        await ServiceErrorHandler.ExecuteAsync(
            () => Task.FromResult(Result.Ok()), recorder, "ctx");
        ServiceErrorHandler.Execute(
            () => Result<int>.Ok(1), recorder, "ctx");

        Assert.Empty(recorder.Entries);
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}
