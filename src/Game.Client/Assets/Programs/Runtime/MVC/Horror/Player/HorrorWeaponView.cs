using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Shared.Extensions;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Player
{
    /// <summary>
    /// Horror 一人称視点の武器モデル表示。カメラ子ソケット WeaponRoot にアタッチし、
    /// 装備中武器のモデル生成・表示切替を担う。演出クロックは持たず、<see cref="HorrorPlayerController"/> の
    /// EquippingState / ReloadingState が持つ経過時間を <see cref="BeginSwitch"/> / <see cref="TickSwitch"/> /
    /// <see cref="TickReload"/> 経由で受け取って駆動する
    /// （単一クロック設計。View 独自のタイマーや UniTask 演出ループは持たない）。
    /// 発砲キックのみ例外で、<see cref="NotifyFired"/> が立てたインパルスを <see cref="UpdatePose"/> 内で
    /// 自己減衰させる（発砲インパルスは AttackingState の滞在時間より長く残り得るため。
    /// <see cref="HorrorReticleView"/> の NotifyFired と同イディオム）。
    /// WeaponRoot ローカル位置・回転への書き込みは <see cref="UpdatePose"/> に一元化されており、
    /// 切替演出の下げ量・エイム構えオフセット・リロード傾き・発砲キックの合成もそこでのみ行われる。
    /// </summary>
    public class HorrorWeaponView : MonoBehaviour
    {
        [Tooltip("装備切替演出で武器を下げる相対オフセット（WeaponRoot ローカル座標）")]
        [SerializeField] private Vector3 _downOffset = new(0f, -0.4f, 0f);

        [Tooltip("エイム時に武器を構える相対オフセット（WeaponRoot ローカル座標）")]
        [SerializeField] private Vector3 _aimOffset = new(-0.25f, 0.1f, 0f);

        [Tooltip("リロード演出で武器を傾けるロール角（度）。正で右傾き")]
        [SerializeField] private float _reloadTiltAngle = 35f;

        [Tooltip("リロード演出の傾け・戻しの遷移秒数")]
        [SerializeField] private float _reloadTiltSeconds = 0.4f;

        [Tooltip("発砲キックで武器を後退させる相対オフセット（WeaponRoot ローカル座標）")]
        [SerializeField] private Vector3 _recoilOffset = new(0f, 0.02f, -0.04f);

        [Tooltip("発砲キックの跳ね上げ角（度）。正で銃口が上を向く")]
        [SerializeField] private float _recoilKickAngle = 3f;

        [Tooltip("発砲キックが収まるまでの秒数")]
        [SerializeField] private float _recoilRecoverSeconds = 0.15f;

        private IAddressableAssetService _assetService;

        // 武器マスター Id をキーにした Addressables プレハブハンドルキャッシュ（OnDestroy で ReleaseAsset）
        private readonly Dictionary<int, GameObject> _prefabs = new();

        // 武器マスター Id をキーにした生成済みモデルインスタンス（WeaponRoot 子。プレイヤー破棄カスケードで自動破棄）
        private readonly Dictionary<int, GameObject> _models = new();

        // 武器マスター Id をキーにしたマズルフラッシュプレハブハンドルキャッシュ（OnDestroy で ReleaseAsset）
        private readonly Dictionary<int, GameObject> _muzzleFlashPrefabs = new();

        // 武器マスター Id をキーにした生成済みマズルフラッシュインスタンス（モデルの Muzzle ソケット子。プレイヤー破棄カスケードで自動破棄）
        private readonly Dictionary<int, ParticleSystem> _muzzleFlashes = new();

        // ロード中の Id（ShowImmediate / PreloadAsync / BeginSwitch 起点のロードが同一 Id で重複しないようにする）
        private readonly HashSet<int> _loading = new();

        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private float _lowerAmount; // 切替演出の下げ量（0-1）。TickSwitch が更新し UpdatePose が消費する
        private float _reloadTiltWeight; // リロード演出の傾き量（0-1）。TickReload が更新し UpdatePose が消費する
        private float _recoilWeight; // 発砲キックのインパルス（0-1）。NotifyFired が 1 にし UpdatePose が自己減衰させながら消費する
        private int _currentId = -1;
        private bool _disposed;

        // 切替演出コンテキスト（BeginSwitch で確定し、TickSwitch とロード完了ハンドラが参照する）
        private int _pendingId = -1;
        private bool _skipPutDown;
        private bool _swapped;

        private void Awake()
        {
            _baseLocalPosition = transform.localPosition;
            _baseLocalRotation = transform.localRotation;
        }

        private void OnDestroy()
        {
            _disposed = true;

            // モデルインスタンス自体はプレイヤー破棄カスケードで自動破棄されるため、ここではプレハブハンドルのみ解放する
            foreach (var prefab in _prefabs.Values)
            {
                _assetService?.Release(prefab);
            }
            _prefabs.Clear();

            foreach (var flashPrefab in _muzzleFlashPrefabs.Values)
            {
                _assetService?.Release(flashPrefab);
            }
            _muzzleFlashPrefabs.Clear();
        }

        /// <summary>
        /// Addressables アセットサービスを注入する。<see cref="HorrorPlayerController.Initialize"/> の装備復元直後に呼ぶこと。
        /// </summary>
        public void Initialize()
        {
            _assetService = GameServiceManager.Resolve<IAddressableAssetService>();
        }

        /// <summary>
        /// 演出なしで即座に武器モデルを表示する。復元（ステージ開始時の装備反映）用途。未生成ならロード後に表示する。
        /// </summary>
        public void ShowImmediate(HorrorWeaponMaster master)
        {
            if (master == null) return;

            _currentId = master.Id;

            if (_models.TryGetValue(master.Id, out var model))
            {
                model.SetActive(true);
                return;
            }

            if (_loading.Contains(master.Id)) return;

            LoadModelAsync(master).Forget();
        }

        /// <summary>
        /// ショートカット登録済み武器（＋装備中）のモデルを事前ロードし、非表示状態で生成しておく。
        /// </summary>
        public async UniTask PreloadAsync(List<HorrorWeaponMaster> masters)
        {
            foreach (var master in masters)
            {
                if (master == null || _models.ContainsKey(master.Id) || _loading.Contains(master.Id)) continue;

                await LoadModelAsync(master);
            }
        }

        /// <summary>
        /// 装備切替演出を開始する。EquippingState.Enter から装備反映直後に呼ばれる。
        /// 現行未装備（初回装備）なら下げ演出を省略するフラグを立てる。
        /// </summary>
        public void BeginSwitch(HorrorWeaponMaster next)
        {
            if (next == null) return;

            _pendingId = next.Id;
            _skipPutDown = _currentId < 0;
            _swapped = false;

            if (!_models.ContainsKey(next.Id) && !_loading.Contains(next.Id))
            {
                LoadModelAsync(next).Forget();
            }
        }

        /// <summary>
        /// 装備切替演出を毎フレーム進行させる。EquippingState.Update から呼ばれる。
        /// EquippingState の経過時間を唯一のクロックとして受け取り、下げ量の更新とモデル入替を行う。
        /// </summary>
        public void TickSwitch(float elapsed, float duration)
        {
            _lowerAmount = CalculateLowerAmount(elapsed, duration, _skipPutDown);

            if (!_swapped && IsPastSwapPoint(elapsed, duration, _skipPutDown))
            {
                _swapped = true;
                SwapModel(_pendingId);
            }
        }

        /// <summary>
        /// リロード演出を毎フレーム進行させる。ReloadingState.Update から呼ばれる。
        /// ReloadingState の経過時間を唯一のクロックとして受け取り、傾き量を更新する。
        /// </summary>
        public void TickReload(float elapsed, float duration)
        {
            _reloadTiltWeight = CalculateReloadTiltWeight(elapsed, duration, _reloadTiltSeconds);
        }

        /// <summary>
        /// リロード演出の傾きを即時解除する。ReloadingState.Exit から呼ばれる（中断・完了とも確実にリセット）。
        /// </summary>
        public void ResetReload()
        {
            _reloadTiltWeight = 0f;
        }

        /// <summary>
        /// 武器の構え位置・傾きを毎フレーム反映する。<see cref="HorrorPlayerController"/> の各ステート Update
        /// （装備切替中は TickSwitch の後、リロード中は TickReload の後）から UpdateAimPose 経由で呼ばれ、
        /// 切替演出の下げ量・エイムブレンド・発砲キックを合成した唯一の位置・回転書き込み点となる。
        /// 発砲キックのインパルスはここで自己減衰させる（<see cref="NotifyFired"/> 側にはタイマーを持たせない）。
        /// </summary>
        public void UpdatePose(float aimBlend)
        {
            _recoilWeight = Mathf.MoveTowards(_recoilWeight, 0f, Time.deltaTime / Mathf.Max(_recoilRecoverSeconds, 0.0001f));
            transform.localPosition = CalculateLocalPosition(_baseLocalPosition, _downOffset, _lowerAmount, _aimOffset, aimBlend, _recoilOffset, _recoilWeight);
            transform.localRotation = CalculateLocalRotation(_baseLocalRotation, _reloadTiltAngle, _reloadTiltWeight, _recoilKickAngle, _recoilWeight);
        }

        /// <summary>
        /// 発砲キックを開始する。<see cref="HorrorPlayerController.Fire"/> から呼ばれ、
        /// 武器モデルが一瞬後退・跳ね上がりながら素早く戻り、マズルフラッシュを再生する。
        /// </summary>
        public void NotifyFired()
        {
            _recoilWeight = 1f;

            if (_muzzleFlashes.TryGetValue(_currentId, out var flash) && flash != null)
            {
                flash.Clear(true);
                flash.Play(true);
            }
        }

        // 入替点で旧モデルを非表示にし、新モデルが生成済みなら表示する（未生成ならロード完了側で表示される）
        private void SwapModel(int nextId)
        {
            if (_currentId >= 0 && _models.TryGetValue(_currentId, out var currentModel))
            {
                currentModel.SetActive(false);
            }

            _currentId = nextId;

            if (_models.TryGetValue(nextId, out var nextModel))
            {
                nextModel.SetActive(true);
            }
        }

        // プレハブをロード（未取得ならキャッシュ）してモデルを生成する（既生成ならそれを返す）。
        // 生成直後の表示可否は _currentId と一致するかで判定する（ShowImmediate/BeginSwitch 側が先に _currentId を確定させているため）。
        private async UniTask<GameObject> LoadModelAsync(HorrorWeaponMaster master)
        {
            _loading.Add(master.Id);
            try
            {
                if (!_prefabs.TryGetValue(master.Id, out var prefab))
                {
                    prefab = await _assetService.LoadAssetAsync<GameObject>(master.ModelAssetName)
                        .AttachExternalCancellation(destroyCancellationToken);

                    if (_disposed)
                    {
                        _assetService.Release(prefab);
                        return null;
                    }

                    _prefabs[master.Id] = prefab;
                }

                if (_models.TryGetValue(master.Id, out var existing)) return existing;

                var model = Object.Instantiate(prefab, transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.SetLayerRecursively(gameObject.layer);
                model.SetActive(_currentId == master.Id);
                _models[master.Id] = model;

                if (!string.IsNullOrEmpty(master.MuzzleFlashAssetName))
                {
                    await LoadMuzzleFlashAsync(master, model);
                }

                return model;
            }
            finally
            {
                _loading.Remove(master.Id);
            }
        }

        // マズルフラッシュのプレハブをロード（未取得ならキャッシュ）し、モデルの Muzzle ソケット子として生成する。
        // 既に生成済み（_muzzleFlashes 登録済み）なら何もしない。
        private async UniTask LoadMuzzleFlashAsync(HorrorWeaponMaster master, GameObject model)
        {
            if (!_muzzleFlashPrefabs.TryGetValue(master.Id, out var flashPrefab))
            {
                flashPrefab = await _assetService.LoadAssetAsync<GameObject>(master.MuzzleFlashAssetName)
                    .AttachExternalCancellation(destroyCancellationToken);

                if (_disposed)
                {
                    _assetService.Release(flashPrefab);
                    return;
                }

                _muzzleFlashPrefabs[master.Id] = flashPrefab;
            }

            if (_muzzleFlashes.ContainsKey(master.Id)) return;

            var socket = model.transform.Find("Muzzle");
            if (socket == null)
            {
                Debug.LogWarning($"HorrorWeaponView: Muzzle ソケットが見つかりません（武器 Id={master.Id}）。モデルルートへフォールバックします。");
                socket = model.transform;
            }

            var flashInstance = Object.Instantiate(flashPrefab, socket);
            flashInstance.transform.localPosition = Vector3.zero;
            flashInstance.transform.localRotation = Quaternion.identity;
            flashInstance.SetLayerRecursively(gameObject.layer);

            // ParticlePack のプレハブはデモ表示用に looping=true / playOnAwake=true で設定されているため、
            // NotifyFired からの明示 Play/Stop 制御に統一するべく全パーティクルシステムで無効化する
            foreach (var particle in flashInstance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.loop = false;
                main.playOnAwake = false;
            }

            if (!flashInstance.TryGetComponent<ParticleSystem>(out var rootParticle))
            {
                Debug.LogWarning($"HorrorWeaponView: マズルフラッシュのルートに ParticleSystem がありません（武器 Id={master.Id}）。");
                return;
            }

            rootParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _muzzleFlashes[master.Id] = rootParticle;
        }

        /// <summary>
        /// 装備切替演出の下げ量（0-1）を算出する。通常は前半で 0→1（下げ）、後半で 1→0（上げ）の三角波。
        /// 初回装備（<paramref name="skipPutDown"/>）は下げを省略し 1→0（上げのみ）。
        /// <paramref name="duration"/> が 0 以下ならゼロ除算を避けて 0 を返す。
        /// </summary>
        public static float CalculateLowerAmount(float elapsed, float duration, bool skipPutDown)
        {
            if (duration <= 0f) return 0f;

            var t = Mathf.Clamp01(elapsed / duration);

            if (skipPutDown) return Mathf.Clamp01(1f - t);

            var amount = t < 0.5f ? t * 2f : (1f - t) * 2f;
            return Mathf.Clamp01(amount);
        }

        /// <summary>
        /// 基準位置に切替演出の下げオフセット・エイム構えオフセット・発砲キックオフセットを合成した WeaponRoot ローカル位置を算出する。
        /// </summary>
        public static Vector3 CalculateLocalPosition(Vector3 basePosition, Vector3 downOffset, float lowerAmount, Vector3 aimOffset, float aimBlend, Vector3 recoilOffset, float recoilWeight)
            => basePosition + downOffset * lowerAmount + aimOffset * aimBlend + recoilOffset * recoilWeight;

        /// <summary>
        /// モデル入替点（中間点）を通過したかを判定する。初回装備（<paramref name="skipPutDown"/>）は
        /// 下げ演出が無いため常に true（開始直後に入替）。
        /// </summary>
        public static bool IsPastSwapPoint(float elapsed, float duration, bool skipPutDown)
        {
            return skipPutDown || elapsed >= duration * 0.5f;
        }

        /// <summary>
        /// リロード演出の傾き量（0-1）を算出する。開始から transitionSeconds で 0→1（傾け）、
        /// 終端の transitionSeconds で 1→0（戻し）、間は 1 を保持する台形カーブ。
        /// duration が 0 以下なら 0。transitionSeconds が短い duration では自然に三角波化する。
        /// </summary>
        public static float CalculateReloadTiltWeight(float elapsed, float duration, float transitionSeconds)
        {
            if (duration <= 0f) return 0f;

            var t = Mathf.Max(transitionSeconds, 0.0001f);
            return Mathf.Clamp01(Mathf.Min(elapsed / t, (duration - elapsed) / t));
        }

        /// <summary>
        /// 基準回転にリロード傾き（ロール角 × 傾き量）と発砲キックの跳ね上げ（ピッチ角 × キック量）を合成した
        /// WeaponRoot ローカル回転を算出する。
        /// </summary>
        public static Quaternion CalculateLocalRotation(Quaternion baseRotation, float tiltAngle, float tiltWeight, float recoilKickAngle, float recoilWeight)
            => baseRotation * Quaternion.Euler(-recoilKickAngle * recoilWeight, 0f, tiltAngle * tiltWeight);
    }
}
