using UnityEngine;
using UnityEngine.UI;

public class Timer : MonoBehaviour
{
    private int minute;
    private float seconds = 0f;

    private float oldSeconds;
    private Text timerText;
    private float speed = 1.0f;
    private float time;

    private bool isTimeup = false;

    private Color textColor;

    private void Start()
    {
        timerText = GetComponentInChildren<Text>();
        textColor = timerText.color;
    }

    private void Update()
    {
        // GameManager GM = GameManager.Instance;
        // if (!GM.isTimeup && seconds <= 0f)
        // {
        //     ResetTimer(GM.limitTime);
        //     isTimeup = false;
        // }
        //
        // if (!GM.isTimeup && GM.isRetry == true)
        // {
        //     ResetTimer(GM.limitTime);
        //     GM.isRetry = false;
        // }
        //
        // if (!isTimeup)
        // {
        //     seconds -= Time.deltaTime;
        //     minute = (int)seconds / 60;
        //
        //     if((int)seconds != (int)oldSeconds) {
        //         timerText.text = minute.ToString("00") + ":" + ((int)seconds % 60).ToString ("00");
        //     }
        //
        //     oldSeconds = seconds;
        // }
        //
        // if (seconds <= 0f)
        // {
        //     isTimeup = true;
        //     GM.isTimeup = true;
        //     timerText.color = GetAlphaColor(timerText.color);
        // }
    }

    private void ResetTimer(float resetTime)
    {
        seconds = resetTime;
        minute = (int)seconds / 60;
        oldSeconds = 0f;
        timerText.color = textColor;
    }

    private Color GetAlphaColor(Color color)
    {
        time += Time.deltaTime * 5.0f * speed;
        color.a = Mathf.Sin(time) * 0.5f + 0.5f;
        return color;
    }
}