using System.ComponentModel.DataAnnotations;
using Game.Library.Shared.Dto;

namespace Game.Server.Tests.Validation;

public class DtoValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    #region MatchmakingRequest

    [Fact]
    public void MatchmakingRequest_ValidGameMode_Passes()
    {
        var request = new MatchmakingRequest { GameMode = "survival" };
        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    [Fact]
    public void MatchmakingRequest_EmptyGameMode_Fails()
    {
        var request = new MatchmakingRequest { GameMode = "" };
        var results = ValidateModel(request);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void MatchmakingRequest_GameModeTooLong_Fails()
    {
        var request = new MatchmakingRequest { GameMode = new string('a', 31) };
        var results = ValidateModel(request);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void MatchmakingRequest_GameModeAtMaxLength_Passes()
    {
        var request = new MatchmakingRequest { GameMode = new string('a', 30) };
        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    #endregion

    #region CreateLobbyRequest

    [Fact]
    public void CreateLobbyRequest_Valid_Passes()
    {
        var request = new CreateLobbyRequest
        {
            LobbyName = "My Lobby",
            GameMode = "survival",
            MaxPlayers = 4,
            PlayerName = "Player1",
        };
        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    [Fact]
    public void CreateLobbyRequest_EmptyLobbyName_Fails()
    {
        var request = new CreateLobbyRequest
        {
            LobbyName = "",
            GameMode = "survival",
            MaxPlayers = 4,
            PlayerName = "Player1",
        };
        var results = ValidateModel(request);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void CreateLobbyRequest_EmptyPlayerName_Fails()
    {
        var request = new CreateLobbyRequest
        {
            LobbyName = "Lobby",
            GameMode = "survival",
            MaxPlayers = 4,
            PlayerName = "",
        };
        var results = ValidateModel(request);
        Assert.NotEmpty(results);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateLobbyRequest_MaxPlayersOutOfRange_Fails(int maxPlayers)
    {
        var request = new CreateLobbyRequest
        {
            LobbyName = "Lobby",
            GameMode = "survival",
            MaxPlayers = maxPlayers,
            PlayerName = "Player1",
        };
        var results = ValidateModel(request);
        Assert.NotEmpty(results);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(16)]
    public void CreateLobbyRequest_MaxPlayersInRange_Passes(int maxPlayers)
    {
        var request = new CreateLobbyRequest
        {
            LobbyName = "Lobby",
            GameMode = "survival",
            MaxPlayers = maxPlayers,
            PlayerName = "Player1",
        };
        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    #endregion

    #region ChatMessage

    [Fact]
    public void ChatMessage_ValidContent_Passes()
    {
        var msg = new ChatMessage
        {
            UserId = "user123",
            Content = "Hello world",
        };
        var results = ValidateModel(msg);
        Assert.Empty(results);
    }

    [Fact]
    public void ChatMessage_EmptyContent_Fails()
    {
        var msg = new ChatMessage
        {
            UserId = "user123",
            Content = "",
        };
        var results = ValidateModel(msg);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void ChatMessage_ContentTooLong_Fails()
    {
        var msg = new ChatMessage
        {
            UserId = "user123",
            Content = new string('x', 501),
        };
        var results = ValidateModel(msg);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void ChatMessage_ContentAtMaxLength_Passes()
    {
        var msg = new ChatMessage
        {
            UserId = "user123",
            Content = new string('x', 500),
        };
        var results = ValidateModel(msg);
        Assert.Empty(results);
    }

    #endregion

    #region CreateChatRoomRequest

    [Fact]
    public void CreateChatRoomRequest_Valid_Passes()
    {
        var request = new CreateChatRoomRequest
        {
            RoomName = "General",
            RoomType = "public",
            MaxMembers = 50,
        };
        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(101)]
    public void CreateChatRoomRequest_MaxMembersOutOfRange_Fails(int maxMembers)
    {
        var request = new CreateChatRoomRequest
        {
            RoomName = "General",
            RoomType = "public",
            MaxMembers = maxMembers,
        };
        var results = ValidateModel(request);
        Assert.NotEmpty(results);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(100)]
    public void CreateChatRoomRequest_MaxMembersInRange_Passes(int maxMembers)
    {
        var request = new CreateChatRoomRequest
        {
            RoomName = "General",
            RoomType = "public",
            MaxMembers = maxMembers,
        };
        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    #endregion

    #region InviteMemberRequest

    [Fact]
    public void InviteMemberRequest_Valid_Passes()
    {
        var request = new InviteMemberRequest
        {
            TargetUserId = "user456",
            PlayerName = "Player2",
        };
        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    [Fact]
    public void InviteMemberRequest_TargetUserIdTooLong_Fails()
    {
        var request = new InviteMemberRequest
        {
            TargetUserId = new string('u', 129),
            PlayerName = "Player2",
        };
        var results = ValidateModel(request);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void InviteMemberRequest_EmptyPlayerName_Fails()
    {
        var request = new InviteMemberRequest
        {
            TargetUserId = "user456",
            PlayerName = "",
        };
        var results = ValidateModel(request);
        Assert.NotEmpty(results);
    }

    #endregion
}
