using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sample
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float _speed = 1f;
        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private TextMeshProUGUI _winText;

        private Rigidbody _rb;
        private int _count;
        private float _movementX;
        private float _movementY;

        private void Start()
        {
            if (TryGetComponent<Rigidbody>(out var rigidBody))
            {
                _rb = rigidBody;
            }

            _count = 0;
            SetCountText();
            _winText.gameObject.SetActive(false);
        }

        private void OnMove(InputValue inputValue)
        {
            var inputVector2 = inputValue.Get<Vector2>();
            _movementX = inputVector2.x;
            _movementY = inputVector2.y;
        }

        private void SetCountText()
        {
            _countText.text = "Count: " + _count;

            if (_count >= 16)
            {
                _winText.gameObject.SetActive(true);
            }
        }

        private void FixedUpdate()
        {
            var vector3 = new Vector3(_movementX, 0.0f, _movementY);
            _rb.AddForce(vector3 * _speed);
        }

        private void OnCollisionEnter(Collision other)
        {
            if (other.gameObject.name.Contains("Enemy"))
            {
                other.gameObject.SetActive(false);

                _winText.gameObject.SetActive(true);
                _winText.text = "You Lose!!";
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.name.Contains("PickUp"))
            {
                other.gameObject.SetActive(false);
                _count++;
                SetCountText();
            }
        }
    }
}