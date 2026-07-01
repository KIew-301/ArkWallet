using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArkWallet.Presentation.API;

[ApiController]
[Route("api/v1/[controller]")]
public class TokensController(ITokenQueryService tokenQueryService) : ControllerBase
{
    [Authorize]
    [HttpGet("token")]
    public async Task<IActionResult> GetToken()
    {
        var result = await tokenQueryService.GetAllActiveTokensAsync();

        if (!result.TryGetData(out var data))
            return BadRequest(result.Message);

        return Ok(new GetTokenListResponse(data.ToArray()));
    }
}