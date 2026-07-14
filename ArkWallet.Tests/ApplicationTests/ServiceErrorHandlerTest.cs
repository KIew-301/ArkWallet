using ArkWallet.Application.Common;
using ArkWallet.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ApplicationTests;

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
}
