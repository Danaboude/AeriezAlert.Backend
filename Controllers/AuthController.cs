using AeriezAlert.Backend.Models;
using AeriezAlert.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace AeriezAlert.Backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserLookupService _userLookup;

    public AuthController(UserLookupService userLookup)
    {
        _userLookup = userLookup;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.PhoneNumber))
        {
            return BadRequest("Phone number is required.");
        }

        var user = _userLookup.GetUserByPhoneNumber(request.PhoneNumber);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        return Ok(user);
    }
}

public class LoginRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
}
