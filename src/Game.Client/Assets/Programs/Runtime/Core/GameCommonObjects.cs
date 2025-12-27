using Cysharp.Threading.Tasks;
using Game.Core.Extensions;
using Game.Core.Services;
using Game.Core.MessagePipe;
using MessagePipe;
using R3;
using Sample;
using TMPro;
using UnityEngine;


namespace Game.Core
{
    public class GameCommonObjects : MonoBehaviour
    {
        private const string Address = "Assets/Prefabs/GameCommonObjects.prefab";

        public static GameCommonObjects Instance { get; private set; }

        public static async UniTask LoadAssetAsync()
        {
            var assetService = GameServiceManager.Instance.GetService<AddressableAssetService>();
            var prefab = await assetService.LoadAssetAsync<GameObject>(Address);
            if (prefab == null)
            {
                Debug.LogError($"Load Asset Failed. {Address}");
                Debug.Break();
            }

            Instance.SafeDestroy();
            Instance = null;

            var go = Instantiate(prefab);
            if (go.TryGetComponent<GameCommonObjects>(out var commonObjects))
            {
                DontDestroyOnLoad(go);
                commonObjects.Initialize();
                Instance = commonObjects;
            }
        }

        [SerializeField] private GameObject _player;
        [SerializeField] private PlayerFollowCameraController _playerFollowCameraController;

        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private TextMeshProUGUI _winText;

        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        private GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

        private int _count;

        private void Initialize()
        {
            _count = 0;
            SetCountText();

            GlobalMessageBroker.GetSubscriber<int, GameObject>()
                .Subscribe(MessageKey.Player.SpawnPlayer, handler: player =>
                {
                    _player = player;
                    _playerFollowCameraController.SetPlayer(player);
                })
                .AddTo(this);
            GlobalMessageBroker.GetSubscriber<int, int>()
                .Subscribe(MessageKey.Sample.AddScore, handler: score =>
                {
                    _count += score;
                    SetCountText();
                })
                .AddTo(this);
            GlobalMessageBroker.GetSubscriber<int, bool>()
                .Subscribe(MessageKey.Sample.EnemyCollied, handler: isCollied =>
                {
                    _winText.gameObject.SetActive(isCollied);
                    if (isCollied) _winText.text = "You Lose...";
                })
                .AddTo(this);
        }

        private void SetCountText()
        {
            _countText.gameObject.SetActive(true);
            _countText.text = "Count: " + _count;

            bool isWin = _count >= 16;
            _winText.gameObject.SetActive(isWin);
            if (isWin) _winText.text = "You Win!!";
        }
    }
}