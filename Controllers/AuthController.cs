using AeriezAlert.Backend.Models;
using AeriezAlert.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace AeriezAlert.Backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly PhoneNotificationService _phoneNotificationService;

    public AuthController(PhoneNotificationService phoneNotificationService)
    {
        _phoneNotificationService = phoneNotificationService;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            return BadRequest("Identifier is required.");
        }

        // Register the user as "connected" or "known"
        _phoneNotificationService.RegisterUser(request.Identifier);

        // Return a simple success or the user object (mocked)
        // For compatibility with frontend that expects a User object, we return a mock one.
        var user = new User 
        { 
            Name = request.Identifier, 
            Email = request.Identifier.Contains("@") ? request.Identifier : null,
            Phone = !request.Identifier.Contains("@") ? request.Identifier : null
        };
        
        return Ok(user);
    }
}

public class LoginRequest
{
    public string Identifier { get; set; } = string.Empty; // Email or Phone
}
