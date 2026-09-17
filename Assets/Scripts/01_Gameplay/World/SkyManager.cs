using System.Collections;
using System.Collections.Generic;
using SocketSave;
using UnityEngine;

public class SkyManager : MonoBehaviour
{
    private int onlineSunId;

    public static SkyManager Instance;

    private readonly List<Sun> sunList = new List<Sun>();

    public int clickedSunNum;

    private Coroutine TimeCoroutine;
    private Coroutine RainChangeCoroutine;
    private Coroutine SnowChangeCoroutine;
    private Coroutine HailChangeCoroutine;

    [Header("Managers")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private StatsManager statsManager;
    [SerializeField] private LVManager lvManager;
    [SerializeField] private LV lv;
    [SerializeField] private AudioManager audioManager;
    public Timetable timetable;
    [SerializeField] private GobalEffManager gobalEffManager;
    [SerializeField] private MapManager mapManager;
    [SerializeField] private GobalLight gobalLight;
    [SerializeField] private LvItemManager lvItemManager;
    [SerializeField] private SocketServer socketServer;
    [SerializeField] private PlantManager plantManager;

    [SerializeField] private SpectatorList spectatorList;
    [SerializeField] private PvPSelector pvpSelector;
    public Lightning lightning;

    public GameObject NormalLightning;

    public SpriteRenderer ScrollFog;

    public ParticleSystem RainParticle;

    public ParticleSystem SnowParticle;

    public ParticleSystem HailParticle;

    public bool DayLightCycle;

    private ParticleSystem.EmissionModule RainEmissionModule;
    private ParticleSystem.EmissionModule SnowEmissionModule;
    private ParticleSystem.EmissionModule HailEmissionModule;

    private bool isThunder;
    private bool canLightning;
    private int LightningTime;

    public int WindTime;
    public int CurrWindTime;

    private bool windTowardRight;

    private int rainScale;
    private int snowScale;
    private int windScale;
    private int hailScale;

    [SerializeField]
    private int time;

    public bool SunAutoCollect;
    public bool SlowSunAutoCollect;
    public bool isRainFog;

    public int Time
    {
        get
        {
            return time;
        }
        set
        {
            int num = value;

            if (!DayLightCycle && !gameManager.isClient)
            {
                num = time;
            }

            statsManager.AddStatsNum(StatsEnum.GameTime);

            if (lvManager.GameIsStart)
            {
                if (GetIsDay(time) && !GetIsDay(num))
                {
                    audioManager.FadeBgAndPlayNew(lv.NightBgm);
                }
                else if (!GetIsDay(time) && GetIsDay(num))
                {
                    audioManager.FadeBgAndPlayNew(lv.DayBgm);
                }
            }

            time = num;

            if (Time > 1439)
            {
                time = 0;
            }

            timetable.UpdateTime(Time);

            gobalEffManager.RefreshEffect();

            if (!gameManager.isClient)
            {
                if (lv.LoadWeathers.Count > 0 &&
                    lv.LoadWeathers[0].appearTime != 0 &&
                    lv.LoadWeathers[0].appearTime == lvManager.PassTime)
                {
                    PlayWeather(lv.LoadWeathers[0]);
                    MyTool.MoveFirstToLast(lv.LoadWeathers);
                    timetable.UpdateWeatherReport();
                }

                if (DayLightCycle &&
                    lv.TimeAction.ContainsKey(Time))
                {
                    lv.TimeAction[Time]();
                }
            }

            clickedSunNum = 0;

            for (int i = 0; i < mapManager.mapList.Count; i++)
            {
                mapManager.mapList[i].BaseTimeChange(time);
            }

            gobalLight.TimeChange();

            UpdateWind();

            if (IsThunder && !gameManager.isClient)
            {
                if (canLightning &&
                    (Random.Range(0, 15) > 13 || LightningTime > 5))
                {
                    LightningTime = 0;

                    lightning.gameObject.SetActive(true);

                    Grid grid = mapManager.GetRandomGrid();

                    PlantBase plantBase = null;

                    List<Clematis> list = new List<Clematis>();

                    List<Grid> aroundGrid =
                        mapManager.GetAroundGrid(grid, 2);

                    for (int j = 0; j < aroundGrid.Count; j++)
                    {
                        if (aroundGrid[j].CurrPlantBase != null)
                        {
                            if (aroundGrid[j].CurrPlantBase is Clematis)
                            {
                                list.Add(
                                    (Clematis)aroundGrid[j].CurrPlantBase
                                );
                            }
                            else if (
                                aroundGrid[j].CurrPlantBase.CarryPlant
                                is Clematis)
                            {
                                list.Add(
                                    (Clematis)aroundGrid[j]
                                        .CurrPlantBase
                                        .CarryPlant
                                );
                            }
                        }
                    }

                    if (list.Count > 0)
                    {
                        plantBase =
                            list[
                                Random.Range(
                                    0,
                                    list.Count
                                )
                            ];

                        grid = plantBase.currGrid;
                    }
                    else
                    {
                        List<Umbrellaleaf> list2 =
                            new List<Umbrellaleaf>();

                        List<Grid> aroundGrid2 =
                            mapManager.GetAroundGrid(grid, 1);

                        for (int k = 0; k < aroundGrid2.Count; k++)
                        {
                            if (aroundGrid2[k].CurrPlantBase != null)
                            {
                                if (
                                    aroundGrid2[k].CurrPlantBase
                                    is Umbrellaleaf)
                                {
                                    list2.Add(
                                        (Umbrellaleaf)
                                        aroundGrid2[k].CurrPlantBase
                                    );
                                }
                                else if (
                                    aroundGrid2[k]
                                        .CurrPlantBase
                                        .CarryPlant
                                        is Umbrellaleaf)
                                {
                                    list2.Add(
                                        (Umbrellaleaf)
                                        aroundGrid2[k]
                                            .CurrPlantBase
                                            .CarryPlant
                                    );
                                }
                            }
                        }

                        if (list2.Count > 0)
                        {
                            plantBase =
                                list2[
                                    Random.Range(
                                        0,
                                        list2.Count
                                    )
                                ];

                            grid = plantBase.currGrid;
                        }
                    }

                    lightning.LightGrid(grid, plantBase);

                    StartCoroutine(WaitLightning());

                    if (gameManager.isServer)
                    {
                        LightingSpawn lightingSpawn =
                            new LightingSpawn();

                        lightingSpawn.Pos = grid.Position;

                        socketServer.SpawnLightning(
                            lightingSpawn
                        );
                    }
                }
                else
                {
                    LightningTime++;
                }
            }

            if (HailScale > 3 &&
                Random.Range(0, 15) < HailScale)
            {
                int count = mapManager.mapList.Count;

                for (int l = 0; l < count; l++)
                {
                    lvItemManager.SpawnFallHail(
                        mapManager.GetRandomGrid(),
                        Random.Range(
                            5,
                            6 + HailScale
                        ) / 10f
                    );
                }
            }
        }
    }

    public int OnlineSunId
    {
        get
        {
            onlineSunId++;
            return onlineSunId;
        }
        private set
        {
            onlineSunId = value;
        }
    }

    public int RainScale
    {
        get
        {
            return rainScale;
        }
        private set
        {
            rainScale = value;

            timetable.UpdateWeather();

            for (int i = 0; i < mapManager.mapList.Count; i++)
            {
                mapManager.mapList[i].BaseTimeChange(Time);
            }
        }
    }

    public bool IsThunder
    {
        get
        {
            return isThunder;
        }
        set
        {
            if (lvManager.InGame && isThunder != value)
            {
                isThunder = value;

                canLightning = true;

                gobalLight.ThunderChange();

                timetable.UpdateWeather();

                if (gameManager.isServer)
                {
                    WeatherChange weatherChange =
                        new WeatherChange();

                    weatherChange.type =
                        WeatherType.Rain;

                    weatherChange.WeSc =
                        RainScale;

                    weatherChange.isTud =
                        IsThunder;

                    socketServer.SendWeatherCmd(
                        weatherChange
                    );
                }
            }
        }
    }

    public int SnowScale
    {
        get
        {
            return snowScale;
        }
        private set
        {
            snowScale = value;

            timetable.UpdateWeather();

            for (int i = 0; i < mapManager.mapList.Count; i++)
            {
                mapManager.mapList[i].BaseTimeChange(Time);
            }
        }
    }

    public int WindScale
    {
        get
        {
            int num = windScale + RainScale / 4;

            if (num > 5)
            {
                num = 5;
            }

            return num;
        }
        private set
        {
            if (windScale != value)
            {
                if (value > 5)
                {
                    windScale = 5;
                }
                else
                {
                    windScale = value;
                }

                WindScaleReSet();

                if (gameManager.isServer)
                {
                    WeatherChange weatherChange =
                        new WeatherChange();

                    weatherChange.type =
                        WeatherType.Wind;

                    weatherChange.WeSc =
                        windScale;

                    weatherChange.isTud =
                        WindTowardRight;

                    socketServer.SendWeatherCmd(
                        weatherChange
                    );
                }
            }
        }
    }

    public bool WindTowardRight
    {
        get
        {
            return windTowardRight;
        }
        private set
        {
            windTowardRight = value;
        }
    }

    public int HailScale
    {
        get
        {
            return hailScale;
        }
        private set
        {
            hailScale = value;

            timetable.UpdateWeather();

            for (int i = 0; i < mapManager.mapList.Count; i++)
            {
                mapManager.mapList[i].BaseTimeChange(Time);
            }
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        DayLightCycle = true;
        canLightning = true;
        SunAutoCollect = false;

        RainEmissionModule =
            RainParticle.emission;

        RainEmissionModule.rateOverTime =
            new ParticleSystem.MinMaxCurve(0f);

        SnowEmissionModule =
            SnowParticle.emission;

        SnowEmissionModule.rateOverTime =
            new ParticleSystem.MinMaxCurve(0f);

        HailEmissionModule =
            HailParticle.emission;

        HailEmissionModule.rateOverTime =
            new ParticleSystem.MinMaxCurve(0f);

        audioManager.weatherAudio.enabled =
            false;
    }

    public void StartTime()
    {
        if (TimeCoroutine != null)
        {
            StopCoroutine(TimeCoroutine);
        }

        TimeCoroutine =
            StartCoroutine(TimeAdd());
    }

    public void StopTime()
    {
        if (TimeCoroutine != null)
        {
            StopCoroutine(TimeCoroutine);
            TimeCoroutine = null;
        }
    }

    private IEnumerator TimeAdd()
    {
        WaitForSeconds wait =
            new WaitForSeconds(1f);

        while (true)
        {
            yield return wait;

            if (lv.CurrLVType != LVType.IZombie &&
                lv.CurrLVType != LVType.VaseBreaker)
            {
                if (!gameManager.isClient)
                {
                    Time++;
                }

                if (gameManager.isServer)
                {
                    TimeCmd timeCmd =
                        new TimeCmd();

                    timeCmd.time = Time;

                    socketServer.SendTimeCmd(
                        timeCmd
                    );
                }
            }

            lvManager.PassTime++;
            lvManager.LvTotalTime--;
        }
    }

    public void SetWindScale(int scale)
    {
        WindScale = scale;
    }

    private void UpdateWind()
    {
        if (WindScale <= 0)
        {
            return;
        }

        if (WindTime == 0)
        {
            WindTime = Random.Range(20, 40);
            CurrWindTime += WindTime;
        }

        if (CurrWindTime > 0)
        {
            CurrWindTime--;

            if (CurrWindTime == 0)
            {
                CurrWindTime = -WindTime;
                SetWindToward(false);
            }
        }
        else if (CurrWindTime < 0)
        {
            CurrWindTime++;

            if (CurrWindTime == 0)
            {
                WindTime = 0;
                SetWindToward(true);
            }
        }
    }

    private void SetWindToward(
        bool isRight,
        bool synClient = false)
    {
        if (synClient || !gameManager.isClient)
        {
            windTowardRight = isRight;

            for (int i = 0;
                i < mapManager.mapList.Count;
                i++)
            {
                mapManager.mapList[i]
                    .WindDirectionReset();
            }

            if (gameManager.isServer)
            {
                WeatherChange weatherChange =
                    new WeatherChange();

                weatherChange.type =
                    WeatherType.Wind;

                weatherChange.WeSc =
                    windScale;

                weatherChange.isTud =
                    WindTowardRight;

                socketServer.SendWeatherCmd(
                    weatherChange
                );
            }
        }
    }

    private void WindScaleReSet()
    {
        timetable.UpdateWeather();

        for (int i = 0;
            i < mapManager.mapList.Count;
            i++)
        {
            mapManager.mapList[i]
                .WindScaleReset();
        }

        audioManager.SetWindVolume(
            WindScale > 0,
            needFade: true
        );
    }

    public void AffectWind(
        bool facingLeft,
        int intensity)
    {
        if (gameManager.isClient ||
            WindScale == 0)
        {
            return;
        }

        intensity =
            (int)(
                intensity *
                (2f -
                 WindScale * 0.2f)
            );

        if (facingLeft)
        {
            int num =
                CurrWindTime -
                intensity;

            if (num < 0 &&
                CurrWindTime > 0)
            {
                CurrWindTime =
                    num -
                    WindTime;

                SetWindToward(false);
            }
            else
            {
                CurrWindTime = num;
            }
        }
        else
        {
            int num =
                CurrWindTime +
                intensity;

            if (num >= 0 &&
                CurrWindTime < 0)
            {
                CurrWindTime = num;

                SetWindToward(true);

                WindTime = 0;
            }
            else
            {
                CurrWindTime = num;
            }
        }

        if (CurrWindTime > 50)
        {
            CurrWindTime = 50;
        }
        else if (CurrWindTime < -50)
        {
            CurrWindTime = -50;
        }
    }

    private IEnumerator WaitLightning()
    {
        int last = -10;

        canLightning = false;

        int waitTime =
            45 -
            mapManager.mapList.Count * 10;

        WaitForSeconds wait =
            new WaitForSeconds(1f);

        for (int i = 0;
            i < waitTime;
            i++)
        {
            yield return wait;

            if (Random.Range(0, 7) > 5 &&
                i - last > 3)
            {
                last = i;

                if (Random.Range(0, 2) == 0)
                {
                    audioManager.PlayEFAudio(
                        gameManager.AudioConf.Thunder3,
                        transform.position,
                        isAll: true
                    );
                }
                else
                {
                    audioManager.PlayEFAudio(
                        gameManager.AudioConf.Thunder4,
                        transform.position,
                        isAll: true
                    );
                }

                StartCoroutine(
                    NormalLightningLight()
                );
            }
        }

        canLightning = true;
    }

    private IEnumerator NormalLightningLight()
    {
        NormalLightning.SetActive(true);

        yield return new WaitForSeconds(0.8f);

        NormalLightning.SetActive(false);

        yield return new WaitForSeconds(0.6f);

        NormalLightning.SetActive(true);

        yield return new WaitForSeconds(0.6f);

        NormalLightning.SetActive(false);
    }

    public int GetWeatherTempt()
    {
        if (RainScale > 0)
        {
            return -RainScale;
        }

        if (SnowScale > 0)
        {
            return -SnowScale - 10;
        }

        if (HailScale > 0)
        {
            return -HailScale / 2;
        }

        return 0;
    }

    public void SetRainScale(
        int scale,
        bool isDirect,
        bool synClient = false)
    {
        if (scale == RainScale ||
            (!synClient &&
             gameManager.isClient) ||
            !lvManager.InGame)
        {
            return;
        }

        if (SnowScale > 0)
        {
            SetSnowScale(
                0,
                isDirect: false
            );
        }

        if (HailScale > 0)
        {
            SetHailScale(
                0,
                isDirect: false
            );
        }

        RainScale = scale;

        if (RainScale == 0 &&
            isDirect)
        {
            RainParticle.gameObject
                .SetActive(false);
        }
        else
        {
            RainParticle.gameObject
                .SetActive(true);
        }

        audioManager.SetRainVolume(
            scale > 0,
            !isDirect
        );

        if (isDirect)
        {
            RainEmissionModule.rateOverTime =
                new ParticleSystem.MinMaxCurve(
                    scale * 30
                );

            if (!isRainFog)
            {
                if (scale > 6)
                {
                    ScrollFog.enabled = true;

                    ScrollFog.material.SetFloat(
                        "_FogIntensity",
                        (scale - 6) *
                        0.05f -
                        0.1f
                    );
                }
                else
                {
                    ScrollFog.enabled = false;
                }
            }
        }
        else
        {
            if (RainChangeCoroutine != null)
            {
                StopCoroutine(
                    RainChangeCoroutine
                );
            }

            RainChangeCoroutine =
                StartCoroutine(
                    ChangeRain(scale)
                );
        }

        WindScaleReSet();

        gobalLight.RainScaleChange(
            !isDirect
        );

        if (gameManager.isServer)
        {
            WeatherChange weatherChange =
                new WeatherChange();

            weatherChange.type =
                WeatherType.Rain;

            weatherChange.WeSc =
                RainScale;

            weatherChange.isTud =
                IsThunder;

            weatherChange.isNoFade =
                isDirect;

            socketServer.SendWeatherCmd(
                weatherChange
            );
        }
    }

    private IEnumerator ChangeRain(int scale)
    {
        int direction = 1;

        float target =
            scale * 30f;

        if (target <
            RainEmissionModule
                .rateOverTime
                .constant)
        {
            direction = -1;
        }

        if (scale > 6 &&
            !isRainFog)
        {
            if (!ScrollFog.enabled)
            {
                ScrollFog.material.SetFloat(
                    "_FogIntensity",
                    -0.2f
                );
            }

            ScrollFog.enabled = true;
        }
        else
        {
            ScrollFog.enabled = false;
        }

        WaitForSeconds wait =
            new WaitForSeconds(0.05f);

        while (
            RainEmissionModule
                .rateOverTime
                .constant != target)
        {
            yield return wait;

            float current =
                RainEmissionModule
                    .rateOverTime
                    .constant;

            float next =
                current + direction;

            if (direction > 0 &&
                next > target)
            {
                next = target;
            }
            else if (
                direction < 0 &&
                next < target)
            {
                next = target;
            }

            RainEmissionModule.rateOverTime =
                new ParticleSystem.MinMaxCurve(
                    next
                );
        }

        if (!isRainFog)
        {
            float fog =
                ScrollFog.material.GetFloat(
                    "_FogIntensity"
                );

            float goal =
                (scale - 6) *
                0.05f -
                0.1f;

            WaitForSeconds fogWait =
                new WaitForSeconds(0.1f);

            while (Mathf.Abs(fog - goal) > 0.001f)
            {
                yield return fogWait;

                fog =
                    Mathf.MoveTowards(
                        fog,
                        goal,
                        0.01f
                    );

                ScrollFog.material.SetFloat(
                    "_FogIntensity",
                    fog
                );
            }
        }

        RainChangeCoroutine = null;
    }

    public void SetSnowScale(
        int scale,
        bool isDirect,
        bool synClient = false)
    {
        if (!synClient &&
            gameManager.isClient)
        {
            return;
        }

        SnowScale = scale;

        if (RainScale > 0)
        {
            SetRainScale(
                0,
                isDirect: false
            );
        }

        if (HailScale > 0)
        {
            SetHailScale(
                0,
                isDirect: false
            );
        }

        if (SnowScale == 0 &&
            isDirect)
        {
            SnowParticle.gameObject
                .SetActive(false);
        }
        else
        {
            SnowParticle.gameObject
                .SetActive(true);
        }

        if (isDirect)
        {
            SnowEmissionModule.rateOverTime =
                new ParticleSystem.MinMaxCurve(
                    (scale - 1) * 30
                );

            for (int i = 0;
                i < mapManager.mapList.Count;
                i++)
            {
                mapManager.mapList[i]
                    .SnowInit();
            }
        }
        else
        {
            if (SnowChangeCoroutine != null)
            {
                StopCoroutine(
                    SnowChangeCoroutine
                );
            }

            SnowChangeCoroutine =
                StartCoroutine(
                    ChangeSnow(
                        (snowScale - 1) * 30
                    )
                );
        }

        if (gameManager.isServer)
        {
            WeatherChange weatherChange =
                new WeatherChange();

            weatherChange.type =
                WeatherType.Snow;

            weatherChange.WeSc =
                SnowScale;

            weatherChange.isTud =
                IsThunder;

            weatherChange.isNoFade =
                isDirect;

            socketServer.SendWeatherCmd(
                weatherChange
            );
        }
    }

    private IEnumerator ChangeSnow(int scale)
    {
        int direction = 1;

        float target = scale;

        if (target <
            SnowEmissionModule
                .rateOverTime
                .constant)
        {
            direction = -1;
        }

        WaitForSeconds wait =
            new WaitForSeconds(0.05f);

        while (
            SnowEmissionModule
                .rateOverTime
                .constant != target)
        {
            yield return wait;

            float current =
                SnowEmissionModule
                    .rateOverTime
                    .constant;

            float next =
                current + direction;

            if (direction > 0 &&
                next > target)
            {
                next = target;
            }
            else if (
                direction < 0 &&
                next < target)
            {
                next = target;
            }

            SnowEmissionModule.rateOverTime =
                new ParticleSystem.MinMaxCurve(
                    next
                );
        }

        SnowChangeCoroutine = null;
    }

    public void SetHailScale(
        int scale,
        bool isDirect,
        bool synClient = false)
    {
        if (!synClient &&
            gameManager.isClient)
        {
            return;
        }

        if (SnowScale > 0)
        {
            SetSnowScale(
                0,
                isDirect: false
            );
        }

        if (RainScale > 0)
        {
            SetRainScale(
                0,
                isDirect: false
            );
        }

        HailScale = scale;

        float num =
            HailScale * 30 + 100;

        if (scale == 0)
        {
            num = 0f;
        }

        if (HailScale == 0 &&
            isDirect)
        {
            HailParticle.gameObject
                .SetActive(false);
        }
        else
        {
            HailParticle.gameObject
                .SetActive(true);
        }

        audioManager.SetHailVolume(
            scale > 0,
            !isDirect
        );

        ParticleSystem.MainModule main =
            HailParticle.main;

        main.startSize =
            new ParticleSystem.MinMaxCurve(
                0.1f,
                0.1f +
                0.03f *
                HailScale
            );

        RainEmissionModule.rateOverTime =
            new ParticleSystem.MinMaxCurve(
                scale * 20
            );

        plantManager.AllCheckWeather();

        if (isDirect)
        {
            HailEmissionModule.rateOverTime =
                new ParticleSystem.MinMaxCurve(
                    num
                );

            for (int i = 0;
                i < mapManager.mapList.Count;
                i++)
            {
                mapManager.mapList[i]
                    .SnowInit();
            }
        }
        else
        {
            if (HailChangeCoroutine != null)
            {
                StopCoroutine(
                    HailChangeCoroutine
                );
            }

            HailChangeCoroutine =
                StartCoroutine(
                    ChangeHail(num)
                );
        }

        gobalLight.HailScaleChange(
            !isDirect
        );

        if (gameManager.isServer)
        {
            WeatherChange weatherChange =
                new WeatherChange();

            weatherChange.type =
                WeatherType.Hail;

            weatherChange.WeSc =
                HailScale;

            weatherChange.isNoFade =
                isDirect;

            socketServer.SendWeatherCmd(
                weatherChange
            );
        }
    }

    private IEnumerator ChangeHail(float scale)
    {
        int direction = 5;

        if (scale <
            HailEmissionModule
                .rateOverTime
                .constant)
        {
            direction = -5;
        }

        WaitForSeconds wait =
            new WaitForSeconds(0.05f);

        while (
            HailEmissionModule
                .rateOverTime
                .constant != scale)
        {
            yield return wait;

            float current =
                HailEmissionModule
                    .rateOverTime
                    .constant;

            float next =
                current + direction;

            if (direction > 0 &&
                next > scale)
            {
                next = scale;
            }
            else if (
                direction < 0 &&
                next < scale)
            {
                next = scale;
            }

            HailEmissionModule.rateOverTime =
                new ParticleSystem.MinMaxCurve(
                    next
                );
        }

        HailChangeCoroutine = null;
    }

    public void DirectSetTime(
        int time,
        bool synClient = false)
    {
        if (!gameManager.isClient ||
            synClient)
        {
            bool dayLightCycle =
                DayLightCycle;

            DayLightCycle = true;

            int num = time;

            if (time < 0)
            {
                num = -time;
            }

            if (time >= 1440)
            {
                num = time % 1440;
            }

            Time = num;

            for (int i = 0;
                i < mapManager.mapList.Count;
                i++)
            {
                mapManager.mapList[i]
                    .TimeInitMap(Time);

                mapManager.mapList[i]
                    .BaseTimeChange(Time);
            }

            gobalLight.InitIntensity();

            DayLightCycle =
                dayLightCycle;

            if (gameManager.isServer)
            {
                TimeCmd timeCmd =
                    new TimeCmd();

                timeCmd.time =
                    10000 + num;

                socketServer.SendTimeCmd(
                    timeCmd
                );
            }
        }
    }

    public void ClientLightningThis(
        Grid grid)
    {
        lightning.gameObject
            .SetActive(true);

        lightning.LightGrid(
            grid,
            null
        );
    }

    public void ClientSynWeather(
        WeatherChange bag)
    {
        if (bag.type ==
            WeatherType.Rain)
        {
            SetRainScale(
                bag.WeSc,
                bag.isNoFade,
                synClient: true
            );
        }
        else if (
            bag.type ==
            WeatherType.Snow)
        {
            SetSnowScale(
                bag.WeSc,
                bag.isNoFade,
                synClient: true
            );
        }
        else if (
            bag.type ==
            WeatherType.Hail)
        {
            SetHailScale(
                bag.WeSc,
                bag.isNoFade,
                synClient: true
            );
        }
        else if (
            bag.type ==
            WeatherType.Wind)
        {
            WindScale = bag.WeSc;

            SetWindToward(
                bag.isTud,
                synClient: true
            );
        }

        IsThunder = bag.isTud;
    }

    public void ClearAllWeather(
        bool isDirect,
        bool synClient = false)
    {
        IsThunder = false;

        SetRainScale(
            0,
            isDirect,
            synClient
        );

        SetSnowScale(
            0,
            isDirect,
            synClient
        );

        SetHailScale(
            0,
            isDirect,
            synClient
        );

        WindScale = 0;
    }

    public void PlayWeather(
        FutureWeather weather)
    {
        if (!gameManager.isClient)
        {
            bool isDirect =
                weather.appearTime <= 0;

            switch (weather.type)
            {
                case WeatherType.Clear:
                    ClearAllWeather(
                        isDirect
                    );
                    break;

                case WeatherType.Rain:
                    SetRainScale(
                        weather.scale,
                        isDirect
                    );
                    break;

                case WeatherType.Thunder:
                    IsThunder = true;

                    SetRainScale(
                        10,
                        isDirect
                    );
                    break;

                case WeatherType.Snow:
                    SetSnowScale(
                        weather.scale,
                        isDirect
                    );
                    break;

                case WeatherType.Wind:
                    WindScale =
                        weather.scale;
                    break;

                case WeatherType.Hail:
                    SetHailScale(
                        weather.scale,
                        isDirect
                    );
                    break;
            }
        }
    }

    public int GetShadowNum(
        Grid grid)
    {
        if (grid == null)
        {
            return 0;
        }

        int num = 1;

        if (!GetIsDay())
        {
            num += 2;
        }

        if (RainScale > 6)
        {
            num++;
        }

        if (grid.isShadow)
        {
            num += 2;
        }

        return num;
    }

    public void ResetAll()
    {
        while (sunList.Count > 0)
        {
            sunList[0].DestroySun();
        }

        NormalLightning.SetActive(false);

        canLightning = true;

        sunList.Clear();

        ClearAllWeather(
            isDirect: true,
            synClient: true
        );

        StopAllCoroutines();

        TimeCoroutine = null;
        RainChangeCoroutine = null;
        SnowChangeCoroutine = null;
        HailChangeCoroutine = null;

        gobalLight.ResetAll();
    }

    public void RemoveSun(Sun sun)
    {
        sunList.Remove(sun);
    }

    public Sun GetARandomSun(
        MapBase map)
    {
        Sun result = null;

        int validCount = 0;

        for (int i = 0;
            i < sunList.Count;
            i++)
        {
            Sun sun = sunList[i];

            if (!sun.CanGet)
            {
                continue;
            }

            if (mapManager.GetCurrMap(
                    sun.transform.position) != map)
            {
                continue;
            }

            validCount++;

            if (Random.Range(
                    0,
                    validCount) == 0)
            {
                result = sun;
            }
        }

        return result;
    }

    public void CollectAllSun()
    {
        if (!gameManager.isClient)
        {
            for (int i = 0;
                i < sunList.Count;
                i++)
            {
                sunList[i].CollectSun();
            }
        }
    }

    public void OnlineCollectSun(
        ClickedSun sun)
    {
        for (int i = 0;
            i < sunList.Count;
            i++)
        {
            if (sunList[i].OnlineSunId ==
                sun.OnlineSunId)
            {
                sunList[i].OnlineSyn(sun);
                break;
            }
        }
    }

    public void ClientSpawnSun(
        SunSpawn spawn)
    {
        if (PoolManager.Instance == null)
        {
            Debug.LogError(
                "SkyManager: PoolManager no est asignado."
            );
            return;
        }

        if (gameManager == null)
        {
            Debug.LogError(
                "SkyManager: GameManager no est asignado."
            );
            return;
        }

        if (gameManager.GameConf == null)
        {
            Debug.LogError(
                "SkyManager: GameConf es NULL."
            );
            return;
        }

        if (gameManager.GameConf.Sun == null)
        {
            Debug.LogError(
                "SkyManager: GameConf.Sun es NULL."
            );
            return;
        }

        GameObject sunObject =
            PoolManager.Instance.GetObj(
                gameManager.GameConf.Sun
            );

        if (sunObject == null)
        {
            Debug.LogError(
                "SkyManager: PoolManager.GetObj() devolvi NULL para GameConf.Sun."
            );
            return;
        }

        Sun component =
            sunObject.GetComponent<Sun>();

        if (component == null)
        {
            Debug.LogError(
                "SkyManager: el prefab GameConf.Sun no tiene componente Sun."
            );
            return;
        }

        component.transform.SetParent(
            transform
        );

        if (spawn.isSkySun)
        {
            component.InitForSky(
                spawn.Afloat,
                spawn.Pos,
                spawn.type
            );
        }
        else
        {
            if (spectatorList == null)
            {
                Debug.LogError(
                    "SkyManager: SpectatorList no est asignado."
                );
                return;
            }

            if (lv == null)
            {
                Debug.LogError(
                    "SkyManager: LV no est asignado."
                );
                return;
            }

            if (pvpSelector == null)
            {
                Debug.LogError(
                    "SkyManager: PvPSelector no est asignado."
                );
                return;
            }

            if (!spectatorList.LocalIsSpectator &&
                lv.CurrLVType == LVType.PvP &&
                !pvpSelector.IsSameTeam(
                    gameManager.HostName
                ))
            {
                spawn.Pos =
                    new Vector2(
                        -spawn.Pos.x,
                        spawn.Pos.y
                    );
            }

            component.InitForPlant(
                spawn.Pos,
                spawn.Afloat,
                spawn.type,
                spawn.Player
            );
        }

        component.OnlineSunId =
            spawn.OnlineId;

        sunList.Add(component);
    }

    public void CreateSkySun(
        Vector2 SpawnPos,
        float DownY,
        SunType type)
    {
        if (gameManager == null)
        {
            Debug.LogError(
                "SkyManager: GameManager no est asignado."
            );
            return;
        }

        if (gameManager.isClient)
        {
            return;
        }

        if (lv == null)
        {
            Debug.LogError(
                "SkyManager: LV no est asignado."
            );
            return;
        }

        if (lv.CurrLVType != LVType.Normal)
        {
            return;
        }

        if (PoolManager.Instance == null)
        {
            Debug.LogError(
                "SkyManager: PoolManager no est asignado."
            );
            return;
        }

        if (gameManager.GameConf == null)
        {
            Debug.LogError(
                "SkyManager: GameConf es NULL."
            );
            return;
        }

        if (gameManager.GameConf.Sun == null)
        {
            Debug.LogError(
                "SkyManager: GameConf.Sun es NULL."
            );
            return;
        }

        GameObject sunObject =
            PoolManager.Instance.GetObj(
                gameManager.GameConf.Sun
            );

        if (sunObject == null)
        {
            Debug.LogError(
                "SkyManager: PoolManager.GetObj() devolvi NULL para GameConf.Sun."
            );
            return;
        }

        Sun component =
            sunObject.GetComponent<Sun>();

        if (component == null)
        {
            Debug.LogError(
                "SkyManager: el prefab GameConf.Sun no tiene componente Sun."
            );
            return;
        }

        component.transform.SetParent(
            transform
        );

        component.InitForSky(
            DownY,
            SpawnPos,
            type
        );

        sunList.Add(component);

        if (gameManager.isServer)
        {
            if (socketServer == null)
            {
                Debug.LogError(
                    "SkyManager: SocketServer no est asignado."
                );
                return;
            }

            SunSpawn sunSpawn =
                new SunSpawn();

            sunSpawn.OnlineId =
                OnlineSunId;

            sunSpawn.Player = null;

            component.OnlineSunId =
                sunSpawn.OnlineId;

            sunSpawn.Pos =
                SpawnPos;

            sunSpawn.type =
                type;

            sunSpawn.isSkySun =
                true;

            sunSpawn.Afloat =
                DownY;

            socketServer.SpawnSun(
                sunSpawn
            );
        }
    }

    public void CreatePlantSun(
        Vector2 pos,
        float SunNum,
        SunType type,
        string Player)
    {
        if (gameManager == null)
        {
            Debug.LogError(
                "SkyManager: GameManager no est asignado."
            );
            return;
        }

        if (gameManager.isClient)
        {
            return;
        }

        if (PoolManager.Instance == null)
        {
            Debug.LogError(
                "SkyManager: PoolManager no est asignado."
            );
            return;
        }

        if (gameManager.GameConf == null)
        {
            Debug.LogError(
                "SkyManager: GameConf es NULL."
            );
            return;
        }

        if (gameManager.GameConf.Sun == null)
        {
            Debug.LogError(
                "SkyManager: GameConf.Sun es NULL."
            );
            return;
        }

        GameObject sunObject =
            PoolManager.Instance.GetObj(
                gameManager.GameConf.Sun
            );

        if (sunObject == null)
        {
            Debug.LogError(
                "SkyManager: PoolManager.GetObj() devolvi NULL para GameConf.Sun."
            );
            return;
        }

        Sun component =
            sunObject.GetComponent<Sun>();

        if (component == null)
        {
            Debug.LogError(
                "SkyManager: el prefab GameConf.Sun no tiene componente Sun."
            );
            return;
        }

        component.transform.SetParent(
            transform
        );

        component.InitForPlant(
            pos,
            SunNum,
            type,
            Player
        );

        sunList.Add(component);

        if (gameManager.isServer)
        {
            if (socketServer == null)
            {
                Debug.LogError(
                    "SkyManager: SocketServer no est asignado."
                );
                return;
            }

            SunSpawn sunSpawn =
                new SunSpawn();

            sunSpawn.OnlineId =
                OnlineSunId;

            sunSpawn.Player =
                Player;

            component.OnlineSunId =
                sunSpawn.OnlineId;

            sunSpawn.Pos =
                pos;

            sunSpawn.type =
                type;

            sunSpawn.isSkySun =
                false;

            sunSpawn.Afloat =
                SunNum;

            socketServer.SpawnSun(
                sunSpawn
            );
        }
    }

    public bool GetIsDay()
    {
        return GetIsDay(Time);
    }

    public bool GetIsDay(int time)
    {
        return time < 1110 &&
            time > 360;
    }
}