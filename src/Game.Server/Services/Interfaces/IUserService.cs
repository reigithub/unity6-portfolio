using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;

namespace Game.Server.Services.Interfaces;

public interface IUserService
{
    Task<UserResponse?> GetUserAsync(Guid id);

    Task<Result<UserResponse, ApiError>> UpdateUserAsync(Guid id, UpdateUserRequest request);
}
