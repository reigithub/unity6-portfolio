using Game.Core.Services;
using Game.Horror.Signals;
using Game.Shared.Constants;
using Game.Shared.Scriptable.Database.Tables;
using R3;
using UnityEngine;

namespace Game.Horror.Enemy
{
    /// <summary>
    /// 警戒レベル（<see cref="HorrorEnemyPerception.Awareness"/> を閾値で量子化した値）
    /// </summary>
    public enum AwarenessLevel
    {
        /// <summary>平常：脅威を認識していない</summary>
        Unaware,

        /// <summary>警戒：何かがおかしいと感じている</summary>
        Suspicious,

        /// <summary>発見：プレイヤーを確定認識している</summary>
        Alert,
    }

    /// <summary>
    /// ホラーゲームのゾンビ型敵 AI 知覚センサー。
    /// 視覚（距離 → 視野角 → 遮蔽 Raycast の多段評価）と
    /// 聴覚（<see cref="HorrorSignals.Noise.Occurred"/> の MessagePipe 購読）を統合し、
    /// 警戒度ゲージ <see cref="Awareness"/>（0〜1）を駆動する。
    /// コントローラーから <see cref="Initialize"/> で注入を受けた後、
    /// Update で自律的に知覚を更新する。
    /// </summary>
    public class HorrorEnemyPerception : MonoBehaviour
    {
        [Tooltip("視覚スキャンの間引き間隔（秒）。毎フレームではなく間引く")]
        [SerializeField] private float _scanInterval = 0.1f;

        [Tooltip("遮蔽判定の対象レイヤー（壁・床・構造物）。Interactable レイヤーは実行時に常時合成される。Initialize 時に 0 なら Structure|Ground を既定設定する")]
        [SerializeField] private LayerMask _occluderMask;

        // 追跡対象と調整値（Initialize で注入）
        private Transform _target;
        private HorrorEnemyMaster _master;

        // 視野半角の余弦（Initialize で事前計算し Update の Mathf.Cos を回避）
        private float _cosHalfAngle;

        // 視覚スキャンのタイマー
        private float _nextScanTime;

        // MessagePipe 購読の一括解放コンテナ（OnDisable で Clear、OnDestroy で Dispose）
        private readonly CompositeDisposable _subscriptions = new();

        // 警戒度ゲージ（0..1）
        private float _awareness;

        // 公開状態
        private bool _hasConfirmedSight;
        private float _sightDistance;

        // 統合知覚位置
        private Vector3 _perceivedPlayerPosition;   // プレイヤー知覚位置（視認・足音・銃声）
        private bool _hasPerceivedPlayerPosition;
        private Vector3 _noticedPosition;           // 注意対象位置（視認・全種の音）
        private bool _hasNoticedPosition;

#if UNITY_EDITOR
        [Header("Debug Gizmos")]
        [SerializeField] private bool _drawGizmos;
        [SerializeField] private bool _drawSightRange = true;
        [SerializeField] private bool _drawSightCone = true;
        [SerializeField] private bool _drawSightRay = true;
        [SerializeField] private bool _drawHearingRadius = true;

        private static readonly Color GizmoColorSightRange = new Color(1f, 1f, 0f, 0.5f);
        private static readonly Color GizmoColorSightCone = new Color(1f, 0.8f, 0f, 0.4f);
        private static readonly Color GizmoColorSightRayClear = Color.green;
        private static readonly Color GizmoColorSightRayBlocked = Color.red;
        private static readonly Color GizmoColorSightRayOutOfCone = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color GizmoColorHearingRadius = new Color(0f, 0.8f, 1f, 0.3f);

        // Gizmo スナップショット（最後のスキャン結果を OnDrawGizmos から参照する）
        private Vector3 _gizmoEyePos;
        private Vector3 _gizmoTargetPos;
        private bool _gizmoRayActive;
        private bool _gizmoRayBlocked;
        private bool _gizmoTargetInRange;
#endif

        #region 公開 API

        /// <summary>現在の警戒レベル</summary>
        public AwarenessLevel Level
        {
            get
            {
                if (_master == null) return AwarenessLevel.Unaware;
                if (_awareness >= _master.AlertThreshold) return AwarenessLevel.Alert;
                if (_awareness >= _master.SuspiciousThreshold) return AwarenessLevel.Suspicious;
                return AwarenessLevel.Unaware;
            }
        }

        /// <summary>直近スキャンで視線が通ったか</summary>
        public bool HasConfirmedSight => _hasConfirmedSight;

        /// <summary>
        /// 最後にプレイヤー本体を知覚した位置（視認、または足音/銃声）を取得する。
        /// 一度も知覚していなければ false（デコイ由来の音では更新されない）。
        /// 鮮度の概念はなく、一度でも知覚すれば以後の Chase で常にデコイ注意より優先される（受容済みの制限。
        /// 逆転が体感される場合は鮮度失効の導入を検討する）。
        /// </summary>
        public bool TryGetLastPerceivedPlayerPosition(out Vector3 position)
        {
            position = _perceivedPlayerPosition;
            return _hasPerceivedPlayerPosition;
        }

        /// <summary>
        /// 最後に注意を引かれた位置（視認・全種の音の最新）を取得する。刺激未受信なら false。
        /// </summary>
        public bool TryGetLastNoticedPosition(out Vector3 position)
        {
            position = _noticedPosition;
            return _hasNoticedPosition;
        }

        /// <summary>警戒度ゲージ（0〜1、デバッグ/UI 用）</summary>
        public float Awareness => _awareness;

        /// <summary>脅威を確定認識しているか（視認中、または警戒レベルが Alert）</summary>
        public bool IsThreatConfirmed => HasConfirmedSight || Level == AwarenessLevel.Alert;

        /// <summary>不審以上の警戒状態か（警戒レベルが Suspicious 以上）</summary>
        public bool IsSuspiciousOrHigher => Level >= AwarenessLevel.Suspicious;

        #endregion

        #region 初期化

        /// <summary>
        /// 知覚センサーを初期化する。コントローラーから Awake または生成直後に呼ぶ。
        /// </summary>
        /// <param name="target">追跡対象（プレイヤー）の Transform</param>
        /// <param name="master">調整値マスターデータ</param>
        public void Initialize(Transform target, HorrorEnemyMaster master)
        {
            _target = target;
            _master = master;

            // 視野半角の余弦を事前計算（スキャン毎の Mathf.Cos を回避）
            _cosHalfAngle = Mathf.Cos(master.SightHalfAngle * Mathf.Deg2Rad);

            // Inspector 未設定時は Structure|Ground を既定として使用
            if (_occluderMask.value == 0)
                _occluderMask = LayerMaskConstants.Structure | LayerMaskConstants.Ground;

            // 聴覚（Noise.Occurred）とプレイヤー死亡（Player.Died）を購読する
            var messagePipeService = GameServiceManager.Resolve<IMessagePipeService>();
            messagePipeService.Subscribe<HorrorSignals.Noise.Occurred>(OnNoise).AddTo(_subscriptions);
            messagePipeService.Subscribe<HorrorSignals.Player.Died>(OnPlayerDied).AddTo(_subscriptions);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"[HorrorEnemyPerception] Initialize: target={target.name}, sightRange={master.SightRange}, hearingRadius={master.HearingRadius}");
#endif
        }

        #endregion

        #region MonoBehaviour

        private void Update()
        {
            if (_master == null || _target == null) return;

            // ---- 視覚スキャン（間引き） ----
            if (Time.time >= _nextScanTime)
            {
                _nextScanTime = Time.time + _scanInterval;
                ScanVision();
            }

            // ---- 警戒度ゲージ更新（毎フレーム） ----
            float distance01 = _hasConfirmedSight && _master.SightRange > 0f
                ? Mathf.Clamp01(_sightDistance / _master.SightRange)
                : 1f;

            _awareness = UpdateAwareness(
                _awareness,
                _hasConfirmedSight,
                distance01,
                _master.AwarenessFillRate,
                _master.AwarenessDecayRate,
                Time.deltaTime);
        }

        private void OnDisable()
        {
            _subscriptions.Clear();
            _hasConfirmedSight = false;
            _nextScanTime = 0f;
        }

        private void OnDestroy()
        {
            _subscriptions.Dispose();
        }

        #endregion

        #region 視覚スキャン

        /// <summary>
        /// 視覚パイプラインを1スキャン実行する。
        /// 安価な判定（距離 → 視野角 → Raycast）の順に評価し、
        /// いずれかで失格になれば即 return することで物理コストを最小化する。
        /// </summary>
        private void ScanVision()
        {
            if (_target == null || _master == null)
            {
                _hasConfirmedSight = false;
                return;
            }

            Vector3 toTarget = _target.position - transform.position;

            // Step 1: 距離（sqrMagnitude で sqrt を回避）
            float sightRangeSq = _master.SightRange * _master.SightRange;
            if (toTarget.sqrMagnitude > sightRangeSq)
            {
                _hasConfirmedSight = false;
#if UNITY_EDITOR
                _gizmoTargetInRange = false;
                _gizmoRayActive = false;
#endif
                return;
            }

            float distance = toTarget.magnitude;
            Vector3 toTargetNormalized = distance > 0f ? toTarget / distance : transform.forward;

            // Step 2: 視野角（Dot と事前計算済み cos 閾値で判定。Vector3.Angle より軽量）
            if (!IsInSightCone(transform.forward, toTargetNormalized, _cosHalfAngle))
            {
                _hasConfirmedSight = false;
#if UNITY_EDITOR
                _gizmoTargetInRange = true;
                _gizmoRayActive = false;
#endif
                return;
            }

            // Step 3: 視線遮蔽（目の高さから Raycast。構造物/地形に加え、Interactable 家具も視線を遮る）
            Vector3 eyePos = transform.position + Vector3.up * _master.EyeHeight;
            Vector3 eyeToTarget = _target.position - eyePos;
            float eyeDist = eyeToTarget.magnitude;

            bool occluded = eyeDist > 0f && Physics.Raycast(
                eyePos,
                eyeToTarget / eyeDist,
                eyeDist,
                _occluderMask | LayerMaskConstants.Interactable,
                QueryTriggerInteraction.Ignore);

#if UNITY_EDITOR
            _gizmoEyePos = eyePos;
            _gizmoTargetPos = _target.position;
            _gizmoTargetInRange = true;
            _gizmoRayActive = true;
            _gizmoRayBlocked = occluded;
#endif

            if (occluded)
            {
                _hasConfirmedSight = false;
                return;
            }

            // 視認成立
            _hasConfirmedSight = true;
            _sightDistance = distance;
            RecordPlayerPerceived(_target.position);
        }

        #endregion

        #region 聴覚

        /// <summary>
        /// HorrorSignals.Noise.Occurred を受信したときの処理。
        /// 到達半径内なら知覚位置（プレイヤー由来の音はプレイヤー知覚位置も、それ以外は注意対象位置のみ）を更新し、
        /// 警戒度を加算する。
        /// </summary>
        private void OnNoise(HorrorSignals.Noise.Occurred evt)
        {
            // _target == null は知覚断絶（プレイヤー死亡後）。Update が停止し警戒度が減衰しないため、
            // ここで加算すると凍結した警戒度による Wander↔Investigate ループが起きる
            if (_master == null || _target == null) return;

            float reachRadius = HearingRadiusFor(_master.HearingRadius, evt.Loudness, _master.HearingSensitivity);
            float dist = Vector3.Distance(transform.position, evt.Position);

            if (dist > reachRadius) return;

            if (IsPlayerLocatedNoise(evt.Type))
                RecordPlayerPerceived(evt.Position);
            else
                RecordNoticed(evt.Position);

            // 近いほど・音が大きいほど警戒度を多く加算
            float distRatio = reachRadius > 0f ? Mathf.Clamp01(1f - dist / reachRadius) : 1f;
            float addition = evt.Loudness * distRatio * 0.3f;
            _awareness = Mathf.Clamp01(_awareness + addition);
        }

        #endregion

        #region 知覚位置の記録

        /// <summary>
        /// プレイヤー知覚位置を記録する。プレイヤーの知覚は注意対象でもあるため、注意対象位置の更新を必ず伴う
        /// （包含関係の単一実装点）。
        /// </summary>
        private void RecordPlayerPerceived(Vector3 position)
        {
            _perceivedPlayerPosition = position;
            _hasPerceivedPlayerPosition = true;
            RecordNoticed(position);
        }

        /// <summary>
        /// 注意対象位置のみを記録する（デコイ可能な着弾音・敵自身の悲鳴など）。
        /// 同フレームに複数の音が届いた場合は後着優先（HorrorPlayerController.Fire はこの規則に依存して
        /// 着弾音→発砲音の順で発行している）。
        /// </summary>
        private void RecordNoticed(Vector3 position)
        {
            _noticedPosition = position;
            _hasNoticedPosition = true;
        }

        #endregion

        #region プレイヤー死亡

        /// <summary>
        /// HorrorSignals.Player.Died を受信したときの処理。知覚を断絶する。
        /// _target=null で以後の視覚スキャン・警戒度更新（Update の early return）が停止するため、
        /// 減衰に頼れない _awareness と、再スキャンで消えない _hasConfirmedSight は明示的にクリアする
        /// （放置すると凍結値で IsThreatConfirmed が恒真化し Chase から抜けられない）。
        /// 位置履歴（_perceivedPlayerPosition/_noticedPosition と各 _has フラグ）は意図的に保持する：
        /// クリアすると Investigate がその場見回しに退化し、最終知覚位置へ捜索移動する自然な余韻が失われる。
        /// FSM は既存遷移（LostTarget→Investigate→GiveUp→Wander）で自然に平常へ戻る。
        /// </summary>
        private void OnPlayerDied(HorrorSignals.Player.Died evt)
        {
            _target = null;
            _awareness = 0f;
            _hasConfirmedSight = false;
        }

        #endregion

        #region 純粋計算ヘルパー

        /// <summary>
        /// 方向ベクトルが視野錐の内側にあるかを判定する。
        /// Vector3.Angle を使わず Dot と cos 閾値で判定することで sqrt を回避する。
        /// </summary>
        /// <param name="forward">センサーの前方ベクトル（正規化済み）</param>
        /// <param name="toTargetNormalized">センサーからターゲットへの方向（正規化済み）</param>
        /// <param name="cosHalfAngle">視野半角の余弦（<c>Mathf.Cos(halfAngle * Deg2Rad)</c> の事前計算値）</param>
        /// <returns>視野錐内なら true</returns>
        internal static bool IsInSightCone(Vector3 forward, Vector3 toTargetNormalized, float cosHalfAngle)
        {
            return Vector3.Dot(forward, toTargetNormalized) >= cosHalfAngle;
        }

        /// <summary>
        /// 聴覚の到達半径を計算する。
        /// </summary>
        /// <param name="baseRadius">基準半径（マスターデータの HearingRadius）</param>
        /// <param name="loudness">音の大きさ（0=無音、1=通常、それ以上=特大）</param>
        /// <param name="sensitivity">聴覚感度倍率（マスターデータの HearingSensitivity）</param>
        /// <returns>実効到達半径（メートル）</returns>
        internal static float HearingRadiusFor(float baseRadius, float loudness, float sensitivity)
        {
            return baseRadius * loudness * sensitivity;
        }

        /// <summary>
        /// 音種がプレイヤーの実位置に相関するか（発生位置=プレイヤー所在とみなせるか）を判定する。
        /// Footstep/Gunshot はプレイヤー自身から発生する。Object（着弾等）は着弾点=デコイ可能、Scream は敵自身の発声。
        /// </summary>
        internal static bool IsPlayerLocatedNoise(NoiseType type)
            => type is NoiseType.Footstep or NoiseType.Gunshot;

        /// <summary>
        /// 警戒度ゲージを 1 ステップ更新する（純粋関数）。
        /// 視認継続中は距離が近いほど速く充填し、刺激がなければ減衰する。
        /// </summary>
        /// <param name="current">現在の警戒度（0〜1）</param>
        /// <param name="hasSight">視認成立フラグ</param>
        /// <param name="distance01">視程に対する距離の割合（0=最近接、1=視程端）</param>
        /// <param name="fillRate">充填レート（/秒）</param>
        /// <param name="decayRate">減衰レート（/秒）</param>
        /// <param name="dt">経過時間（deltaTime）</param>
        /// <returns>更新後の警戒度（0〜1 にクランプ済み）</returns>
        internal static float UpdateAwareness(
            float current,
            bool hasSight,
            float distance01,
            float fillRate,
            float decayRate,
            float dt)
        {
            if (hasSight)
            {
                // 近いほど充填が速い（distance01=0 なら 2x レート、=1 なら 1x レート）
                float proximityMultiplier = 1f + (1f - distance01);
                current += fillRate * proximityMultiplier * dt;
            }
            else
            {
                current -= decayRate * dt;
            }

            return Mathf.Clamp01(current);
        }

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_drawGizmos || _master == null) return;

            float sightRange = _master.SightRange;
            float eyeH = _master.EyeHeight;
            Vector3 eyePos = transform.position + Vector3.up * eyeH;

            // 視程スフィア
            if (_drawSightRange)
            {
                Gizmos.color = GizmoColorSightRange;
                Gizmos.DrawWireSphere(transform.position, sightRange);
            }

            // 視野錐（正面 + 水平/垂直の縁 4 本）
            if (_drawSightCone)
            {
                Gizmos.color = GizmoColorSightCone;
                float halfRad = _master.SightHalfAngle * Mathf.Deg2Rad;
                float cosA = Mathf.Cos(halfRad);
                float sinA = Mathf.Sin(halfRad);
                Vector3 fwd = transform.forward;
                Vector3 right = transform.right;
                Vector3 up = transform.up;

                Gizmos.DrawRay(eyePos, fwd * sightRange);
                Gizmos.DrawRay(eyePos, (fwd * cosA + right * sinA) * sightRange);
                Gizmos.DrawRay(eyePos, (fwd * cosA - right * sinA) * sightRange);
                Gizmos.DrawRay(eyePos, (fwd * cosA + up * sinA) * sightRange);
                Gizmos.DrawRay(eyePos, (fwd * cosA - up * sinA) * sightRange);
            }

            // 聴覚半径（基準半径を描画）
            if (_drawHearingRadius)
            {
                Gizmos.color = GizmoColorHearingRadius;
                Gizmos.DrawWireSphere(transform.position, _master.HearingRadius);
            }

            // 視線レイ（Play 中かつスナップショット有効時のみ）
            if (!Application.isPlaying || !_drawSightRay) return;

            if (_gizmoRayActive)
            {
                Gizmos.color = _gizmoRayBlocked ? GizmoColorSightRayBlocked : GizmoColorSightRayClear;
                Gizmos.DrawLine(_gizmoEyePos, _gizmoTargetPos);
            }
            else if (_gizmoTargetInRange && _target != null)
            {
                // 距離内だが視野角外または遮蔽 → グレーで描画
                Gizmos.color = GizmoColorSightRayOutOfCone;
                Gizmos.DrawLine(eyePos, _target.position);
            }
        }
#endif
    }
}
