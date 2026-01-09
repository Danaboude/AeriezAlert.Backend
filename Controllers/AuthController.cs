using AeriezAlert.Backend.Models;
using AeriezAlert.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace AeriezAlert.Backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly PhoneNotificationService _phoneNotificationService;

    public AuthController(PhoneNotificationService phoneNotificationService, UserLookupService userLookupService)
    {
        _phoneNotificationService = phoneNotificationService;
        
        // Register mock users from the lookup service into the notification service so they appear "known"
        foreach(var user in userLookupService.GetAllUsers())
        {
             if(!string.IsNullOrEmpty(user.Email)) _phoneNotificationService.RegisterUser(user.Email);
             if(!string.IsNullOrEmpty(user.PhoneNumber)) _phoneNotificationService.RegisterUser(user.PhoneNumber);
        }
    }

    [HttpGet("users")]
    public IActionResult GetAllUsers([FromServices] UserLookupService userLookupService)
    {
        return Ok(userLookupService.GetAllUsers());
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request, [FromServices] UserLookupService userLookupService)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            return BadRequest("Identifier is required.");
        }

        // Validate existence
        var isEmail = request.Identifier.Contains("@");
        var existingUser = isEmail 
            ? userLookupService.GetUserByEmail(request.Identifier) 
            : userLookupService.GetUserByPhone(request.Identifier);

        if (existingUser == null)
        {
             return NotFound("User does not exist in the database.");
        }

        // Register the user as "connected" or "known"
        _phoneNotificationService.RegisterUser(request.Identifier);

        // Return a simple success or the user object (mocked)
        // For compatibility with frontend that expects a User object, we return a mock one.
        var user = new User 
        { 
            // Map Identifier to UserId as a display name equivalent
            UserId = existingUser.UserId, 
            Email = existingUser.Email,
            PhoneNumber = existingUser.PhoneNumber
        };
        
        return Ok(user);
    }
}

public class LoginRequest
{
    public string Identifier { get; set; } = string.Empty; // Email or Phone
}
