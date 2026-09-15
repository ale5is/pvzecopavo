using System;
using System.Collections.Generic;
using SaveClass;
using UnityEngine;
using UnityEngine.UI;

public class StatsManager : MonoBehaviour
{
    public static StatsManager Instance;

    public GameObject StatsDisPreFab;

    public Transform Content;

    public GameObject ScrollView;

    private readonly List<Text> LoadedObj = new List<Text>();

    private readonly List<StatsEnum> StatsList = new List<StatsEnum>
    {
        StatsEnum.GameTime,
        StatsEnum.GameOpenNum,
        StatsEnum.EarnMoney,
        StatsEnum.SpendMoney,
        StatsEnum.ClickMoney,
        StatsEnum.WinNum,
        StatsEnum.FailNum,
        StatsEnum.ReStartNum,
        StatsEnum.OverAcvNum,
        StatsEnum.ShovelOffNum,
        StatsEnum.MowerStartNum,
        StatsEnum.VaseBreakNum,
        StatsEnum.ClickSunNum,
        StatsEnum.NutFirstAidNum,
        StatsEnum.AbsorbEquipNum,
        StatsEnum.ChangeMapNum
    };

    private StatsAndAcvSave StatsSave
    {
        get
        {
            return GameManager.Instance.StatsAcvSave;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (ScrollView != null)
            ScrollView.SetActive(false);
    }

    private void EnsureStatsCapacity()
    {
        if (GameManager.Instance == null)
            return;

        if (StatsSave == null)
            return;

        int requiredLength = Enum.GetValues(typeof(StatsEnum)).Length;

        if (StatsSave.OtherStats == null)
            StatsSave.OtherStats = new List<int>();

        while (StatsSave.OtherStats.Count < requiredLength)
        {
            StatsSave.OtherStats.Add(0);
        }
    }

    public void LoadStatsSave()
    {
        EnsureStatsCapacity();
    }

    public void AddStatsNum(StatsEnum stats, int addNum = 1)
    {
        EnsureStatsCapacity();

        if (StatsSave == null ||
            StatsSave.OtherStats == null)
        {
            return;
        }

        int index = (int)stats;

        if (index < 0 ||
            index >= StatsSave.OtherStats.Count)
        {
            return;
        }

        int num = StatsSave.OtherStats[index];

        StatsSave.OtherStats[index] += addNum;

        if (stats == StatsEnum.EarnMoney &&
            StatsSave.OtherStats[index] >= 1000000)
        {
            if (AcvmentManager.Instance != null)
            {
                AcvmentManager.Instance.GetAchievement(
                    Acvname.Money100w
                );
            }
        }

        if (stats == StatsEnum.ClickMoney &&
            StatsSave.OtherStats[index] >= 100 &&
            num < 100)
        {
            if (AcvmentManager.Instance != null)
            {
                AcvmentManager.Instance.GetAchievement(
                    Acvname.ClickMoney100
                );
            }
        }
    }

    public void ClearStatsNum(StatsEnum stats)
    {
        EnsureStatsCapacity();

        if (StatsSave == null ||
            StatsSave.OtherStats == null)
        {
            return;
        }

        int index = (int)stats;

        if (index < 0 ||
            index >= StatsSave.OtherStats.Count)
        {
            return;
        }

        StatsSave.OtherStats[index] = 0;
    }

    public void LoadStats()
    {
        EnsureStatsCapacity();

        if (ScrollView == null)
            return;

        ScrollView.SetActive(true);

        ScrollRect scrollRect =
            ScrollView.GetComponent<ScrollRect>();

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;

        if (LoadedObj.Count == 0)
        {
            for (int i = 0; i < StatsList.Count; i++)
            {
                if (StatsDisPreFab == null ||
                    Content == null)
                {
                    continue;
                }

                GameObject gameObject =
                    Instantiate(StatsDisPreFab);

                if (gameObject == null)
                    continue;

                Transform titleTransform =
                    gameObject.transform.Find("TitleText");

                Transform numTransform =
                    gameObject.transform.Find("NumText");

                if (titleTransform == null ||
                    numTransform == null)
                {
                    Destroy(gameObject);
                    continue;
                }

                Text titleText =
                    titleTransform.GetComponent<Text>();

                Text numText =
                    numTransform.GetComponent<Text>();

                if (titleText == null ||
                    numText == null)
                {
                    Destroy(gameObject);
                    continue;
                }

                titleText.text =
                    GetStatsName(StatsList[i]);

                if (i % 2 == 1)
                {
                    Image image =
                        gameObject.GetComponent<Image>();

                    if (image != null)
                    {
                        image.color =
                            new Color(0f, 0f, 0f, 0.1f);
                    }
                }

                LoadedObj.Add(numText);

                gameObject.transform.SetParent(
                    Content,
                    false
                );

                gameObject.transform.localScale =
                    new Vector3(1.4f, 1.4f);
            }

            RectTransform contentRect =
                Content != null
                    ? Content.GetComponent<RectTransform>()
                    : null;

            if (contentRect != null)
            {
                contentRect.sizeDelta =
                    new Vector2(
                        0f,
                        56f * StatsList.Count
                    );
            }
        }

        int count =
            Mathf.Min(
                StatsList.Count,
                LoadedObj.Count
            );

        for (int j = 0; j < count; j++)
        {
            int index = (int)StatsList[j];

            if (index < 0 ||
                index >= StatsSave.OtherStats.Count)
            {
                LoadedObj[j].text = "0";
                continue;
            }

            if (StatsList[j] == StatsEnum.GameTime)
            {
                int num =
                    StatsSave.OtherStats[index];

                int days =
                    num / 86400;

                int remainder =
                    num % 86400;

                int hours =
                    remainder / 3600;

                int minutes =
                    remainder % 3600 / 60;

                string text = "";

                if (days > 0)
                    text += days + "d";

                if (hours > 0)
                    text += hours + "h";

                text += minutes + "m";

                LoadedObj[j].text = text;
            }
            else
            {
                LoadedObj[j].text =
                    StatsSave.OtherStats[index].ToString();
            }
        }

        RectTransform finalContentRect =
            Content != null
                ? Content.GetComponent<RectTransform>()
                : null;

        if (finalContentRect != null)
        {
            finalContentRect.localPosition =
                new Vector2(0f, 0f);
        }
    }

    public void CloseViwer()
    {
        if (ScrollView != null)
            ScrollView.SetActive(false);
    }

    private string GetStatsName(StatsEnum stats)
    {
        if (stats == StatsEnum.GameTime)
            return "Tiempo de juego";

        if (stats == StatsEnum.GameOpenNum)
            return "Veces que se abrió el juego";

        if (stats == StatsEnum.EarnMoney)
            return "Dinero ganado";

        if (stats == StatsEnum.SpendMoney)
            return "Dinero gastado";

        if (stats == StatsEnum.ClickMoney)
            return "Dinero recogido consecutivamente";

        if (stats == StatsEnum.EatGraveNum)
            return "Lápidas destruidas";

        if (stats == StatsEnum.WinNum)
            return "Niveles ganados";

        if (stats == StatsEnum.FailNum)
            return "Niveles perdidos";

        if (stats == StatsEnum.ReStartNum)
            return "Reinicios";

        if (stats == StatsEnum.WakeUpNum)
            return "Plantas despertadas";

        if (stats == StatsEnum.OverAcvNum)
            return "Logros completados";

        if (stats == StatsEnum.CheatSquashNum)
            return "Calabazas engañadas";

        if (stats == StatsEnum.ShovelOffNum)
            return "Usos de la pala";

        if (stats == StatsEnum.MowerStartNum)
            return "Cortadoras de césped activadas";

        if (stats == StatsEnum.VaseBreakNum)
            return "Jarrones destruidos";

        if (stats == StatsEnum.ClickSunNum)
            return "Soles recogidos";

        if (stats == StatsEnum.NutFirstAidNum)
            return "Usos de reparación de nueces";

        if (stats == StatsEnum.AbsorbEquipNum)
            return "Equipamientos atraídos";

        if (stats == StatsEnum.ChangeMapNum)
            return "Cambios de mapa";

        return "???";
    }
}