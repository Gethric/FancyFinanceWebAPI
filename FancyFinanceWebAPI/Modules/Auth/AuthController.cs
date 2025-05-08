using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FancyFinanceWebAPI.Modules.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var response = await _authService.LoginAsync(request);
        if (response == null)
            return Unauthorized(new { message = "Invalid credentials" });

        return Ok(response);
    }

    [HttpPost("validate")]
    public IActionResult ValidateToken()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
            return Unauthorized();

        // Optional: regenerate token here (if you're doing refresh logic)
        // For now, just return 200 OK to indicate token is valid
        return Ok(new { valid = true });
    }

}
