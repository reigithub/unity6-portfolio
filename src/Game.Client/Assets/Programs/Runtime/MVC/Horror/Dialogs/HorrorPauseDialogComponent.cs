using Game.MVC.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Dialogs
{
    public class HorrorPauseDialogComponent : GameSceneComponent
    {
        [SerializeField]
        private Button _resumeButton;

        [SerializeField]
        private Button _optionButton;

        [SerializeField]
        private Button _returnButton;

        [SerializeField]
        private Button _quitButton;

        public Observable<Unit> OnResume => _resumeButton.OnClickAsObservable();
        public Observable<Unit> OnOption => _optionButton.OnClickAsObservable();
        public Observable<Unit> OnReturn => _returnButton.OnClickAsObservable();
        public Observable<Unit> OnQuit => _quitButton.OnClickAsObservable();
    }
}
