using ArkWallet.Core.General.Application.Common;
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

        var controller = new SubscriptionsController(mockQueryService.Object);
        controller.AddContext("101");

        var result = await controller.GetSubscriptions();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SubscriptionsResponse>(okResult.Value);

        var subscription = Assert.Single(response.Subscriptions);
        Assert.Equal("Premium", subscription.Name);
        Assert.Equal(100m, subscription.PriceWeekRubles);
        Assert.Equal(290m, subscription.PriceMonthRubles);
        Assert.Equal(2900m, subscription.PriceYearRubles);
        Assert.Equal(20, subscription.MaxOrders);
    }

    [Fact]
    public async Task GetSubscriptions_ServiceReturnsError_ReturnsBadRequest()
    {
        var mockQueryService = new Mock<ISubscriptionQueryService>();
        mockQueryService
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(Result<List<SubscriptionInfo>>.Fail("ошибка"));

        var controller = new SubscriptionsController(mockQueryService.Object);
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

        var controller = new SubscriptionsController(mockQueryService.Object);

        var result = await controller.GetSubscriptions();

        Assert.IsType<UnauthorizedResult>(result);
    }
}