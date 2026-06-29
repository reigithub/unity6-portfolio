using Game.MVC.Core.Scenes;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Dialogs
{
    public class HorrorMessageDialogComponent : GameSceneComponent
    {
        [SerializeField]
        private TextMeshProUGUI _messageText;

        [SerializeField]
        private Button _closeButton;

        public Observable<Unit> OnClose => _closeButton.OnClickAsObservable();

        public void SetMessage(string message)
            => _messageText.text = message;
    }
}
