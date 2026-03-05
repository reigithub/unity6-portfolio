using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// Mirror スポーン用プレハブ参照。
    /// Assets/NetworkAssets/SurvivorNetworkPrefabs.asset に配置し、
    /// Addressables 経由でロードしてランタイム生成の NetworkManager に登録する。
    /// </summary>
    [CreateAssetMenu(fileName = "SurvivorNetworkPrefabs", menuName = "Game/Survivor Network Prefabs")]
    public class SurvivorNetworkPrefabs : ScriptableObject
    {
        [Tooltip("NetworkIdentity を持つスポーン対象プレハブ")]
        public GameObject[] Prefabs;

        private static SurvivorNetworkPrefabs _instance;

        public static async UniTask<SurvivorNetworkPrefabs> LoadAsync()
        {
            if (_instance != null) return _instance;
            _instance = await Addressables.LoadAssetAsync<SurvivorNetworkPrefabs>("SurvivorNetworkPrefabs");
            return _instance;
        }
    }
}
