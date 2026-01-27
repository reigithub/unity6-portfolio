using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Enums;

namespace Game.Shared.Services
{
    /// <summary>
    /// オーディオ再生サービスの共通インターフェース
    /// MVC/MVP両方で使用
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// オーディオサービスを初期化・起動する
        /// アプリケーション起動時に呼び出す
        /// </summary>
        void Startup();

        /// <summary>
        /// オーディオサービスをシャットダウンする
        /// リソースの解放と再生中のオーディオの停止を行う
        /// </summary>
        void Shutdown();

        /// <summary>
        /// BGM（バックグラウンドミュージック）を再生する
        /// </summary>
        /// <param name="assetName">再生するBGMのアセット名</param>
        /// <param name="token">キャンセレーショントークン</param>
        UniTask PlayBgmAsync(string assetName, CancellationToken token = default);

        /// <summary>
        /// 現在再生中のBGMを停止する
        /// </summary>
        /// <param name="token">キャンセレーショントークン</param>
        UniTask StopBgmAsync(CancellationToken token = default);

        /// <summary>
        /// ボイス音声を再生する
        /// </summary>
        /// <param name="assetName">再生するボイスのアセット名</param>
        /// <param name="token">キャンセレーショントークン</param>
        UniTask PlayVoiceAsync(string assetName, CancellationToken token = default);

        /// <summary>
        /// 効果音（SE）を再生する
        /// </summary>
        /// <param name="assetName">再生する効果音のアセット名</param>
        /// <param name="token">キャンセレーショントークン</param>
        UniTask PlaySoundEffectAsync(string assetName, CancellationToken token = default);

        /// <summary>
        /// 指定したカテゴリとアセット名でオーディオを再生する
        /// </summary>
        /// <param name="audioCategory">オーディオカテゴリ（BGM, Voice, SFX等）</param>
        /// <param name="audioName">オーディオアセット名</param>
        /// <param name="token">キャンセレーショントークン</param>
        UniTask PlayAsync(AudioCategory audioCategory, string audioName, CancellationToken token = default);

        /// <summary>
        /// オーディオIDを指定して再生する
        /// マスターデータのオーディオテーブルから検索して再生
        /// </summary>
        /// <param name="audioId">オーディオID</param>
        /// <param name="token">キャンセレーショントークン</param>
        UniTask PlayAsync(int audioId, CancellationToken token = default);

        /// <summary>
        /// 複数のオーディオIDを指定して同時再生する
        /// </summary>
        /// <param name="audioIds">オーディオIDの配列</param>
        /// <param name="token">キャンセレーショントークン</param>
        UniTask PlayAsync(int[] audioIds, CancellationToken token = default);

        /// <summary>
        /// 指定したタグを持つオーディオからランダムに1つ再生する
        /// </summary>
        /// <param name="audioPlayTag">再生タグ</param>
        /// <param name="token">キャンセレーショントークン</param>
        UniTask PlayRandomOneAsync(AudioPlayTag audioPlayTag, CancellationToken token = default);

        /// <summary>
        /// 指定したカテゴリとタグを持つオーディオからランダムに1つ再生する
        /// </summary>
        /// <param name="audioCategory">オーディオカテゴリ</param>
        /// <param name="audioPlayTag">再生タグ</param>
        /// <param name="token">キャンセレーショントークン</param>
        UniTask PlayRandomOneAsync(AudioCategory audioCategory, AudioPlayTag audioPlayTag, CancellationToken token = default);

        /// <summary>
        /// 各カテゴリのボリュームを設定する
        /// </summary>
        /// <param name="bgm">BGMボリューム (0.0-1.0)</param>
        /// <param name="voice">ボイスボリューム (0.0-1.0)</param>
        /// <param name="sfx">効果音ボリューム (0.0-1.0)</param>
        void SetVolume(float bgm, float voice, float sfx);
    }
}