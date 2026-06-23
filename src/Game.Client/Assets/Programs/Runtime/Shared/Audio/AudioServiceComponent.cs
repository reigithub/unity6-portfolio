using UnityEngine;
using UnityEngine.Audio;

namespace Game.Shared
{
    public class AudioServiceComponent : MonoBehaviour
    {
        [SerializeField] private AudioMixer _audioMixer;

        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioSource _voiceSource;
        [SerializeField] private AudioSource _seSource;

        public AudioMixer AudioMixer => _audioMixer;

        public AudioSource BgmSource => _bgmSource;
        public AudioSource VoiceSource => _voiceSource;
        public AudioSource SeSource => _seSource;
    }
}
