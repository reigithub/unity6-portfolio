using System;
using System.Collections.Generic;
using Game.Core.Services;
using Game.Horror.Signals;
using UnityEngine;

namespace Game.Horror.Player
{
    /// <summary>
    /// OverlayCanvas/DamagePopups にアタッチ。MessagePipe で <see cref="HorrorSignals.Combat.DamageApplied"/> を購読し、
    /// 内部プールからポップアップを再生する。
    /// </summary>
    public class HorrorDamageSpawner : MonoBehaviour
    {
        [Tooltip("ダメージポップアップの prefab")]
        [SerializeField] private HorrorDamageView _popupPrefab;

        [Tooltip("ワールド→スクリーン変換に使うカメラ。HorrorPlayer.prefab 内の Camera を配線")]
        [SerializeField] private Camera _camera;

        private readonly Queue<HorrorDamageView> _pool = new();
        private Action<HorrorDamageView> _returnView;
        private IDisposable _subscription;

        private void Start()
        {
            // メソッドグループ変換によるヒット毎のデリゲート生成を避けるため一度だけ生成する
            _returnView = ReturnView;

            // ダメージ適用イベントを購読。戻り IDisposable を保持する（OnDestroy で破棄）
            var messagePipeService = GameServiceManager.Resolve<IMessagePipeService>();
            _subscription = messagePipeService.Subscribe<HorrorSignals.Combat.DamageApplied>(OnDamageApplied);
        }

        private void OnDamageApplied(HorrorSignals.Combat.DamageApplied e)
        {
            if (_popupPrefab == null || _camera == null) return;

            var popup = GetFromPool();
            popup.Play(_camera, e.Position, e.Damage, _returnView);
        }

        // プールから未使用のポップアップを取得する。破棄済み（null）はスキップし、空なら新規生成する。
        private HorrorDamageView GetFromPool()
        {
            while (_pool.Count > 0)
            {
                var pooled = _pool.Dequeue();
                if (pooled == null) continue;
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            return Instantiate(_popupPrefab, transform);
        }

        private void ReturnView(HorrorDamageView view)
        {
            view.gameObject.SetActive(false);
            _pool.Enqueue(view);
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}
