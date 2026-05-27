using Cysharp.Threading.Tasks;
using Game.MVC.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Scenes
{
    public class HorrorTitleSceneComponent : GameSceneComponent
    {
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _returnButton;
        [SerializeField] private Button _quitButton;

        public Observable<Unit> OnStart => _startButton != null ? _startButton.OnClickAsObservable() : Observable.Empty<Unit>();
        public Observable<Unit> OnReturn => _returnButton != null ? _returnButton.OnClickAsObservable() : Observable.Empty<Unit>();
        public Observable<Unit> OnQuit => _quitButton != null ? _quitButton.OnClickAsObservable() : Observable.Empty<Unit>();
    }
}
