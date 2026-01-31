using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;

namespace Game.Server.Services.Interfaces;

public interface IAuthService
{
    Task<Result<LoginResponse, ApiError>> RegisterAsync(RegisterRequest request);

    Task<Result<LoginResponse, ApiError>> LoginAsync(LoginRequest request);

    Task<Result<LoginResponse, ApiError>> RefreshTokenAsync(string userId);
}
