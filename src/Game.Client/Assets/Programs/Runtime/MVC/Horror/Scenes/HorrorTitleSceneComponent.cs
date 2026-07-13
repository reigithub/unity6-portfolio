using Game.MVC.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Scenes
{
    public class HorrorTitleSceneComponent : GameSceneComponent
    {
        [SerializeField] private GameObject _titleMenuRoot;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _optionButton;
        [SerializeField] private Button _returnButton;
        [SerializeField] private Button _quitButton;

        [SerializeField] private GameObject _gameStartMenuRoot;
        [SerializeField] private Button _continueGameButton;
        [SerializeField] private Button _loadGameButton;
        [SerializeField] private Button _newGameButton;

        public Observable<Unit> OnStart => _startButton != null ? _startButton.OnClickAsObservable() : Observable.Empty<Unit>();
        public Observable<Unit> OnOption => _optionButton != null ? _optionButton.OnClickAsObservable() : Observable.Empty<Unit>();
        public Observable<Unit> OnReturn => _returnButton != null ? _returnButton.OnClickAsObservable() : Observable.Empty<Unit>();
        public Observable<Unit> OnQuit => _quitButton != null ? _quitButton.OnClickAsObservable() : Observable.Empty<Unit>();

        public Observable<Unit> OnContinueGame => _continueGameButton != null ? _continueGameButton.OnClickAsObservable() : Observable.Empty<Unit>();
        public Observable<Unit> OnLoadGame => _loadGameButton != null ? _loadGameButton.OnClickAsObservable() : Observable.Empty<Unit>();
        public Observable<Unit> OnNewGame => _newGameButton != null ? _newGameButton.OnClickAsObservable() : Observable.Empty<Unit>();

        public void Initialize(bool hasSaveData)
        {
            SetGameStartMenu(hasSaveData);

            if (_titleMenuRoot != null)
                _titleMenuRoot.SetActive(true);

            if (_gameStartMenuRoot != null)
                _gameStartMenuRoot.SetActive(false);
        }

        public void SetGameStartMenu(bool hasSaveData)
        {
            if (_continueGameButton != null)
                _continueGameButton.gameObject.SetActive(hasSaveData);

            if (_loadGameButton != null)
                _loadGameButton.gameObject.SetActive(hasSaveData);

            if (_newGameButton != null)
                _newGameButton.gameObject.SetActive(true);

            ResolveSelectable();
        }

        public void OpenGameStartMenu()
        {
            if (_gameStartMenuRoot == null) return;
            if (!_gameStartMenuRoot.activeSelf)
            {
                if (_titleMenuRoot != null)
                    _titleMenuRoot.SetActive(false);

                _gameStartMenuRoot.SetActive(true);
            }

            ResolveSelectable();
        }

        public void CloseGameStartMenu()
        {
            if (_gameStartMenuRoot == null) return;
            if (_gameStartMenuRoot.activeSelf)
            {
                _gameStartMenuRoot.SetActive(false);

                if (_titleMenuRoot != null)
                    _titleMenuRoot.SetActive(true);
            }

            ResolveSelectable();
        }
    }
}
