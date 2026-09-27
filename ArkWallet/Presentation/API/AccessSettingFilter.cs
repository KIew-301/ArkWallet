using ArkWallet.Infrastructure.AccessControl;
using ArkWallet.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ArkWallet.Presentation.API;

internal class AccessSettingFilter(AccessControlService accessControl, ArkWalletDbContext dbContext, IWebHostEnvironment environment) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (environment.EnvironmentName == "Testing")
            return;

        var idClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (idClaim == null || !long.TryParse(idClaim.Value, out var traderId))
            return;

        var trader = await dbContext.Traders
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == traderId);
        if (trader == null)
            return;

        var Id = trader.Id;

        if (!accessControl.IsAuthorized(Id!))
        {
            context.Result = new StatusCodeResult(403);
        }
    }
}