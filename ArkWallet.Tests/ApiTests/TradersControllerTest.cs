using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Presentation.API;
using ArkWallet.Presentation.DTOs;
using ArkWallet.Tests.HelpTools;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ArkWallet.Tests.ApiTests;

public class TradersControllerTest
{
    [Fact]
    public async Task TakeBalance_WhenCalcServiceReturnData_ReturnsSuccess()
    {
        var mockBalanceChangesCalculationService = new Mock<IBalanceChangesCalculationService>();
        mockBalanceChangesCalculationService
            .Setup(x => x.TakeBalanceChanges(It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(Result<BalanceChangesBundle>
                .Ok(new BalanceChangesBundle(
                    new BalanceChangesData(1250, 1000, 250, 25),
                    new BalanceChangesData(1250, 1000, 250, 25))));

        var traderController = new TradersController(mockBalanceChangesCalculationService.Object);
        traderController.AddContext("101");

        var result = await traderController.GetBalance(new GetBalanceRequest(5));

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GetBalanceResponse>(okResult.Value);

        Assert.Equal(1250, response.MainBalance.CurrentBalance);
        Assert.Equal(250, response.MainBalance.ChangeAbsolute);
        Assert.Equal(25, response.MainBalance.ChangePercent);
        Assert.Equal(1250, response.TotalBalance.CurrentBalance);
        Assert.Equal(250, response.TotalBalance.ChangeAbsolute);
        Assert.Equal(25, response.TotalBalance.ChangePercent);
    }

    [Fact]
    public async Task TakeBalance_WhenCalcServiceReturnError_ReturnsBadRequest()
    {
        var mockBalanceChangesCalculationService = new Mock<IBalanceChangesCalculationService>();
        mockBalanceChangesCalculationService
            .Setup(x => x.TakeBalanceChanges(It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(Result<BalanceChangesBundle>
                .Fail("Calculation service error"));

        var traderController = new TradersController(mockBalanceChangesCalculationService.Object);
        traderController.AddContext("101");

        var result = await traderController.GetBalance(new GetBalanceRequest(5));

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var message = badRequestResult.Value;

        Assert.Equal("Calculation service error", message);
    }

    [Fact]
    public async Task TakeBalance_WhenContextNotExist_ReturnsUnauthorized()
    {
        var mockBalanceChangesCalculationService = new Mock<IBalanceChangesCalculationService>();
        mockBalanceChangesCalculationService
            .Setup(x => x.TakeBalanceChanges(It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(Result<BalanceChangesBundle>
                .Fail("Calculation service error"));

        var traderController = new TradersController(mockBalanceChangesCalculationService.Object);

        var result = await traderController.GetBalance(new GetBalanceRequest(5));

        var badRequestResult = Assert.IsType<UnauthorizedResult>(result);
    }
}
