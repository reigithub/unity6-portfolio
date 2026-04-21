using Game.Library.Shared.Dto;

namespace Game.Server.Services.Interfaces;

/// <summary>
/// Dedicated Server レジストリ管理サービスのインターフェース。
/// Valkey Hash で DS 一覧を管理し、ハートビートによる生存確認を行う。
/// </summary>
public interface IUnityServerRegistryService
{
    /// <summary>
    /// Dedicated Server をレジストリに登録する。
    /// </summary>
    /// <param name="request">DS の識別子・アドレス・ポート情報。</param>
    Task RegisterAsync(UnityServerRegistrationRequest request);

    /// <summary>
    /// Dedicated Server をレジストリから削除する。
    /// </summary>
    /// <param name="dsId">削除する DS の識別子。</param>
    Task DeregisterAsync(string dsId);

    /// <summary>
    /// Dedicated Server のハートビートを更新する（TTL 60秒）。
    /// </summary>
    /// <param name="dsId">ハートビートを更新する DS の識別子。</param>
    Task HeartbeatAsync(string dsId);

    /// <summary>
    /// アイドル状態（空き）の DS 一覧を返す。
    /// ハートビートが期限切れの DS は自動的に削除される。
    /// </summary>
    /// <returns>利用可能な DS 情報の配列。</returns>
    Task<DsInfo[]> GetAvailableServersAsync();

    /// <summary>
    /// DS のステータスを更新する。
    /// </summary>
    /// <param name="dsId">対象の DS 識別子。</param>
    /// <param name="status">"idle" または "active"。</param>
    /// <param name="sessionName">アクティブセッションの Fusion セッション名（SessionName）。idle 時は null。</param>
    Task SetStatusAsync(string dsId, string status, string sessionName = null);

    /// <summary>
    /// DS のセッション終了を受け取り、ステータスを idle に戻す。
    /// </summary>
    /// <param name="dsId">セッションが終了した DS の識別子。</param>
    /// <param name="sessionName">終了した Fusion セッション名（SessionName）。</param>
    Task SessionEndedAsync(string dsId, string sessionName);
}

/// <summary>
/// DS レジストリに保存される DS の情報。
/// </summary>
public class DsInfo
{
    /// <summary>
    /// Dedicated Server の一意識別子。
    /// </summary>
    public string DsId { get; set; } = string.Empty;

    /// <summary>
    /// DS のアドレス（IP またはホスト名）。クライアント UDP 接続用の外部 IP。
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// DS の VPC 内部 IP アドレス。Game.Server → DS 間の HTTP 通信（VPC Connector 経由）に使用。
    /// 未設定時は空文字列。その場合は <see cref="Address"/> をフォールバックとして使用する。
    /// </summary>
    public string InternalAddress { get; set; } = string.Empty;

    /// <summary>
    /// Fusion ゲームポート番号。
    /// </summary>
    public int GamePort { get; set; }

    /// <summary>
    /// ヘルスチェックポート番号。
    /// </summary>
    public int HealthPort { get; set; }

    /// <summary>
    /// DS の現在ステータス。"idle"（待機中）または "active"（セッション実行中）。
    /// </summary>
    public string Status { get; set; } = "idle";

    /// <summary>
    /// 現在実行中の Fusion セッション名（SessionName）。idle 時は空文字列。
    /// </summary>
    public string CurrentSessionName { get; set; } = string.Empty;

    /// <summary>
    /// DS の登録日時（UTC）。
    /// </summary>
    public DateTimeOffset RegisteredAt { get; set; }
}
