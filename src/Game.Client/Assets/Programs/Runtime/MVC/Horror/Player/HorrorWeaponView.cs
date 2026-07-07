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
    /// EquippingState が持つ経過時間を <see cref="BeginSwitch"/> / <see cref="TickSwitch"/> 経由で受け取って駆動する
    /// （単一クロック設計。View 独自のタイマーや UniTask 演出ループは持たない）。
    /// WeaponRoot ローカル位置への書き込みは <see cref="UpdatePose"/> に一元化されており、
    /// 切替演出の下げ量とエイム構えオフセットの合成もそこでのみ行われる。
    /// </summary>
    public class HorrorWeaponView : MonoBehaviour
    {
        [Tooltip("装備切替演出で武器を下げる相対オフセット（WeaponRoot ローカル座標）")]
        [SerializeField] private Vector3 _downOffset = new(0f, -0.4f, 0f);

        [Tooltip("エイム時に武器を構える相対オフセット（WeaponRoot ローカル座標）")]
        [SerializeField] private Vector3 _aimOffset = new(-0.25f, 0.1f, 0f);

        private IAddressableAssetService _assetService;

        // 武器マスター Id をキーにした Addressables プレハブハンドルキャッシュ（OnDestroy で ReleaseAsset）
        private readonly Dictionary<int, GameObject> _prefabs = new();

        // 武器マスター Id をキーにした生成済みモデルインスタンス（WeaponRoot 子。プレイヤー破棄カスケードで自動破棄）
        private readonly Dictionary<int, GameObject> _models = new();

        // ロード中の Id（ShowImmediate / PreloadAsync / BeginSwitch 起点のロードが同一 Id で重複しないようにする）
        private readonly HashSet<int> _loading = new();

        private Vector3 _baseLocalPosition;
        private float _lowerAmount; // 切替演出の下げ量（0-1）。TickSwitch が更新し UpdatePose が消費する
        private int _currentId = -1;
        private bool _disposed;

        // 切替演出コンテキスト（BeginSwitch で確定し、TickSwitch とロード完了ハンドラが参照する）
        private int _pendingId = -1;
        private bool _skipPutDown;
        private bool _swapped;

        private void Awake()
        {
            _baseLocalPosition = transform.localPosition;
        }

        private void OnDestroy()
        {
            _disposed = true;

            // モデルインスタンス自体はプレイヤー破棄カスケードで自動破棄されるため、ここではプレハブハンドルのみ解放する
            foreach (var prefab in _prefabs.Values)
            {
                _assetService?.ReleaseAsset(prefab);
            }
            _prefabs.Clear();
        }

        /// <summary>
        /// Addressables アセットサービスを注入する。<see cref="HorrorPlayerController.Initialize"/> の装備復元直後に呼ぶこと。
        /// </summary>
        public void Initialize()
        {
            _assetService = GameServiceManager.Get<AddressableAssetService>();
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
        /// 武器の構え位置を毎フレーム反映する。<see cref="HorrorPlayerController"/> の各ステート Update
        /// （装備切替中は TickSwitch の後）から UpdateAimPose 経由で呼ばれ、
        /// 切替演出の下げ量とエイムブレンドを合成した唯一の位置書き込み点となる。
        /// </summary>
        public void UpdatePose(float aimBlend)
        {
            transform.localPosition = CalculateLocalPosition(_baseLocalPosition, _downOffset, _lowerAmount, _aimOffset, aimBlend);
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
                        _assetService.ReleaseAsset(prefab);
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

                return model;
            }
            finally
            {
                _loading.Remove(master.Id);
            }
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
        /// 基準位置に切替演出の下げオフセットとエイム構えオフセットを合成した WeaponRoot ローカル位置を算出する。
        /// </summary>
        public static Vector3 CalculateLocalPosition(Vector3 basePosition, Vector3 downOffset, float lowerAmount, Vector3 aimOffset, float aimBlend)
            => basePosition + downOffset * lowerAmount + aimOffset * aimBlend;

        /// <summary>
        /// モデル入替点（中間点）を通過したかを判定する。初回装備（<paramref name="skipPutDown"/>）は
        /// 下げ演出が無いため常に true（開始直後に入替）。
        /// </summary>
        public static bool IsPastSwapPoint(float elapsed, float duration, bool skipPutDown)
        {
            return skipPutDown || elapsed >= duration * 0.5f;
        }
    }
}
