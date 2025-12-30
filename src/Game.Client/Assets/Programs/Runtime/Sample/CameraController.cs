using UnityEngine;

namespace Sample
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private GameObject _player;

        private Vector3 _offset;
        private bool _initialized;

        private Vector3 _currentPos; //現在のカメラ位置
        private Vector3 _pastPos;    //過去のカメラ位置

        private Vector3 _diff; //移動距離

        private void Initialize()
        {
            //最初のプレイヤーの位置の取得
            _pastPos = _player.transform.position;
        }

        public void SetPlayer(GameObject p)
        {
            _player = p;
            Initialize();
        }

        private void LateUpdate()
        {
            if (!_player) return;

            //------カメラの移動------

            //プレイヤーの現在地の取得
            _currentPos = _player.transform.position;

            _diff = _currentPos - _pastPos;

            // transform.position = Vector3.Lerp(transform.position, transform.position + _diff, 1.0f); //カメラをプレイヤーの移動差分だけうごかすよ

            _pastPos = _currentPos;

            //------カメラの回転------

            // マウスの移動量を取得
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");

            // X方向に一定量移動していれば横回転
            if (Mathf.Abs(mx) > 0.01f)
            {
                // 回転軸はワールド座標のY軸
                transform.RotateAround(_player.transform.position, Vector3.up, mx);
            }

            // Y方向に一定量移動していれば縦回転
            if (Mathf.Abs(my) > 0.01f)
            {
                // 回転軸はカメラ自身のX軸
                transform.RotateAround(_player.transform.position, transform.right, -my);
            }
        }
    }
}