using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services.Interfaces;

namespace Game.Server.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserResponse?> GetUserAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        return new UserResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Level = user.Level,
            CreatedAt = new DateTimeOffset(user.CreatedAt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            AuthType = user.AuthType,
            Email = user.Email,
        };
    }

    public async Task<Result<UserResponse, ApiError>> UpdateUserAsync(
        string userId, UpdateUserRequest request)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return new ApiError("User not found", "USER_NOT_FOUND", StatusCodes.Status404NotFound);
        }

        if (!string.IsNullOrEmpty(request.DisplayName))
        {
            var existing = await _userRepository.GetByDisplayNameAsync(request.DisplayName);
            if (existing != null && existing.Id != userId)
            {
                return new ApiError("DisplayName already exists", "DUPLICATE_NAME", StatusCodes.Status409Conflict);
            }

            user.DisplayName = request.DisplayName;
        }

        await _userRepository.UpdateAsync(user);

        return new UserResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Level = user.Level,
            CreatedAt = new DateTimeOffset(user.CreatedAt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            AuthType = user.AuthType,
            Email = user.Email,
        };
    }
}
