using Extensions;
using System.Threading.Tasks;
using Game.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Sample
{
    public class Sample : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log(Addressables.BuildPath);
            Debug.Log(Addressables.PlayerBuildDataPath);
            Debug.Log(Addressables.RuntimePath);

            // Test: Load and Exec MoonSharp Lua
            // var files = Directory.GetFiles("Assets/Lua", "*.lua", SearchOption.AllDirectories);
            // var scripts = files
            //     .ToDictionary(
            //         x => Path.GetFileName(x).Replace(".lua", ""),
            //         x => new StreamReader(x.Replace("\\", "/")).ReadToEnd()
            //         );
            // var st = new FileStream("path", FileMode.Open, FileAccess.Read);

            // Test: Load Addressable Asset
            LoadCubeAssetAsync().Forget();
        }

        private async Task LoadCubeAssetAsync()
        {
            // var asset = await Addressables.LoadAssetAsync<GameObject>("Assets/Project/Prefabs/Cube.prefab").Task;
            // var obj = Instantiate(asset, gameObject.transform, false);
            // if (obj.TryGetComponent<Cube>(out var cube))
            // {
            //     cube.Greet();
            // }
            await Task.CompletedTask;
        }
    }
}