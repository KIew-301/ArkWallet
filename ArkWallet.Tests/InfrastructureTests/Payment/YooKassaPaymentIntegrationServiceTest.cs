using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using ArkWallet.Infrastructure.Payment;
using PReq = ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentRequest;
using PRes = ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentResult;

namespace ArkWallet.Tests.InfrastructureTests.Payment;

public class YooKassaPaymentIntegrationServiceTest
{
    private static YooKassaOptions DefaultOptions => new()
    {
        ShopId = "test_shop",
        SecretKey = "test_secret",
        ApiBaseUrl = "https://api.test.yookassa.ru/v3",
        ReturnUrl = "https://t.me/testbot"
    };

    private static JsonSerializerOptions JsonIndent = new() { WriteIndented = false };

    private static HttpResponseMessage MakeResponse(HttpStatusCode statusCode, string json)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return response;
    }

    private static Mock<HttpMessageHandler> CreateMockHandler(Func<System.Net.Http.HttpRequestMessage, Task<HttpResponseMessage>> sendFunc)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<System.Net.Http.HttpRequestMessage>(),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .Returns((System.Net.Http.HttpRequestMessage req, System.Threading.CancellationToken ct) => sendFunc(req));
        return handler;
    }

    private static (Mock<HttpMessageHandler> Handler, HttpClient Client) CreateClient(Mock<HttpMessageHandler> handler, Uri? baseAddress = null)
    {
        var client = new HttpClient(handler.Object)
        {
            BaseAddress = baseAddress ?? new Uri(DefaultOptions.ApiBaseUrl)
        };
        return (handler, client);
    }

    [Fact]
    public async Task CreatePaymentAsync_OnSuccess_ReturnsSuccessWithConfirmationUrl()
    {
        var expectedPaymentId = "2e4d0000-1111-2222-3333-444444444444";
        var responseBody = @"{""id"":""2e4d0000-1111-2222-3333-444444444444"",""status"":""pending"",""confirmation"":{""confirmation_url"":""https://yoomoney.ru/checkout/payments/v2/contract?orderId=ABC123""},""amount"":{""value"":""290.00"",""currency"":""RUB""}}";

        var handler = CreateMockHandler(_ => Task.FromResult(MakeResponse(HttpStatusCode.OK, responseBody)));
        var (_, client) = CreateClient(handler);

        var options = Options.Create(DefaultOptions);
        var logger = NullLogger<YooKassaPaymentIntegrationService>.Instance;
        var service = new YooKassaPaymentIntegrationService(client, options, logger);

        var request = new PReq
        {
            TraderTelegramId = 12345,
            AmountRubles = 290,
            SubscriptionId = 1,
            Description = "Premium subscription"
        };

        var result = await service.CreatePaymentAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedPaymentId, result.PaymentId);
        Assert.NotNull(result.ConfirmationUrl);
        Assert.True(result.RequiresConfirmation);
    }

    [Fact]
    public async Task CreatePaymentAsync_WithConfirmationUrl_ReturnsSameUrl()
    {
        var confirmationUrl = "https://yoomoney.ru/checkout/payments/v2/contract?orderId=TEST_URL";
        var responseBody = $@"{{""id"":""PAY-001"",""status"":""pending"",""confirmation"":{{""confirmation_url"":""{confirmationUrl}""}},""amount"":{{""value"":""100.00"",""currency"":""RUB""}}}}";

        var handler = CreateMockHandler(_ => Task.FromResult(MakeResponse(HttpStatusCode.OK, responseBody)));
        var (_, client) = CreateClient(handler);

        var options = Options.Create(DefaultOptions);
        var logger = NullLogger<YooKassaPaymentIntegrationService>.Instance;
        var service = new YooKassaPaymentIntegrationService(client, options, logger);

        var request = new PReq
        {
            TraderTelegramId = 12345,
            AmountRubles = 100,
            SubscriptionId = 1
        };

        var result = await service.CreatePaymentAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(confirmationUrl, result.ConfirmationUrl);
    }

    [Fact]
    public async Task CreatePaymentAsync_OnSucceededStatus_StillReturnsSuccess()
    {
        var responseBody = @"{""id"":""PAY-SUCCEED"",""status"":""succeeded"",""amount"":{""value"":""290.00"",""currency"":""RUB""}}";

        var handler = CreateMockHandler(_ => Task.FromResult(MakeResponse(HttpStatusCode.OK, responseBody)));
        var (_, client) = CreateClient(handler);

        var options = Options.Create(DefaultOptions);
        var logger = NullLogger<YooKassaPaymentIntegrationService>.Instance;
        var service = new YooKassaPaymentIntegrationService(client, options, logger);

        var request = new PReq
        {
            TraderTelegramId = 12345,
            AmountRubles = 290,
            SubscriptionId = 1
        };

        var result = await service.CreatePaymentAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("PAY-SUCCEED", result.PaymentId);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public async Task CreatePaymentAsync_OnNonSuccessHttpStatus_ReturnsFailed()
    {
        var handler = CreateMockHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));
        var (_, client) = CreateClient(handler);

        var options = Options.Create(DefaultOptions);
        var logger = NullLogger<YooKassaPaymentIntegrationService>.Instance;
        var service = new YooKassaPaymentIntegrationService(client, options, logger);

        var request = new PReq
        {
            TraderTelegramId = 12345,
            AmountRubles = 290,
            SubscriptionId = 1
        };

        var result = await service.CreatePaymentAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("yookassa_error", result.PayerInfo);
        Assert.Null(result.PaymentId);
    }

    [Fact]
    public async Task CreatePaymentAsync_OnCanceledStatus_ReturnsFailed()
    {
        var responseBody = @"{""id"":""PAY-CANCEL"",""status"":""canceled"",""amount"":{""value"":""290.00"",""currency"":""RUB""}}";

        var handler = CreateMockHandler(_ => Task.FromResult(MakeResponse(HttpStatusCode.OK, responseBody)));
        var (_, client) = CreateClient(handler);

        var options = Options.Create(DefaultOptions);
        var logger = NullLogger<YooKassaPaymentIntegrationService>.Instance;
        var service = new YooKassaPaymentIntegrationService(client, options, logger);

        var request = new PReq
        {
            TraderTelegramId = 12345,
            AmountRubles = 290,
            SubscriptionId = 1
        };

        var result = await service.CreatePaymentAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("PAY-CANCEL", result.PaymentId);
        Assert.Equal("yookassa_error", result.PayerInfo);
    }

    [Fact]
    public async Task CreatePaymentAsync_OnInvalidJson_ReturnsFailed()
    {
        var handler = CreateMockHandler(_ => Task.FromResult(MakeResponse(HttpStatusCode.OK, "not-json{broken")));
        var (_, client) = CreateClient(handler);

        var options = Options.Create(DefaultOptions);
        var logger = NullLogger<YooKassaPaymentIntegrationService>.Instance;
        var service = new YooKassaPaymentIntegrationService(client, options, logger);

        var request = new PReq
        {
            TraderTelegramId = 12345,
            AmountRubles = 290,
            SubscriptionId = 1
        };

        var result = await service.CreatePaymentAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("yookassa_error", result.PayerInfo);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_OnSuccess_ReturnsSucceededStatus()
    {
        var responseBody = @"{""id"":""PAY-STATUS-1"",""status"":""succeeded"",""amount"":{""value"":""290.00"",""currency"":""RUB""}}";

        var handler = CreateMockHandler(msg =>
        {
            var expectedPath = "/payments/PAY-STATUS-1";
            Assert.EndsWith(expectedPath, msg.RequestUri!.AbsolutePath);
            return Task.FromResult(MakeResponse(HttpStatusCode.OK, responseBody));
        });
        var (_, client) = CreateClient(handler);

        var options = Options.Create(DefaultOptions);
        var logger = NullLogger<YooKassaPaymentIntegrationService>.Instance;
        var service = new YooKassaPaymentIntegrationService(client, options, logger);

        var result = await service.GetPaymentStatusAsync("PAY-STATUS-1");

        Assert.True(result.IsSucceeded);
        Assert.Equal("succeeded", result.Status);
        Assert.Equal("PAY-STATUS-1", result.PaymentId);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_OnError_ReturnsErrorAndFailed()
    {
        var handler = CreateMockHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var (_, client) = CreateClient(handler);

        var options = Options.Create(DefaultOptions);
        var logger = NullLogger<YooKassaPaymentIntegrationService>.Instance;
        var service = new YooKassaPaymentIntegrationService(client, options, logger);

        var result = await service.GetPaymentStatusAsync("PAY-ERR");

        Assert.Equal("PAY-ERR", result.PaymentId);
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_OnNullOrEmptyBody_ReturnsError()
    {
        var handler = CreateMockHandler(_ => Task.FromResult(MakeResponse(HttpStatusCode.OK, "")));
        var (_, client) = CreateClient(handler);

        var options = Options.Create(DefaultOptions);
        var logger = NullLogger<YooKassaPaymentIntegrationService>.Instance;
        var service = new YooKassaPaymentIntegrationService(client, options, logger);

        var result = await service.GetPaymentStatusAsync("PAY-NULL");

        Assert.Equal("PAY-NULL", result.PaymentId);
        Assert.Equal("error", result.Status);
    }
}
