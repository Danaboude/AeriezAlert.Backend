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
        return Ok(new { 
            isConfigured = _userLookupService.IsConfigured(),
            settings = _userLookupService.GetCurrentSettings()
        });
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
        if (string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            return BadRequest(new { message = "Base URL is required." });
        }

        bool isValid = await _userLookupService.ValidateAndSetSettingsAsync(request.BaseUrl, request.Token, request.RefreshIntervalMinutes);

        if (isValid)
        {
            return Ok(new { message = "Settings verified and saved successfully." });
        }
        else
        {
            return BadRequest(new { message = "Invalid Settings or unable to connect to the Aeriez API." });
        }
    }
}

public class TokenRequest
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public int RefreshIntervalMinutes { get; set; } = 30;
}
