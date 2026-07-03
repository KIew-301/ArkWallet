using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Services.Other;
using ArkWallet.Presentation.API;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace ArkWallet.Tests.ApiTests;

public class AuthControllerTest
{
    [Fact]
    public async Task Login_WhenTraderNotRegistered_RegistersAndReturnsToken()
    {
        var authController = BuildAuthController(false, true, true);

        var request = new LoginRequest("data");
        var result = await authController.Login(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(okResult.Value);
        Assert.Equal("token", response.Token);
    }

    [Fact]
    public async Task Login_WhenTraderAlreadyRegistered_ReturnsTokenWithoutRegistration()
    {
        var authController = BuildAuthController(true, true, true);

        var request = new LoginRequest("data");
        var result = await authController.Login(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(okResult.Value);
        Assert.Equal("token", response.Token);
    }

    [Fact]
    public async Task Login_WhenAuthFails_ReturnsUnauthorized()
    {
        var authController = BuildAuthController(false, false, true);

        var request = new LoginRequest("data");
        var result = await authController.Login(request);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Login_WhenRegistrationFails_ReturnsBadRequest()
    {
        var authController = BuildAuthController(false, true, false);

        var request = new LoginRequest("data");
        var result = await authController.Login(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private AuthController BuildAuthController(bool isTraderRegistered, bool isAuthSuccess, bool canRegister)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telegram:BotToken:Main"] = "test_bot_token"
            })
            .Build();

        var mockTraderAuthService = new Mock<ITraderAuthService>();
        mockTraderAuthService
            .Setup(x => x.AuthenticateUser(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(isAuthSuccess
            ? Result<TelegramInitData>.Ok(new(new(101, "User", "lastname", "username", "ru", false, true, "url"), "date", "chat_id", "chat_type"))
            : Result<TelegramInitData>.Fail("Ошибка аутентификации"));

        var mockTraderRegistrationService = new Mock<ITraderRegistrationService>();
        mockTraderRegistrationService
            .Setup(x => x.CheckTraderAlreadyRegistered(It.IsAny<long>()))
            .ReturnsAsync(isTraderRegistered);
        mockTraderRegistrationService
            .Setup(x => x.RegisterTraderAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(canRegister ? Result.Ok() : Result.Fail("Ошибка регистрации"));

        var mockTokenService = new Mock<ITokenService>();
        mockTokenService
            .Setup(x => x.GenerateToken(It.IsAny<long>()))
            .Returns("token");

        var authController = new AuthController(
            mockTraderRegistrationService.Object, config,
            mockTokenService.Object, mockTraderAuthService.Object);

        return authController;
    }
}
