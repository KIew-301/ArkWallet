using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ArkWallet.Tests.HelpTools;

public static class ControllerExtention
{
    public static void AddContext(this ControllerBase controller, string telegramId)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, telegramId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new() { HttpContext = new DefaultHttpContext { User = principal } };
    }
}
