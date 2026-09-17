using System.Collections;
using System.Collections.Generic;
using SaveClass;
using SocketSave;
using UnityEngine;

public abstract class MapBase : MonoBehaviour
{
    public Fog fog;

    public SpriteRenderer ChangeSprite;

    public Sprite Day;

    public Sprite Sunset;

    public Sprite Night;

    public Sprite GotoSprite;

    public int GraveStoneNum;

    public int GraveStoneLine;

    public Color puddleColor = new Color(1f, 1f, 1f);

    public List<GameObject> MapLights = new List<GameObject>();

    protected List<LawnMower> mowers = new List<LawnMower>();

    protected List<int> SpawnZombieLine = new List<int>();

    private List<GridSelector> gridSelectors = new List<GridSelector>();

    private float currTempt;

    [SerializeField]
    private float targetTempt;

    private float manmadeTempt;

    private float natureTempt;

    [SerializeField]
    private float fadeTempt;

    [SerializeField]
    private float entityTempt;

    private int mapTemptDiff = 15;

    protected List<CustomTile> tiles = new List<CustomTile>();

    // =========================================================
    // REFERENCIAS ASIGNADAS DESDE EL INSPECTOR
    // =========================================================

    [Header("Managers")]
    [SerializeField] private Timetable timetable;
    [SerializeField] private SkyManager skyManager;
    [SerializeField] private LV lv;
    [SerializeField] private GobalLight gobalLight;
    [SerializeField] private SeedBank seedBank;
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private GameManager gameManager;

    [Header("Prefabs")]
    [SerializeField] private GridSelector gridSelectorPrefab;

    // =========================================================
    // PROPIEDADES DEL MAPA
    // =========================================================

    public abstract Vector2Int MapGridNum { get; }

    public abstract Vector2 MapHalfLengthWidth { get; }

    public abstract List<Grid> GridList { get; }

    public virtual float EndLine { get; } = -7.6f;

    public virtual bool IsWaterShow { get; }

    public virtual bool IsFacingLeft { get; }

    public virtual Color32 SplashColor { get; } = Color.white;

    protected virtual int mapTempt { get; } = 10;

    protected virtual List<int> SpawnLimitLine { get; }

    // =========================================================
    // TEMPERATURA
    // =========================================================

    public float CurrTempt
    {
        get
        {
            return currTempt;
        }
        private set
        {
            currTempt = Mathf.Clamp(value, -50f, 50f);

            if (timetable != null)
            {
                timetable.UpdateTempt(this);
            }
        }
    }

    public float FadeTempt
    {
        get
        {
            return fadeTempt;
        }
        set
        {
            if (fadeTempt == value)
            {
                return;
            }

            fadeTempt = Mathf.Clamp(value, -30f, 30f);

            if (Mathf.Abs(fadeTempt) < 0.2f)
            {
                fadeTempt = 0f;
            }

            RefreshManmadeTemperature();
        }
    }

    public float EntityTempt
    {
        get
        {
            return entityTempt;
        }
        set
        {
            if (entityTempt == value)
            {
                return;
            }

            entityTempt = Mathf.Clamp(value, -30f, 30f);

            RefreshManmadeTemperature();
        }
    }

    // =========================================================
    // INICIALIZACION
    // =========================================================

    public void BaseInitMap()
    {
        InitMap();
        RefreshNatureTemperature();
        RefreshManmadeTemperature();

        CurrTempt = targetTempt;

        StartCoroutine(GoTargetTempt());
    }

    public void BaseTimeChange(int time)
    {
        TimeChange(time);
        RefreshNatureTemperature();
    }

    protected abstract void InitMap();

    public abstract void TimeInitMap(int time);

    protected abstract void TimeChange(int time);

    public virtual void ZombieDeadEvent(ZombieBase zombie)
    {
    }

    public virtual void SpawnMower()
    {
    }

    // =========================================================
    // ZOMBIES
    // =========================================================

    public virtual List<Vector2> GetShowZombiePos(int PosNum)
    {
        List<Vector2> list = new List<Vector2>(PosNum);

        for (int i = 0; i < PosNum; i++)
        {
            list.Add(
                new Vector3(
                    Random.Range(9.5f, 13f),
                    Random.Range(-5f, 3.2f)
                ) + transform.position
            );
        }

        list.Sort((x, y) => -x.y.CompareTo(y.y));

        return list;
    }

    // =========================================================
    // LUCES
    // =========================================================

    public void LightOpenClose()
    {
        if (MapLights.Count == 0 || gobalLight == null)
        {
            return;
        }

        bool shouldOpen =
            !GetIsDay() ||
            gobalLight.gobalLight.intensity < 0.5f;

        bool isOpen = MapLights[0].activeSelf;

        if (shouldOpen != isOpen)
        {
            for (int i = 0; i < MapLights.Count; i++)
            {
                if (MapLights[i] != null)
                {
                    MapLights[i].SetActive(shouldOpen);
                }
            }
        }
    }

    public bool GetIsDay()
    {
        if (skyManager == null)
        {
            return false;
        }

        return
            skyManager.Time < 1140 &&
            skyManager.Time > 360;
    }

    // =========================================================
    // SINCRONIZACION
    // =========================================================

    public void SynMap(SynMap Syn)
    {
        if (Syn.SynCode[0] == 1)
        {
            int mowerIndex = Syn.SynCode[1];

            if (mowerIndex >= 0 && mowerIndex < mowers.Count)
            {
                mowers[mowerIndex].Launch(synClient: true);
            }
        }

        OwnerSynMap(Syn);
    }

    public virtual void SpawnAllGraveStone()
    {
    }

    public virtual void SnowInit()
    {
    }

    public virtual void WindScaleReset()
    {
    }

    public virtual void WindDirectionReset()
    {
    }

    public virtual void MapCloudClear(MapCloud cloud)
    {
    }

    protected virtual void OwnerSynMap(SynMap Syn)
    {
    }

    public virtual bool SpSpawnZombie(
        out Vector2 pos,
        out int SPCode)
    {
        pos = default;
        SPCode = 0;
        return false;
    }

    // =========================================================
    // LINEA ALEATORIA DE SPAWN
    // =========================================================

    public int GetRandomLine(int spCode)
    {
        int num = 0;
        int attempts = 0;

        do
        {
            attempts++;

            if (SpawnZombieLine.Count == 0)
            {
                for (int i = 0; i < MapGridNum.y; i++)
                {
                    SpawnZombieLine.Add(i);
                }

                SpawnZombieLine.Shuffle();
            }

            num = SpawnZombieLine[0];

            if (
                spCode != 1 ||
                SpawnLimitLine == null ||
                !SpawnLimitLine.Contains(num)
            )
            {
                break;
            }

            SpawnZombieLine.RemoveAt(0);
        }
        while (attempts <= 15);

        SpawnZombieLine.RemoveAt(0);

        return num;
    }

    public virtual void SetStripe(int lineX)
    {
    }

    // =========================================================
    // TEMPERATURA AUTOMATICA
    // =========================================================

    private IEnumerator GoTargetTempt()
    {
        WaitForSeconds wait = new WaitForSeconds(1f);

        while (true)
        {
            yield return wait;

            float difference = targetTempt - CurrTempt;

            if (difference != 0f)
            {
                if (Mathf.Abs(difference) < 0.2f)
                {
                    CurrTempt = targetTempt;
                }
                else
                {
                    if (difference > 0f && difference < 2f)
                    {
                        difference = 2f;
                    }
                    else if (difference < 0f && difference > -2f)
                    {
                        difference = -2f;
                    }

                    CurrTempt += difference / 20f;
                }
            }

            if (FadeTempt != 0f)
            {
                float fadeAmount;

                if (FadeTempt > 0f)
                {
                    fadeAmount =
                        2f -
                        (CurrTempt + 50f) / 50f;
                }
                else
                {
                    fadeAmount =
                        (CurrTempt + 50f) / 50f;
                }

                fadeAmount = Mathf.Clamp(
                    fadeAmount,
                    0.2f,
                    1.8f
                );

                if (FadeTempt > 0f)
                {
                    fadeAmount *= -1f;
                }

                FadeTempt += fadeAmount;
            }
        }
    }

    private float GetTimeTempt()
    {
        if (skyManager == null)
        {
            return 0f;
        }

        float time = skyManager.Time;
        float value = 0f;

        if (time >= 0f && time < 360f)
        {
            value =
                0.1f -
                time / 360f * 0.1f;
        }
        else if (time >= 360f && time < 600f)
        {
            value =
                (time - 360f) /
                240f *
                0.7f;
        }
        else if (time >= 600f && time < 840f)
        {
            value =
                (time - 600f) /
                240f *
                0.3f +
                0.7f;
        }
        else if (time >= 840f && time < 1200f)
        {
            value =
                1f -
                (time - 840f) /
                360f *
                0.6f;
        }
        else if (time >= 1200f && time < 1440f)
        {
            value =
                0.4f -
                (time - 1200f) /
                240f *
                0.3f;
        }

        return mapTemptDiff * value;
    }

    public void RefreshManmadeTemperature()
    {
        float fadeTemperature = CalculateTemperature(FadeTempt, true);
        float entityTemperature = CalculateTemperature(EntityTempt, false);

        manmadeTempt =
            fadeTemperature +
            entityTemperature;

        targetTempt =
            manmadeTempt +
            natureTempt;

        if (targetTempt > 30f)
        {
            float difference = natureTempt - 30f;

            if (difference < 0f)
            {
                float adjustedManmade =
                    manmadeTempt +
                    difference;

                targetTempt =
                    adjustedManmade * 0.6f +
                    natureTempt;
            }
        }
        else if (targetTempt < -30f)
        {
            float difference =
                natureTempt + 30f;

            if (difference > 0f)
            {
                float adjustedManmade =
                    manmadeTempt +
                    difference;

                targetTempt =
                    adjustedManmade * 0.6f +
                    natureTempt;
            }
        }
    }

    private float CalculateTemperature(
        float temperature,
        bool fade)
    {
        float abs = Mathf.Abs(temperature);
        float tens = abs / 10f;
        float remainder = abs % 10f;

        float value;

        if (tens >= 3f)
        {
            value = 17f;
        }
        else if (tens >= 2f)
        {
            value =
                15f +
                remainder / 5f;
        }
        else if (tens >= 1f)
        {
            value =
                10f +
                (fade
                    ? remainder / 2f
                    : remainder);
        }
        else
        {
            value =
                fade
                    ? remainder
                    : remainder / 2f;
        }

        return temperature < 0f
            ? -value
            : value;
    }

    public void RefreshNatureTemperature()
    {
        if (skyManager == null || lv == null)
        {
            return;
        }

        natureTempt =
            skyManager.GetWeatherTempt() +
            GetTimeTempt() +
            mapTempt +
            lv.LvTemperature;

        targetTempt =
            manmadeTempt +
            natureTempt;
    }

    // =========================================================
    // CONDICION DE SOL
    // =========================================================

    protected bool SkySunCondition()
    {
        if (
            seedBank != null &&
            playerManager != null &&
            seedBank.isNoCD &&
            playerManager.SunInfinite
        )
        {
            return false;
        }

        if (
            lv != null &&
            lv.LvSpStates.Contains(LVSpState.LastStand)
        )
        {
            return false;
        }

        return true;
    }

    // =========================================================
    // GRID SELECTORS
    // =========================================================

    public void DisplayGridSelector()
    {
        if (gridSelectors.Count == 0)
        {
            if (gridSelectorPrefab == null)
            {
                return;
            }

            List<Grid> grids = GridList;

            for (int i = 0; i < grids.Count; i++)
            {
                GridSelector selector =
                    Instantiate(gridSelectorPrefab);

                selector.transform.position =
                    grids[i].Position;

                selector.transform.SetParent(
                    transform
                );

                selector.grid =
                    grids[i];

                gridSelectors.Add(selector);
            }
        }

        for (int i = 0; i < gridSelectors.Count; i++)
        {
            if (gridSelectors[i] != null)
            {
                gridSelectors[i].gameObject.SetActive(true);
            }
        }
    }

    public void HideGridSelector()
    {
        for (int i = 0; i < gridSelectors.Count; i++)
        {
            if (gridSelectors[i] != null)
            {
                gridSelectors[i].gameObject.SetActive(false);
            }
        }
    }

    public List<GridSelector> GetGridSelector()
    {
        return gridSelectors;
    }

    // =========================================================
    // CUSTOM MAP
    // =========================================================

    public virtual void CustomMapEditInit(
        int vertical,
        int horizontal,
        CustomMapSave save)
    {
    }

    public List<CustomTile> GetAroundTile(Vector2Int point)
    {
        List<CustomTile> result =
            new List<CustomTile>(8);

        for (int i = 0; i < tiles.Count; i++)
        {
            Vector2Int tilePoint =
                tiles[i].CurrGrid.Point;

            int x =
                Mathf.Abs(tilePoint.x - point.x);

            int y =
                Mathf.Abs(tilePoint.y - point.y);

            if (x <= 1 && y <= 1 && (x != 0 || y != 0))
            {
                result.Add(tiles[i]);

                if (result.Count >= 8)
                {
                    break;
                }
            }
        }

        return result;
    }

    public virtual int ChangeDecoration(int decorType)
    {
        return 0;
    }
}