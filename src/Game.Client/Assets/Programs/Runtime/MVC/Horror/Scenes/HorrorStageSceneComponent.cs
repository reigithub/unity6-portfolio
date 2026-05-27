using Game.MVC.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Scenes
{
    public class HorrorStageSceneComponent : UnitySceneComponent
    {
        [SerializeField] private Button _returnButton;
        [SerializeField] private Button _nextButton;

        public Observable<Unit> OnReturn => _returnButton != null
            ? _returnButton.OnClickAsObservable()
            : Observable.Empty<Unit>();

        public Observable<Unit> OnNext => _nextButton != null
            ? _nextButton.OnClickAsObservable()
            : Observable.Empty<Unit>();
    }
}
