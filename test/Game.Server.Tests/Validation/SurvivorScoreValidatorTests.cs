using Game.Library.Shared.Dto;
using Game.Server.MasterData;
using Game.Server.Services.Interfaces;
using Game.Server.Shared.Exceptions;
using Game.Server.Validation;
using MasterMemory;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Logging;
using Moq;

namespace Game.Server.Tests.Validation;

public class SurvivorScoreValidatorTests
{
    // テストデータ: StageId=1, TimeLimit=120, 3ウェーブ
    //   Wave 1: ScoreMultiplier=100
    //   Wave 2: ScoreMultiplier=150
    //   Wave 3: ScoreMultiplier=200
    // Score 上限 (WaveReached=3): 120×(100+150+200) = 54,000

    private readonly SurvivorScoreValidator _validator;

    public SurvivorScoreValidatorTests()
    {
        var resolver = CompositeResolver.Create(MasterMemoryResolver.Instance, StandardResolver.Instance);
        var builder = new DatabaseBuilder(resolver);

        builder.Append(new[]
        {
            new SurvivorStageMaster
            {
                Id = 1, Name = "TestStage", AssetName = "", ThumbnailAssetName = "",
                Description = "", BgmAssetName = "", TimeLimit = 120, Difficulty = 1,
            },
        });
        builder.Append(new[]
        {
            new SurvivorStageWaveMaster { Id = 1, StageId = 1, WaveNumber = 1, ScoreMultiplier = 100, TargetKillCount = 10 },
            new SurvivorStageWaveMaster { Id = 2, StageId = 1, WaveNumber = 2, ScoreMultiplier = 150, TargetKillCount = 15 },
            new SurvivorStageWaveMaster { Id = 3, StageId = 1, WaveNumber = 3, ScoreMultiplier = 200, TargetKillCount = 20 },
        });

        byte[] binary = builder.Build();
        var db = new MemoryDatabase(binary, formatterResolver: resolver);

        var mockMasterData = new Mock<IMasterDataService>();
        mockMasterData.Setup(m => m.MemoryDatabase).Returns(db);

        _validator = new SurvivorScoreValidator(
            mockMasterData.Object,
            Mock.Of<ILogger<SurvivorScoreValidator>>());
    }

    private static ScoreSubmitDto ValidRequest() => new()
    {
        StageId = 1,
        Score = 10000,
        ClearTime = 60f,
        WaveReached = 2,
        EnemiesDefeated = 30,
    };

    // --- 1. ステージ存在チェック ---

    [Fact]
    public void Validate_InvalidStageId_Throws()
    {
        var request = ValidRequest();
        request.StageId = 999;

        var ex = Assert.Throws<ErrorException>(() => _validator.Validate(request));
        Assert.Equal("INVALID_SCORE", ex.ErrorCode);
    }

    // --- 2. Score >= 0 ---

    [Fact]
    public void Validate_NegativeScore_Throws()
    {
        var request = ValidRequest();
        request.Score = -1;

        var ex = Assert.Throws<ErrorException>(() => _validator.Validate(request));
        Assert.Equal("INVALID_SCORE", ex.ErrorCode);
    }

    // --- 3. EnemiesDefeated >= 0 ---

    [Fact]
    public void Validate_NegativeEnemiesDefeated_Throws()
    {
        var request = ValidRequest();
        request.EnemiesDefeated = -1;

        var ex = Assert.Throws<ErrorException>(() => _validator.Validate(request));
        Assert.Equal("INVALID_SCORE", ex.ErrorCode);
    }

    // --- 4. ClearTime 上限 ---

    [Fact]
    public void Validate_ClearTimeExceedsLimit_Throws()
    {
        var request = ValidRequest();
        request.ClearTime = 126f; // TimeLimit(120) + buffer(5) を超過

        var ex = Assert.Throws<ErrorException>(() => _validator.Validate(request));
        Assert.Equal("INVALID_SCORE", ex.ErrorCode);
    }

    // --- 5. ClearTime > 0 ---

    [Fact]
    public void Validate_ZeroClearTime_Throws()
    {
        var request = ValidRequest();
        request.ClearTime = 0f;

        var ex = Assert.Throws<ErrorException>(() => _validator.Validate(request));
        Assert.Equal("INVALID_SCORE", ex.ErrorCode);
    }

    [Fact]
    public void Validate_NegativeClearTime_Throws()
    {
        var request = ValidRequest();
        request.ClearTime = -1f;

        var ex = Assert.Throws<ErrorException>(() => _validator.Validate(request));
        Assert.Equal("INVALID_SCORE", ex.ErrorCode);
    }

    // --- 6. WaveReached 範囲 ---

    [Fact]
    public void Validate_WaveReachedNegative_Throws()
    {
        var request = ValidRequest();
        request.WaveReached = -1;

        var ex = Assert.Throws<ErrorException>(() => _validator.Validate(request));
        Assert.Equal("INVALID_SCORE", ex.ErrorCode);
    }

    [Fact]
    public void Validate_WaveReachedExceedsMax_Throws()
    {
        var request = ValidRequest();
        request.WaveReached = 4; // 最大3

        var ex = Assert.Throws<ErrorException>(() => _validator.Validate(request));
        Assert.Equal("INVALID_SCORE", ex.ErrorCode);
    }

    [Fact]
    public void Validate_WaveReachedZero_Passes()
    {
        var request = ValidRequest();
        request.WaveReached = 0;
        request.Score = 0; // WaveReached=0 なら Score=0 のみ有効

        _validator.Validate(request); // 例外なし
    }

    [Fact]
    public void Validate_WaveReachedEqualsMax_Passes()
    {
        var request = ValidRequest();
        request.WaveReached = 3;
        request.Score = 50000; // 上限 54,000 以内

        _validator.Validate(request); // 例外なし
    }

    // --- 7. Score 上限 ---

    [Fact]
    public void Validate_ScoreExceedsUpperBound_Throws()
    {
        var request = ValidRequest();
        request.WaveReached = 3;
        request.Score = 54001; // 上限 54,000 を超過

        var ex = Assert.Throws<ErrorException>(() => _validator.Validate(request));
        Assert.Equal("INVALID_SCORE", ex.ErrorCode);
    }

    [Fact]
    public void Validate_ScoreAtUpperBound_Passes()
    {
        var request = ValidRequest();
        request.WaveReached = 3;
        request.Score = 54000; // 上限ちょうど: 120×(100+150+200) = 54,000

        _validator.Validate(request); // 例外なし
    }

    [Fact]
    public void Validate_WaveReachedZero_OnlyZeroScoreValid()
    {
        var request = ValidRequest();
        request.WaveReached = 0;
        request.Score = 1; // WaveReached=0 → 上限は 0、Score > 0 は不正

        var ex = Assert.Throws<ErrorException>(() => _validator.Validate(request));
        Assert.Equal("INVALID_SCORE", ex.ErrorCode);
    }

    // --- 全フィールド正常 ---

    [Fact]
    public void Validate_AllFieldsValid_Passes()
    {
        _validator.Validate(ValidRequest()); // 例外なし
    }
}
