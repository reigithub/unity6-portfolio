using Game.MVC.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Scenes
{
    public class HorrorStageSceneComponent : GameSceneComponent
    {
        [SerializeField] private Button _returnButton;

        public Observable<Unit> OnReturn => _returnButton != null
            ? _returnButton.OnClickAsObservable()
            : Observable.Empty<Unit>();
    }
}
