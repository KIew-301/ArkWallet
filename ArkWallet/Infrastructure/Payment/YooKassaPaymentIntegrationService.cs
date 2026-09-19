using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using PReq = ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentRequest;
using PRes = ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentResult;
using PSRes = ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentStatusResult;
using IPaySvc = ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices.IPaymentIntegrationService;

namespace ArkWallet.Infrastructure.Payment;

/// <summary>
/// Конфигурация ЮKassa API.
/// </summary>
public class YooKassaOptions
{
    /// <summary>
    /// ID магазина в ЮKassa.
    /// </summary>
    public string ShopId { get; init; } = null!;

    /// <summary>
    /// Секретный ключ магазина.
    /// </summary>
    public string SecretKey { get; init; } = null!;

    /// <summary>
    /// URL возврата после оплаты (необязательный).
    /// </summary>
    public string? ReturnUrl { get; init; }

    /// <summary>
    /// Базовый URL API (по умолчанию — production).
    /// </summary>
    public string ApiBaseUrl { get; init; } = "https://api.yookassa.ru/v3";
}

/// <summary>
/// Интеграция с платёжной системой ЮKassa для создания и проверки платежей.
/// </summary>
public class YooKassaPaymentIntegrationService : Core.SubscriptionContext.Application.Contracts.PaymentServices.IPaymentIntegrationService
{
    private readonly HttpClient _httpClient;
    private readonly YooKassaOptions _options;
    private readonly ILogger<YooKassaPaymentIntegrationService> _logger;

    public YooKassaPaymentIntegrationService(
        HttpClient httpClient,
        IOptions<YooKassaOptions> options,
        ILogger<YooKassaPaymentIntegrationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Basic Auth: base64(shop_id:secret_key) — ставим один раз, а не на каждый запрос
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{_options.ShopId}:{_options.SecretKey}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    /// <summary>
    /// Создаёт новый платёж через ЮKassa API.
    /// </summary>
    /// <param name="request">Запрос на создание платежа.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Результат создания платежа.</returns>
    public async Task<Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentResult> CreatePaymentAsync(
        Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var body = BuildCreatePaymentBody(request);
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(body),
                System.Text.Encoding.UTF8,
                "application/json");

            // Idempotency key для повторяющихся запросов
            jsonContent.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

            var response = await _httpClient.PostAsync(
                $"{_options.ApiBaseUrl}/payments",
                jsonContent,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ЮKassa returned non-success status {Status} for amount {Amount}",
                    response.StatusCode, request.AmountRubles);
                return new Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentResult
                {
                    IsSuccess = false,
                    PayerInfo = "yookassa_error",
                };
            }

            var jsonText = await response.Content.ReadAsStringAsync(cancellationToken);
            var node = JsonNode.Parse(jsonText);
            if (node is null)
            {
                _logger.LogError("ЮKassa returned empty response body.");
                return new Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentResult
                {
                    IsSuccess = false,
                    PayerInfo = "yookassa_error",
                };
            }

            var status = node["status"]?.GetValue<string>();
            var paymentId = node["id"]?.GetValue<string>();
            var confirmationUrl = node["confirmation"]?["confirmation_url"]?.GetValue<string>();

            // «canceled»/«deleted» / пустой статус при 200 — тоже ошибка для клиента
            if (status is "canceled" or "deleted")
            {
                _logger.LogWarning(
                    "ЮKassa created payment in terminal state {Status} for amount {Amount}",
                    status, request.AmountRubles);
                return new Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentResult
                {
                    IsSuccess = false,
                    PaymentId = paymentId,
                    PayerInfo = "yookassa_error",
                };
            }

            return new Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentResult
            {
                IsSuccess = true,
                PaymentId = paymentId,
                TransactionId = paymentId,
                ConfirmationUrl = confirmationUrl,
                RequiresConfirmation = status != "succeeded",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while creating payment via ЮKassa for amount {Amount}", request.AmountRubles);
            return new Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentResult
            {
                IsSuccess = false,
                PayerInfo = "yookassa_error",
            };
        }
    }

    /// <summary>
    /// Получает текущий статус платежа из ЮKassa.
    /// </summary>
    /// <param name="externalPaymentId">ID платежа во внешней платёжной системе.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Текущий статус платежа.</returns>
    public async Task<Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentStatusResult> GetPaymentStatusAsync(
        string externalPaymentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_options.ApiBaseUrl}/payments/{externalPaymentId}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ЮKassa returned non-success status {Status} for payment {PaymentId}",
                    response.StatusCode, externalPaymentId);
                return new Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentStatusResult
                {
                    PaymentId = externalPaymentId,
                    Status = "expired",
                };
            }

            var jsonText = await response.Content.ReadAsStringAsync(cancellationToken);
            var node = JsonNode.Parse(jsonText);
            if (node is null)
            {
                _logger.LogError("ЮKassa returned empty response body for payment {PaymentId}", externalPaymentId);
                return new Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentStatusResult
                {
                    PaymentId = externalPaymentId,
                    Status = "expired",
                };
            }

            var status = node["status"]?.GetValue<string>() ?? "expired";

            return new Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentStatusResult
            {
                PaymentId = externalPaymentId,
                Status = status,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while getting payment status from ЮKassa for payment {PaymentId}", externalPaymentId);
            return new Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentStatusResult
            {
                PaymentId = externalPaymentId,
                Status = "expired",
            };
        }
    }

    /// <summary>
    /// Формирует JSON-объект тела запроса на создание платежа.
    /// </summary>
    private JsonObject BuildCreatePaymentBody(Core.SubscriptionContext.Application.Contracts.PaymentServices.PaymentRequest request)
    {
        var amount = new JsonObject
        {
            ["value"] = request.AmountRubles.ToString("0.00", CultureInfo.InvariantCulture),
            ["currency"] = "RUB",
        };

        var metadata = new JsonObject
        {
            ["trader_telegram_id"] = request.TraderTelegramId.ToString(CultureInfo.InvariantCulture),
            ["subscription_id"] = request.SubscriptionId.ToString(CultureInfo.InvariantCulture),
        };

        if (request.Period > 0)
        {
            metadata["period"] = request.Period.ToString(CultureInfo.InvariantCulture);
        }

        var body = new JsonObject
        {
            ["amount"] = amount,
            ["capture"] = true,
            ["description"] = request.Description ?? string.Empty,
            ["metadata"] = metadata,
        };

        var confirmation = new JsonObject
        {
            ["type"] = "redirect",
        };

        if (!string.IsNullOrEmpty(_options.ReturnUrl))
        {
            confirmation["return_url"] = _options.ReturnUrl;
        }

        body["confirmation"] = confirmation;

        return body;
    }
}
