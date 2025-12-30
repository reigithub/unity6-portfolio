using UnityEngine;
using UnityEngine.UI;

namespace Sample
{
    public class GameTimerUI : MonoBehaviour
    {
        [SerializeField]
        private Text _timeText;

        // private int _minute;
        // private float _seconds = 0f;
        // private float _oldSeconds;
        // private float _speed = 1.0f;
        // private float _time;
        //
        // private bool isTimeup = false;
        //
        // private Color textColor;
        //
        // private void Start()
        // {
        //     // _timeText = GetComponentInChildren<Text>();
        //     textColor = _timeText.color;
        // }
        //
        // private void Update()
        // {
        //     GameManager GM = GameManager.Instance;
        //     if (!GM.isTimeup && _seconds <= 0f)
        //     {
        //         ResetTimer(GM.limitTime);
        //         isTimeup = false;
        //     }
        //     
        //     if (!GM.isTimeup && GM.isRetry == true)
        //     {
        //         ResetTimer(GM.limitTime);
        //         GM.isRetry = false;
        //     }
        //
        //     if (!isTimeup)
        //     {
        //         _seconds -= Time.deltaTime;
        //         _minute = (int)_seconds / 60;
        //     
        //         if ((int)_seconds != (int)_oldSeconds)
        //         {
        //             _timeText.text = _minute.ToString("00") + ":" + ((int)_seconds % 60).ToString("00");
        //         }
        //     
        //         _oldSeconds = _seconds;
        //     }
        //     
        //     if (_seconds <= 0f)
        //     {
        //         isTimeup = true;
        //         GM.isTimeup = true;
        //         _timeText.color = GetAlphaColor(_timeText.color);
        //     }
        // }
        //
        // private void ResetTimer(float resetTime)
        // {
        //     _seconds = resetTime;
        //     _minute = (int)_seconds / 60;
        //     _oldSeconds = 0f;
        //     _timeText.color = textColor;
        // }
        //
        // private Color GetAlphaColor(Color color)
        // {
        //     _time += Time.deltaTime * 5.0f * _speed;
        //     color.a = Mathf.Sin(_time) * 0.5f + 0.5f;
        //     return color;
        // }
    }
}