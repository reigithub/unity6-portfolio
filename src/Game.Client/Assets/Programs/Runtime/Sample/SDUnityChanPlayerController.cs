using System;
using Game.Core;
using Game.Core.MessagePipe;
using Game.Core.Services;
using UnityChan;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sample
{
    /// <summary>
    /// SD-Unityちゃん用のプレイヤーコントローラー
    /// </summary>
    public class SDUnityChanPlayerController : MonoBehaviour, SDUnityChanInputSystem.IPlayerActions
    {
        [Header("歩く速度")]
        [SerializeField]
        private float _walkSpeed = 2.0f;

        [Header("走る速度")]
        [SerializeField]
        private float _runSpeed = 5.0f;

        [Header("振り向き補間比率")]
        [SerializeField]
        private float _rotationRatio = 10.0f;

        [Header("ジャンプ力")]
        [SerializeField]
        private float _jumpForce = 5.0f;

        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        private GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

        private SDUnityChanInputSystem _inputSystem;
        private SDUnityChanInputSystem.PlayerActions _player;

        private Animator _animator;
        private Rigidbody _rigidbody;
        private RaycastChecker _groundedRaycastChecker;

        private Vector3 _moveVector = Vector3.zero;
        private float _speed;
        private Quaternion _lookRotation = Quaternion.identity;
        private bool _jumpTriggered;

        private void Awake()
        {
            _inputSystem = new SDUnityChanInputSystem();
            _player = _inputSystem.Player;
            _inputSystem.Player.SetCallbacks(this);
        }

        private void OnEnable()
        {
            _inputSystem.Enable();
            _player.Enable();
        }

        private void OnDisable()
        {
            _inputSystem.Disable();
            _player.Disable();
        }

        private void OnDestroy()
        {
            _inputSystem.Dispose();
        }

        private void Start()
        {
            TryGetComponent<Animator>(out _animator);
            TryGetComponent<Rigidbody>(out _rigidbody);
            TryGetComponent<RaycastChecker>(out _groundedRaycastChecker);
        }

        private void Update()
        {
            var moveValue = _player.Move.ReadValue<Vector2>();
            _moveVector = new Vector3(moveValue.x, 0.0f, moveValue.y).normalized;

            _speed = _moveVector.magnitude;
            if (_speed > 0f)
            {
                _speed *= _player.LeftShift.IsPressed() ? _walkSpeed : _runSpeed;
            }

            _animator.SetFloat(Animator.StringToHash("Speed"), _speed);

            if (_moveVector.magnitude > 0.1f)
            {
                _lookRotation = Quaternion.LookRotation(_moveVector);
            }

            // 押した瞬間のみ検知
            if (!_jumpTriggered && _player.Jump.WasPressedThisFrame())
            {
                if (IsGrounded())
                {
                    _animator.SetTrigger(Animator.StringToHash("Jump"));
                    _jumpTriggered = true;
                }
            }
        }

        private void FixedUpdate()
        {
            Move();
            Jump();
        }

        private void Move()
        {
            // 移動
            _rigidbody.MovePosition(_rigidbody.position + _moveVector * _speed * Time.fixedDeltaTime);
            // _rigidbody.AddForce(_moveVector * speed);
            // transform.Translate(moveVector * speed * Time.deltaTime, Space.World);

            // 向き
            _rigidbody.MoveRotation(Quaternion.Slerp(_rigidbody.rotation, _lookRotation, _rotationRatio * Time.fixedDeltaTime));

            // var torque = transform.up * moveVector.x * moveVector.magnitude;
            // _rigidbody.AddTorque(torque, ForceMode.Acceleration);

            // if (!Mathf.Approximately(_moveVector.magnitude, 0f))
            // {
            //     Quaternion from = transform.rotation;
            //     Quaternion to = Quaternion.LookRotation(_moveVector);
            //     transform.rotation = Quaternion.RotateTowards(from, to, 720f * Time.deltaTime);
            // }
        }

        private void Jump()
        {
            if (_jumpTriggered && _player.Jump.IsPressed())
            {
                // _rigidbody.linearDamping = 0.2f;
                // _rigidbody.linearVelocity = new Vector3(_rigidbody.linearVelocity.x, _jumpPower, _rigidbody.linearVelocity.z);
                _rigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);

                if (IsGrounded())
                {
                    _jumpTriggered = false;
                }
            }
        }

        private bool IsGrounded()
        {
            return _groundedRaycastChecker.Check();
        }

        private void OnCollisionEnter(Collision other)
        {
            if (other.gameObject.name.Contains("Enemy"))
            {
                other.gameObject.SetActive(false);

                GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.Sample.EnemyCollied, true);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.name.Contains("PickUp"))
            {
                other.gameObject.SetActive(false);

                GlobalMessageBroker.GetPublisher<int, int>().Publish(MessageKey.Sample.AddScore, 1);
            }
        }

        #region SDUnityChanInputSystem.IPlayerActions

        public void OnMove(InputAction.CallbackContext context)
        {
        }

        public void OnLook(InputAction.CallbackContext context)
        {
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
        }

        public void OnJump(InputAction.CallbackContext context)
        {
        }

        public void OnPrevious(InputAction.CallbackContext context)
        {
        }

        public void OnNext(InputAction.CallbackContext context)
        {
        }

        public void OnReset(InputAction.CallbackContext context)
        {
        }

        public void OnLeftAlt(InputAction.CallbackContext context)
        {
        }

        public void OnLeftControl(InputAction.CallbackContext context)
        {
        }

        public void OnLeftShift(InputAction.CallbackContext context)
        {
        }

        public void OnMouseScrollY(InputAction.CallbackContext context)
        {
        }

        #endregion
    }
}