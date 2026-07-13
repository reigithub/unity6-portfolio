using Game.MVC.Core.Scenes;
using TMPro;
using UnityEngine;

namespace Game.Horror.Dialogs
{
    public class HorrorMessageDialogComponent : GameSceneComponent
    {
        [SerializeField]
        private TextMeshProUGUI _messageText;

        public void SetMessage(string message)
            => _messageText.text = message;
    }
}
