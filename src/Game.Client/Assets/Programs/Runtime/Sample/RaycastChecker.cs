using System;
using UnityEngine;

namespace Sample
{
    public class RaycastChecker : MonoBehaviour
    {
        [SerializeField] private LayerMask _layerMask;

        [SerializeField] private Vector3 _positionOffset = new Vector3(0, 0.1f, 0f);

        [SerializeField] private Vector3 _direction = Vector3.down;

        [SerializeField] private float _distance = 0.35f;

        /// <summary>
        /// 何らかのコライダーとレイキャストが交差しているか
        /// </summary>
        public bool Check()
        {
            var position = transform.position + _positionOffset;
            var ray = new Ray(position, _direction);

            // var a = Physics.Raycast(ray, _distance, _layerMask);
            // var b = Physics.Raycast(ray, out var raycastHit, _distance);
            return Physics.Raycast(ray, _distance);
        }

#if UNITY_EDITOR
        private void Update()
        {
            // Gizmo
            var position = transform.position + _positionOffset;
            Debug.DrawRay(position, _direction * _distance, Color.red);
        }
#endif
    }
}