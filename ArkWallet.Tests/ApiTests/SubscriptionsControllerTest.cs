using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionServices;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;
using ArkWallet.Presentation.API;
using ArkWallet.Presentation.DTOs;
using ArkWallet.Tests.HelpTools;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ArkWallet.Tests.ApiTests;

public class SubscriptionsControllerTest
{
    [Fact]
    public async Task GetSubscriptions_ServiceReturnsData_ReturnsOkWithSubscriptions()
    {
        var mockQueryService = new Mock<ISubscriptionQueryService>();
        mockQueryService
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(Result<List<SubscriptionInfo>>.Ok(new List<SubscriptionInfo>
            {
                new(1, "Premium", 2, 500m, 100m, 290m, 2900m, 20, 10, 10080)
            }));

        var controller = new SubscriptionsController(mockQueryService.Object, null!);
        controller.AddContext("101");

        var result = await controller.GetSubscriptions();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SubscriptionsResponse>(okResult.Value);

        Assert.Equal(1, response.Subscriptions.Length);
        Assert.Equal("Premium", response.Subscriptions[0].Name);
        Assert.Equal(100m, response.Subscriptions[0].PriceWeekRubles);
        Assert.Equal(290m, response.Subscriptions[0].PriceMonthRubles);
        Assert.Equal(2900m, response.Subscriptions[0].PriceYearRubles);
        Assert.Equal(20, response.Subscriptions[0].MaxOrders);
    }

    [Fact]
    public async Task GetSubscriptions_ServiceReturnsError_ReturnsBadRequest()
    {
        var mockQueryService = new Mock<ISubscriptionQueryService>();
        mockQueryService
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(Result<List<SubscriptionInfo>>.Fail("ошибка"));

        var controller = new SubscriptionsController(mockQueryService.Object, null!);
        controller.AddContext("101");

        var result = await controller.GetSubscriptions();

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetSubscriptions_WithoutContext_ReturnsUnauthorized()
    {
        var mockQueryService = new Mock<ISubscriptionQueryService>();
        mockQueryService
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(Result<List<SubscriptionInfo>>.Ok(new List<SubscriptionInfo>()));

        var controller = new SubscriptionsController(mockQueryService.Object, null!);

        var result = await controller.GetSubscriptions();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Purchase_ValidPeriod_ReturnsOkWithResult()
    {
        var mockPurchaseService = new Mock<IPurchaseService>();
        mockPurchaseService
            .Setup(p => p.PurchaseAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<SubscriptionPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseResult(true, "Подписка приобретена на неделю", "TXN-1", new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc)));

        var mockQueryService = new Mock<ISubscriptionQueryService>();
        mockQueryService
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(Result<List<SubscriptionInfo>>.Ok(new List<SubscriptionInfo>()));

        var controller = new SubscriptionsController(mockQueryService.Object, mockPurchaseService.Object);
        controller.AddContext("101");

        var result = await controller.Purchase(new PurchaseSubscriptionRequest { SubscriptionId = 2, Period = "week" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var r = Assert.IsType<PurchaseSubscriptionResponse>(ok.Value);

        Assert.True(r.Success);
        Assert.Equal("TXN-1", r.TransactionId);
        Assert.Equal(new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc), r.ExpiresAtUtc);

        mockPurchaseService.Verify(p => p.PurchaseAsync(101, 2, SubscriptionPeriod.Week, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Purchase_InvalidPeriod_ReturnsBadRequest()
    {
        var mockPurchaseService = new Mock<IPurchaseService>();
        mockPurchaseService
            .Setup(p => p.PurchaseAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<SubscriptionPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseResult(true, "OK", null, default));

        var mockQueryService = new Mock<ISubscriptionQueryService>();
        mockQueryService
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(Result<List<SubscriptionInfo>>.Ok(new List<SubscriptionInfo>()));

        var controller = new SubscriptionsController(mockQueryService.Object, mockPurchaseService.Object);
        controller.AddContext("101");

        var result = await controller.Purchase(new PurchaseSubscriptionRequest { SubscriptionId = 2, Period = "день" });

        Assert.IsType<BadRequestObjectResult>(result);

        mockPurchaseService.Verify(p => p.PurchaseAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<SubscriptionPeriod>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Purchase_ServiceReturnsFail_ReturnsBadRequest()
    {
        var mockPurchaseService = new Mock<IPurchaseService>();
        mockPurchaseService
            .Setup(p => p.PurchaseAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<SubscriptionPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PurchaseResult.Fail("Платеж не прошел"));

        var mockQueryService = new Mock<ISubscriptionQueryService>();
        mockQueryService
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(Result<List<SubscriptionInfo>>.Ok(new List<SubscriptionInfo>()));

        var controller = new SubscriptionsController(mockQueryService.Object, mockPurchaseService.Object);
        controller.AddContext("101");

        var result = await controller.Purchase(new PurchaseSubscriptionRequest { SubscriptionId = 2, Period = "month" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var r = Assert.IsType<PurchaseSubscriptionResponse>(badRequest.Value);

        Assert.False(r.Success);
    }
}
