using AeriezAlert.Backend.Models;
using AeriezAlert.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace AeriezAlert.Backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly PhoneNotificationService _phoneNotificationService;
    private readonly UserLookupService _userLookupService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        PhoneNotificationService phoneNotificationService, 
        UserLookupService userLookupService,
        ILogger<AuthController> logger)
    {
        _phoneNotificationService = phoneNotificationService;
        _userLookupService = userLookupService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user by email or phone number
    /// Triggers registration of the user as Active
    /// </summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            return BadRequest("Identifier is required.");
        }

        // Register the user as "active" in our lookup service
        _userLookupService.RegisterActiveUser(request.Identifier);
        
        // Also register with phone notification service if needed
        _phoneNotificationService.RegisterUser(request.Identifier);

        _logger.LogInformation("User logged in (registered active): {Identifier}", request.Identifier);

        return Ok(new { Message = "User registered as active" });
    }

    /// <summary>
    /// Gets the list of valid users loaded from the API
    /// </summary>
    [HttpGet("users")]
    public IActionResult GetUsers()
    {
        return Ok(_userLookupService.GetValidUsers());
    }
}

public class LoginRequest
{
    public string Identifier { get; set; } = string.Empty; // Email or Phone
}
