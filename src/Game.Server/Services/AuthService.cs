using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Game.Server.Configuration;
using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Game.Server.Services;

public class AuthService : Interfaces.IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly JwtSettings _jwtSettings;

    public AuthService(IAuthRepository authRepository, IOptions<JwtSettings> jwtSettings)
    {
        _authRepository = authRepository;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<Result<LoginResponse, ApiError>> RegisterAsync(RegisterRequest request)
    {
        bool exists = await _authRepository.ExistsByDisplayNameAsync(request.DisplayName);

        if (exists)
        {
            return new ApiError("DisplayName already exists", "DUPLICATE_NAME", StatusCodes.Status409Conflict);
        }

        var user = new UserInfo
        {
            DisplayName = request.DisplayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        };

        await _authRepository.CreateUserAsync(user);

        string token = GenerateJwtToken(user);
        return new LoginResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Token = token,
        };
    }

    public async Task<Result<LoginResponse, ApiError>> LoginAsync(LoginRequest request)
    {
        var user = await _authRepository.GetByDisplayNameAsync(request.DisplayName);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return new ApiError("Invalid credentials", "INVALID_CREDENTIALS", StatusCodes.Status401Unauthorized);
        }

        await _authRepository.UpdateLastLoginAsync(user.Id, DateTime.UtcNow);

        string token = GenerateJwtToken(user);
        return new LoginResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Token = token,
        };
    }

    public async Task<Result<LoginResponse, ApiError>> RefreshTokenAsync(string userId)
    {
        var user = await _authRepository.GetByIdAsync(userId);

        if (user == null)
        {
            return new ApiError("User not found", "USER_NOT_FOUND", StatusCodes.Status404NotFound);
        }

        string token = GenerateJwtToken(user);
        return new LoginResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Token = token,
        };
    }

    private string GenerateJwtToken(UserInfo user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim("level", user.Level.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
