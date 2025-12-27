using UnityEngine;

namespace Sample
{
    /// <summary>
    /// オブジェクトを回転させる
    /// </summary>
    public class Rotator : MonoBehaviour
    {
        private void Update()
        {
            transform.Rotate(new Vector3(15f, 30f, 45f) * Time.deltaTime);
        }
    }
}