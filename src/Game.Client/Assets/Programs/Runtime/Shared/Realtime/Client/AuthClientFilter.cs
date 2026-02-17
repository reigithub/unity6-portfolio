using System;
using System.Threading.Tasks;
using Game.Shared.Services;
using Grpc.Core;
using MagicOnion.Client;
using UnityEngine;

namespace Game.Shared.Realtime.Client
{
    /// <summary>
    /// MagicOnion Client Filter: 全 Unary RPC に認証ヘッダーを自動付与
    /// StreamingHub 用に CreateAuthMetadata() も提供
    /// </summary>
    public class AuthClientFilter : IClientFilter
    {
        private readonly IAuthSessionService _authSessionService;

        public AuthClientFilter(IAuthSessionService authSessionService)
        {
            _authSessionService = authSessionService;
        }

        public async ValueTask<ResponseContext> SendAsync(
            RequestContext context,
            Func<RequestContext, ValueTask<ResponseContext>> next)
        {
            var token = _authSessionService.AuthToken;
            if (!string.IsNullOrEmpty(token))
            {
                var headers = context.CallOptions.Headers;
                if (headers != null)
                {
                    headers.Add("authorization", $"Bearer {token}");
                }
                else
                {
                    Debug.LogWarning("[AuthClientFilter] CallOptions.Headers is null, cannot attach auth token");
                }
            }

            return await next(context);
        }

        /// <summary>
        /// StreamingHub 接続用の認証 Metadata を生成
        /// （StreamingHub は IClientFilter 非対応のため直接 CallOptions に渡す）
        /// </summary>
        public Metadata CreateAuthMetadata()
        {
            var token = _authSessionService.AuthToken;
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning("[AuthClientFilter] No auth token available");
                return new Metadata();
            }
            return new Metadata { { "authorization", $"Bearer {token}" } };
        }
    }
}
