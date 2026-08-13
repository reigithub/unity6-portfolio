using Game.Horror.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.MVC.Horror.Interaction
{
    /// <summary>
    /// <see cref="IInteractable.AllowOutOfView"/> の宣言値検証。
    /// 「視界外フォールバック（画面端クランプ表示での Actionable 成立）の対象は
    /// 拾得系（アイテム・ドロップ品・武器）のみ」という仕様の正本を、各具象クラスの宣言値として固定する。
    /// </summary>
    [TestFixture]
    public class InteractableOutOfViewTests
    {
        // AddComponent で実コンポーネントから宣言値を読む。
        // Awake はコライダー取得のみで無害、サービス解決を行う Start は EditMode テストでは呼ばれない
        private static bool GetDeclaredValue<T>() where T : InteractableBase
        {
            var gameObject = new GameObject(nameof(InteractableOutOfViewTests));
            try
            {
                return gameObject.AddComponent<T>().AllowOutOfView;
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        // 拾得系（インベントリへ加える対象）は視界外でも拾える
        [Test]
        public void Item_AllowsOutOfViewInteraction()
            => Assert.That(GetDeclaredValue<HorrorItemInteractable>(), Is.True);

        [Test]
        public void DropItem_AllowsOutOfViewInteraction()
            => Assert.That(GetDeclaredValue<HorrorDropItemInteractable>(), Is.True);

        [Test]
        public void Weapon_AllowsOutOfViewInteraction()
            => Assert.That(GetDeclaredValue<HorrorWeaponInteractable>(), Is.True);

        // 据え置きの装置は視界外では表示・操作させない
        [Test]
        public void Door_DisallowsOutOfViewInteraction()
            => Assert.That(GetDeclaredValue<HorrorDoorInteractable>(), Is.False);

        [Test]
        public void Chair_DisallowsOutOfViewInteraction()
            => Assert.That(GetDeclaredValue<HorrorChairInteractable>(), Is.False);

        [Test]
        public void Savepoint_DisallowsOutOfViewInteraction()
            => Assert.That(GetDeclaredValue<HorrorSavepointInteractable>(), Is.False);
    }
}
