using FancyFinanceWebAPI.Modules.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FancyFinanceWebAPI.Modules.Auth;

public class AuthService : IAuthService
{
    private readonly IUserService _userService;
    private readonly IConfiguration _config;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(IUserService userService, IConfiguration config, IPasswordHasher<User> passwordHasher)
    {
        _userService = userService;
        _config = config;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userService.GetByEmailAsync(request.Email);
        if (user == null)
        {
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.Password, request.Password);
        if (result != PasswordVerificationResult.Success)
            return null;

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        return new LoginResponse { Token = jwt };
    }
}
