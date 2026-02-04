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

    public async Task<UserResponse?> GetUserAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return null;
        }

        return new UserResponse
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Level = user.Level,
            CreatedAt = new DateTimeOffset(user.CreatedAt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            AuthType = user.AuthType,
            Email = user.Email,
        };
    }

    public async Task<Result<UserResponse, ApiError>> UpdateUserAsync(
        Guid id, UpdateUserRequest request)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return new ApiError("User not found", "USER_NOT_FOUND", StatusCodes.Status404NotFound);
        }

        if (!string.IsNullOrEmpty(request.UserName))
        {
            var existing = await _userRepository.GetByUserNameAsync(request.UserName);
            if (existing != null && existing.Id != id)
            {
                return new ApiError("UserName already exists", "DUPLICATE_NAME", StatusCodes.Status409Conflict);
            }

            user.UserName = request.UserName;
        }

        await _userRepository.UpdateAsync(user);

        return new UserResponse
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Level = user.Level,
            CreatedAt = new DateTimeOffset(user.CreatedAt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            AuthType = user.AuthType,
            Email = user.Email,
        };
    }
}
