using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Game.Shared.Exceptions;
using Game.Shared.Scriptable.Database;
using Game.Shared.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Shared
{
    public class ScriptableDatabaseServiceTests
    {
        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _createdObjects)
                Object.DestroyImmediate(obj);
            _createdObjects.Clear();
        }

        // Addressables 非依存で基底フロー（LoadAsync）を検証するための fake。
        private sealed class FakeService : ScriptableDatabaseServiceBase
        {
            private readonly ScriptableDatabase _db;
            public FakeService(ScriptableDatabase db) => _db = db;
            protected override UniTask<ScriptableDatabase> LoadDatabaseAssetAsync() => UniTask.FromResult(_db);
        }

        /// <summary>
        /// テーブルフィールドをリフレクションで列挙し、除外指定以外へ空テーブルを結線した DB を作る。
        /// フィールド名のハードコードを避け、テーブル増減にテストが自動追従する。
        /// </summary>
        private ScriptableDatabase CreateDatabase(params string[] unassignedFieldNames)
        {
            var db = ScriptableObject.CreateInstance<ScriptableDatabase>();
            _createdObjects.Add(db);

            var so = new SerializedObject(db);
            foreach (var field in TableFields())
            {
                if (unassignedFieldNames.Contains(field.Name)) continue;

                var table = ScriptableObject.CreateInstance(field.FieldType);
                _createdObjects.Add(table);
                so.FindProperty(field.Name).objectReferenceValue = table;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return db;
        }

        private static IEnumerable<FieldInfo> TableFields() =>
            typeof(ScriptableDatabase)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => typeof(ScriptableTableBase).IsAssignableFrom(f.FieldType));

        [Test]
        public void LoadAsync_AllTablesAssigned_SetsDatabase()
        {
            var db = CreateDatabase();
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

        [Test]
        public void LoadAsync_UnassignedTable_Throws()
        {
            var db = CreateDatabase("horrorWeaponMasterTable");
            var service = new FakeService(db);

            Assert.Throws<MasterDataLoadException>(() => service.LoadAsync().GetAwaiter().GetResult());
            Assert.That(service.Database, Is.Null);
        }
    }
}
