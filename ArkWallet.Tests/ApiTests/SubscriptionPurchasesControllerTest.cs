using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;
using ArkWallet.Presentation.API;
using ArkWallet.Presentation.DTOs;
using ArkWallet.Tests.HelpTools;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ArkWallet.Tests.ApiTests;

public class SubscriptionPurchasesControllerTest
{
    [Fact]
    public async Task Purchase_ValidPeriod_ReturnsOkWithResult()
    {
        var mockPurchaseService = new Mock<IPurchaseService>();
        mockPurchaseService
            .Setup(p => p.PurchaseAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<SubscriptionPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseResult(true, "Подписка приобретена на неделю", "TXN-1", new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc)));

        var controller = new SubscriptionPurchasesController(mockPurchaseService.Object);
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

        var controller = new SubscriptionPurchasesController(mockPurchaseService.Object);
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

        var controller = new SubscriptionPurchasesController(mockPurchaseService.Object);
        controller.AddContext("101");

        var result = await controller.Purchase(new PurchaseSubscriptionRequest { SubscriptionId = 2, Period = "month" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var r = Assert.IsType<PurchaseSubscriptionResponse>(badRequest.Value);

        Assert.False(r.Success);
    }
}