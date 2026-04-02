using Game.Server.Dto.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Game.Server.Tests.Helpers;

/// <summary>
/// Result型のテスト検証ヘルパー。
/// Result.Match() から成功値/エラー値を抽出する。
/// </summary>
public static class ResultTestHelper
{
    public static TSuccess? ExtractSuccess<TSuccess, TError>(Result<TSuccess, TError> result)
    {
        TSuccess? success = default;
        result.Match(
            s => { success = s; return new OkResult(); },
            e => new OkResult());
        return success;
    }

    public static TError? ExtractError<TSuccess, TError>(Result<TSuccess, TError> result)
    {
        TError? error = default;
        result.Match(
            s => new OkResult(),
            e => { error = e; return new OkResult(); });
        return error;
    }
}
