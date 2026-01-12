using Game.Core.MessagePipe;
using Game.Core.Services;
using R3;
using UnityChan;
using UnityEngine;

namespace Game.Contents.UI
{
    /// <summary>
    /// ゲームUIコントローラー
    /// </summary>
    public class GameUIController : MonoBehaviour //, SDUnityChanInputSystem.IUIActions
    {
        private MessagePipeService _messagePipeService;
        private MessagePipeService MessagePipeService => _messagePipeService ??= GameServiceManager.Get<MessagePipeService>();

        private SDUnityChanInputSystem _inputSystem;
        private SDUnityChanInputSystem.UIActions _ui;

        private bool _pause;

        public void Initialize()
        {
            MessagePipeService.Subscribe<bool>(MessageKey.InputSystem.Escape, status =>
                {
                    if (status)
                        _ui.Escape.Enable();
                    else
                        _ui.Escape.Disable();
                })
                .AddTo(this);
            MessagePipeService.Subscribe<bool>(MessageKey.InputSystem.ScrollWheel, status =>
                {
                    if (status)
                        _ui.ScrollWheel.Enable();
                    else
                        _ui.ScrollWheel.Disable();
                })
                .AddTo(this);
        }

        private void Awake()
        {
            _inputSystem = new SDUnityChanInputSystem();
            _ui = _inputSystem.UI;
        }

        private void OnEnable()
        {
            _inputSystem.Enable();
            _ui.Enable();
        }

        private void OnDisable()
        {
            _inputSystem.Disable();
            _ui.Disable();
        }

        private void OnDestroy()
        {
            _inputSystem.Dispose();
        }

        private void Start()
        {
        }

        private void Update()
        {
            // _ui.{InputAction}.IsPressed() //押す～離す間
            // _ui.{InputAction}.WasPressedThisFrame() //押した瞬間
            // _ui.{InputAction}.WasReleasedThisFrame() //離した瞬間

            if (_ui.Escape.WasPressedThisFrame())
            {
                MessagePipeService.Publish(MessageKey.UI.Escape, true);
            }

            if (_ui.ScrollWheel.WasPressedThisFrame())
            {
                // 今はプレイヤーフォローカメラ操作用
                var scrollWheel = _ui.ScrollWheel.ReadValue<Vector2>().normalized;
                // Debug.Log($"ScrollWheel(WasPressedThisFrame)=> x: {scrollWheel.x}, y: {scrollWheel.y}");
                MessagePipeService.Publish(MessageKey.UI.ScrollWheel, scrollWheel);
            }
        }

        private void FixedUpdate()
        {
        }
    }
}