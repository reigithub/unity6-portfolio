using Game.Library.Shared.Dto;
using Game.Server.Dto.Responses;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services.Interfaces;
using Npgsql;

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
        var user = await _userRepository.GetByUserIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        return new UserResponse
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Level = user.Level,
            RegisteredAt = new DateTimeOffset(user.RegisteredAt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            AuthType = user.AuthType,
            Email = user.Email,
            HasTransferPassword = !string.IsNullOrEmpty(user.TransferPasswordHash),
        };
    }

    public async Task<Result<UserResponse, ApiError>> UpdateUserAsync(
        string userId, UpdateUserRequest request)
    {
        var user = await _userRepository.GetByUserIdAsync(userId);
        if (user == null)
        {
            return new ApiError("User not found", "USER_NOT_FOUND", StatusCodes.Status404NotFound);
        }

        if (!string.IsNullOrEmpty(request.UserName))
        {
            var existing = await _userRepository.GetByUserNameAsync(request.UserName);
            if (existing != null && existing.Id != user.Id)
            {
                return new ApiError("UserName already exists", "DUPLICATE_NAME", StatusCodes.Status409Conflict);
            }

            user.UserName = request.UserName;
        }

        try
        {
            await _userRepository.UpdateAsync(user);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return new ApiError("UserName already exists", "DUPLICATE_NAME", StatusCodes.Status409Conflict);
        }

        return new UserResponse
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Level = user.Level,
            RegisteredAt = new DateTimeOffset(user.RegisteredAt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            AuthType = user.AuthType,
            Email = user.Email,
            HasTransferPassword = !string.IsNullOrEmpty(user.TransferPasswordHash),
        };
    }
}
