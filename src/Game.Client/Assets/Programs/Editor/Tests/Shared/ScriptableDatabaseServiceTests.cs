using Cysharp.Threading.Tasks;
using Game.Shared.Exceptions;
using Game.Shared.Scriptable.Database;
using Game.Shared.Services;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Shared
{
    public class ScriptableDatabaseServiceTests
    {
        // Addressables 非依存で基底フロー（LoadAsync）を検証するための fake。
        private sealed class FakeService : ScriptableDatabaseServiceBase
        {
            private readonly ScriptableDatabase _db;
            public FakeService(ScriptableDatabase db) => _db = db;
            protected override UniTask<ScriptableDatabase> LoadDatabaseAssetAsync() => UniTask.FromResult(_db);
        }

        [Test]
        public void LoadAsync_Success_SetsDatabase()
        {
            var db = ScriptableObject.CreateInstance<ScriptableDatabase>();
            var service = new FakeService(db);

            service.LoadAsync().GetAwaiter().GetResult();

            Assert.AreSame(db, service.Database);
        }

        [Test]
        public void LoadAsync_NullAsset_Throws()
        {
            var service = new FakeService(null);

            Assert.Throws<MasterDataLoadException>(() => service.LoadAsync().GetAwaiter().GetResult());
        }
    }
}
