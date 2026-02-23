using AeriezAlert.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AeriezAlert.Backend.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly UserLookupService _userLookupService;

    public SettingsController(UserLookupService userLookupService)
    {
        _userLookupService = userLookupService;
    }

    /// <summary>
    /// Checks if the API Token is configured and valid
    /// </summary>
    [HttpGet("token-status")]
    public IActionResult GetTokenStatus()
    {
        return Ok(new { isConfigured = _userLookupService.IsTokenConfigured() });
    }

    /// <summary>
    /// Validates and saves a new API Token
    /// </summary>
    [HttpPost("token")]
    public async Task<IActionResult> SetToken([FromBody] TokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { message = "Token is required." });
        }

        bool isValid = await _userLookupService.ValidateAndSetTokenAsync(request.Token);

        if (isValid)
        {
            return Ok(new { message = "Token verified and saved successfully." });
        }
        else
        {
            return BadRequest(new { message = "Invalid Token or unable to connect to the Aeriez API." });
        }
    }
}

public class TokenRequest
{
    public string Token { get; set; } = string.Empty;
}
