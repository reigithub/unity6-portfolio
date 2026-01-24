using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// 武器用オブジェクトプール（ジェネリック）
    /// Projectile、GroundDamageArea等で共通利用
    /// </summary>
    /// <typeparam name="T">プール対象のMonoBehaviour型（IPoolableWeaponItem実装）</typeparam>
    internal class WeaponObjectPool<T> where T : MonoBehaviour, IPoolableWeaponItem
    {
        private readonly Queue<T> _pool = new();
        private readonly HashSet<T> _activeItems = new();
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly Action<T> _onCreated;

        /// <summary>
        /// 現在アクティブ（使用中）のアイテム数
        /// </summary>
        public int ActiveCount => _activeItems.Count;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="prefab">プールするプレハブ</param>
        /// <param name="initialSize">初期プールサイズ</param>
        /// <param name="parent">親Transform</param>
        /// <param name="onCreated">アイテム生成時のコールバック（イベント登録用）</param>
        public WeaponObjectPool(
            GameObject prefab,
            int initialSize,
            Transform parent,
            Action<T> onCreated)
        {
            _prefab = prefab;
            _parent = parent;
            _onCreated = onCreated;

            // 初期プールを作成
            for (int i = 0; i < initialSize; i++)
            {
                var item = CreateItem();
                item.gameObject.SetActive(false);
                _pool.Enqueue(item);
            }
        }

        /// <summary>
        /// アイテムを生成
        /// </summary>
        private T CreateItem()
        {
            var instance = UnityEngine.Object.Instantiate(_prefab, _parent);
            var item = instance.GetComponent<T>();

            if (item == null)
            {
                item = instance.AddComponent<T>();
            }

            // 生成時コールバック（イベント登録）
            _onCreated?.Invoke(item);

            return item;
        }

        /// <summary>
        /// プールからアイテムを取得
        /// </summary>
        public T Get()
        {
            T item = null;

            // nullチェックしながらデキュー
            while (_pool.Count > 0)
            {
                item = _pool.Dequeue();
                if (item != null)
                {
                    break;
                }
            }

            // プールが空なら新規作成
            if (item == null)
            {
                item = CreateItem();
            }

            _activeItems.Add(item);
            return item;
        }

        /// <summary>
        /// アイテムをプールに返却（このプールに属しているか確認）
        /// </summary>
        /// <returns>true: 返却成功, false: このプールに属していない</returns>
        public bool TryReturn(T item)
        {
            if (!_activeItems.Contains(item))
            {
                return false;
            }

            _activeItems.Remove(item);
            _pool.Enqueue(item);
            return true;
        }

        /// <summary>
        /// アイテムをプールに返却（属性チェックなし）
        /// </summary>
        public void Return(T item)
        {
            if (item == null) return;

            if (_activeItems.Contains(item))
            {
                _activeItems.Remove(item);
            }

            _pool.Enqueue(item);
        }

        /// <summary>
        /// プールをクリア（全アイテム破棄）
        /// </summary>
        public void Clear()
        {
            // プール内のアイテムを破棄
            foreach (var item in _pool)
            {
                if (item != null)
                {
                    item.ClearListeners();
                    UnityEngine.Object.Destroy(item.gameObject);
                }
            }
            _pool.Clear();

            // アクティブなアイテムも破棄
            foreach (var item in _activeItems)
            {
                if (item != null)
                {
                    item.ClearListeners();
                    UnityEngine.Object.Destroy(item.gameObject);
                }
            }
            _activeItems.Clear();
        }
    }
}
