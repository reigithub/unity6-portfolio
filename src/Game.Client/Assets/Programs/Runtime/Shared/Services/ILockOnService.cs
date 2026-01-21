using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Shared.Services
{
    /// <summary>
    /// ロックオンサービスインターフェース
    /// 対象をロックオンして武器がその対象を優先的に狙う機能
    /// </summary>
    public interface ILockOnService
    {
        void Initialize(Camera camera, int layer);

        /// <summary>ロックオン中かどうか（対象が有効かつアクティブ）</summary>
        bool HasTarget();

        /// <summary>ターゲットを取得</summary>
        bool TryGetTarget(out Transform target, bool autoTarget = true);

        /// <summary>スクリーン座標からロックオン対象を設定（レイキャスト）</summary>
        void SetTarget(Vector2 point);

        /// <summary>ロックオンを解除</summary>
        void ClearTarget();

        /// <summary>
        /// 自動ターゲット選択のパラメータを設定
        /// </summary>
        /// <param name="owner">検索の中心となるTransform（プレイヤー）</param>
        void SetAutoTarget(Transform owner);

        /// <summary>
        /// 自動ターゲット選択を更新（毎フレーム呼び出し）
        /// ターゲットが無効（死亡/非アクティブ）の場合、カメラ方向優先で次のターゲットを自動選択
        /// </summary>
        void UpdateAutoTarget();

        /// <summary>
        /// プレハブとUIを事前ロード（初回ロックオン時の遅延を防ぐ）
        /// </summary>
        UniTask PreloadAsync();
    }
}