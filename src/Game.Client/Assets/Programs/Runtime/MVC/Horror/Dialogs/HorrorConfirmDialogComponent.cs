using Game.MVC.Core.Scenes;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Dialogs
{
    public class HorrorConfirmDialogComponent : GameSceneComponent
    {
        [SerializeField] private TextMeshProUGUI _message;
        [SerializeField] private Button _submitButton;
        [SerializeField] private Button _cancelButton;

        public Observable<bool> OnSubmit => _submitButton.OnClickAsObservable().Select(_ => true);
        public Observable<bool> OnCancel => _cancelButton.OnClickAsObservable().Select(_ => false);

        public void Initialize(string message) => _message.text = message;
    }
}
