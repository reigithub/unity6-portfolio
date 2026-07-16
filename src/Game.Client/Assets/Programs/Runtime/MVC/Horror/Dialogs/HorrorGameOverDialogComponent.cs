using Game.MVC.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Dialogs
{
    public class HorrorGameOverDialogComponent : GameSceneComponent
    {
        [SerializeField]
        private Button _continueGameButton;

        [SerializeField]
        private Button _loadGameButton;

        [SerializeField]
        private Button _quitButton;

        public Observable<Unit> OnContinueGame => _continueGameButton.OnClickAsObservable();
        public Observable<Unit> OnLoadGame => _loadGameButton.OnClickAsObservable();
        public Observable<Unit> OnQuit => _quitButton.OnClickAsObservable();
    }
}
