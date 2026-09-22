using SocketSave;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class Timetable : MonoBehaviour
{
    public static Timetable Instance;

    [Header("Managers")]
    [SerializeField] private SkyManager skyManager;
    [SerializeField] private LV lv;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private OnlineNetworkServer socketServer;
    [SerializeField] private NormalSprite normalSprite;
    [SerializeField] private CameraControl cameraControl;

    [Header("UI")]
    [SerializeField] private Text TimeText;
    [SerializeField] private Text WeatherText;
    [SerializeField] private Text TemperatureText;
    [SerializeField] private Text WindText;
    [SerializeField] private Transform WindPointer;
    [SerializeField] private Image TemptMeter;
    [SerializeField] private Image CurrWeather;
    [SerializeField] private Image NextWeather;
    [SerializeField] private Text NextWeatherText;
    [SerializeField] private Image NextWeather2;
    [SerializeField] private Text NextWeather2Text;

    [Header("Camera Post Processing")]
    [SerializeField] private Volume volume;

    [Header("Wind")]
    [SerializeField] private float maxShakeAngle = 30f;
    [SerializeField] private float shakeFrequency = 5f;

    private WhiteBalance whiteBalance;
    private float baseAngle;

    private float WindStrength =>
        skyManager != null ? skyManager.WindScale * 0.4f : 0f;

    private void Awake()
    {
        Instance = this;

        if (volume != null && volume.profile != null)
            volume.profile.TryGet(out whiteBalance);
    }

    private void Update()
    {
        if (skyManager == null || WindPointer == null)
            return;

        float target = skyManager.WindTowardRight ? 0f : 180f;

        if (baseAngle != target)
            baseAngle = Mathf.MoveTowards(
                baseAngle,
                target,
                Mathf.Abs(target - baseAngle) * 5f * Time.deltaTime
            );

        WindPointer.eulerAngles = new Vector3(
            0f,
            0f,
            baseAngle + CalculateShake()
        );
    }

    private float CalculateShake()
    {
        float strength = WindStrength;

        if (strength <= 0.01f)
            return 0f;

        float shake =
            (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) * 2f - 1f) *
            strength * maxShakeAngle;

        if (Mathf.Abs(shake) < strength * 0.5f)
            shake += Mathf.Sin(Time.time * 30f) * strength * 0.5f;

        return shake;
    }

    public void UpdateTime(int time)
    {
        if (TimeText != null)
            TimeText.text = $"{time / 60:00}:{time % 60:00}";
    }

    public void UpdateWeather()
    {
        if (skyManager == null || normalSprite == null ||
            WeatherText == null || WindText == null || CurrWeather == null)
            return;

        int rain = skyManager.RainScale;
        int snow = skyManager.SnowScale;
        int hail = skyManager.HailScale;

        if (skyManager.IsThunder)
        {
            WeatherText.text = "雷雨";
            CurrWeather.sprite = normalSprite.ThunderRain;
        }
        else if (rain > 0)
        {
            WeatherText.text = rain <= 4 ? "小雨" : rain <= 7 ? "中雨" : "大雨";
            CurrWeather.sprite = rain <= 4
                ? normalSprite.SmallRain
                : normalSprite.MidRain;
        }
        else if (snow > 0)
        {
            WeatherText.text = snow <= 4 ? "小雪" : snow <= 7 ? "中雪" : "大雪";
            CurrWeather.sprite = snow <= 4
                ? normalSprite.SmallSnow
                : snow <= 7
                    ? normalSprite.MidSnow
                    : normalSprite.BigSnow;
        }
        else if (hail > 0)
        {
            WeatherText.text = hail <= 4 ? "轻雹" : hail <= 7 ? "中雹" : "重雹";
            CurrWeather.sprite = hail <= 4
                ? normalSprite.SmallHail
                : hail <= 7
                    ? normalSprite.MidHail
                    : normalSprite.BigHail;
        }
        else
        {
            WeatherText.text = "晴朗";
            CurrWeather.sprite = normalSprite.Clear;
        }

        WindText.text = skyManager.WindScale.ToString();
    }

    public void UpdateWeatherReport()
    {
        if (lv == null || normalSprite == null)
            return;

        NextWeatherText.text = "";
        NextWeather2Text.text = "";

        if (lv.LoadWeathers.Count > 0)
        {
            SetReport(
                NextWeather,
                lv.LoadWeathers[0].type,
                lv.LoadWeathers[0].scale,
                NextWeatherText
            );

            if (lv.LoadWeathers.Count > 1)
            {
                SetReport(
                    NextWeather2,
                    lv.LoadWeathers[1].type,
                    lv.LoadWeathers[1].scale,
                    NextWeather2Text
                );
            }
            else
            {
                NextWeather2.sprite = normalSprite.NoWeather;
            }
        }
        else
        {
            NextWeather.sprite = normalSprite.NoWeather;
            NextWeather2.sprite = normalSprite.NoWeather;
        }

        if (gameManager == null || !gameManager.isServer || socketServer == null)
            return;

        TimetableSyn syn = new TimetableSyn();

        if (lv.LoadWeathers.Count > 0)
        {
            syn.type = lv.LoadWeathers[0].type;
            syn.WeSc = lv.LoadWeathers[0].scale;

            if (lv.LoadWeathers.Count > 1)
            {
                syn.type2 = lv.LoadWeathers[1].type;
                syn.WeSc2 = lv.LoadWeathers[1].scale;
            }
        }

        socketServer.SynTimeTable(syn);
    }

    private void SetReport(
        Image image,
        WeatherType type,
        int scale,
        Text text)
    {
        if (image == null || text == null || normalSprite == null)
            return;

        text.text = "";

        switch (type)
        {
            case WeatherType.Nope:
                image.sprite = normalSprite.NoWeather;
                break;

            case WeatherType.Clear:
                image.sprite = normalSprite.Clear;
                break;

            case WeatherType.Rain:
                image.sprite = scale <= 4
                    ? normalSprite.SmallRain
                    : normalSprite.MidRain;
                break;

            case WeatherType.Thunder:
                image.sprite = normalSprite.ThunderRain;
                break;

            case WeatherType.Snow:
                image.sprite = scale <= 4
                    ? normalSprite.SmallSnow
                    : scale <= 7
                        ? normalSprite.MidSnow
                        : normalSprite.BigSnow;
                break;

            case WeatherType.Wind:
                text.text = scale.ToString();
                image.sprite = normalSprite.Wind;
                break;

            case WeatherType.Hail:
                image.sprite = scale <= 4
                    ? normalSprite.SmallHail
                    : scale <= 7
                        ? normalSprite.MidHail
                        : normalSprite.BigHail;
                break;
        }
    }

    public void UpdateTempt(MapBase tempt)
    {
        if (tempt == null ||
            cameraControl == null ||
            tempt != cameraControl.CurrMap ||
            TemptMeter == null ||
            TemperatureText == null ||
            whiteBalance == null)
            return;

        float temperature = tempt.CurrTempt;

        TemptMeter.color = temperature > 0f
            ? new Color(1f, 0.5f, 0f)
            : new Color(0.51f, 0.94f, 1f);

        TemptMeter.fillAmount = Mathf.Abs(temperature) / 50f;
        TemperatureText.text = ((int)temperature).ToString();

        float value = (int)temperature;

        if (value > 0 && value < 10)
            value = 0;
        else if (value > 10)
            value -= 10;

        whiteBalance.temperature.overrideState = true;
        whiteBalance.temperature.value = value;
    }

    public void LvReset()
    {
        if (whiteBalance == null)
            return;

        whiteBalance.temperature.overrideState = true;
        whiteBalance.temperature.value = 0f;
    }

    public void ClientSyn(TimetableSyn syn)
    {
        if (syn == null)
            return;

        SetReport(
            NextWeather,
            syn.type,
            syn.WeSc,
            NextWeatherText
        );

        SetReport(
            NextWeather2,
            syn.type2,
            syn.WeSc2,
            NextWeather2Text
        );
    }
}