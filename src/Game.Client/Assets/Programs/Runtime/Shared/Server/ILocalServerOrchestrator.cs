using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Shared.Server
{
    /// <summary>
    /// SP モード用ローカルサーバーオーケストレーターのインターフェース。
    /// クライアント: LocalServerOrchestrator（PG + Valkey + Game.Server + Headless を起動）
    /// サーバー: NullLocalServerOrchestrator（no-op）
    /// </summary>
    public interface ILocalServerOrchestrator : IDisposable
    {
        UniTask StartAsync(CancellationToken ct = default);
        bool IsReady { get; }
        ushort HeadlessServerPort { get; }
    }
}
