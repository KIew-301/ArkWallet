using ArkWallet.Infrastructure.AccessControl;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace ArkWallet.Presentation.API;

internal class AccessSettingFilter(AccessControlService accessControl) : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var telegramIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (telegramIdClaim == null || !long.TryParse(telegramIdClaim.Value, out var telegramId))
            return;

        if (!accessControl.IsAuthorized(telegramId))
        {
            context.Result = new StatusCodeResult(403);
        }
    }
}
