using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;

namespace Game.Server.Services.Interfaces;

public interface IUserService
{
    Task<UserResponse?> GetUserAsync(string userId);

    Task<Result<UserResponse, ApiError>> UpdateUserAsync(string userId, UpdateUserRequest request);
}
