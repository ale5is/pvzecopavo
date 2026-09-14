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

    private List<Text> LoadedObj = new List<Text>();

    private List<StatsEnum> StatsList = new List<StatsEnum>
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

    private StatsAndAcvSave StatsSave => GameManager.Instance.StatsAcvSave;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ScrollView.SetActive(false);
    }

    public void LoadStatsSave()
    {
        int length = Enum.GetValues(typeof(StatsEnum)).Length;

        while (StatsSave.OtherStats.Count < length)
        {
            StatsSave.OtherStats.Add(0);
        }
    }

    public void AddStatsNum(StatsEnum stats, int addNum = 1)
    {
        int num = StatsSave.OtherStats[(int)stats];

        StatsSave.OtherStats[(int)stats] += addNum;

        if (stats == StatsEnum.EarnMoney &&
            StatsSave.OtherStats[(int)stats] >= 1000000)
        {
            AcvmentManager.Instance.GetAchievement(
                Acvname.Money100w
            );
        }

        if (stats == StatsEnum.ClickMoney &&
            StatsSave.OtherStats[(int)stats] >= 100 &&
            num < 100)
        {
            AcvmentManager.Instance.GetAchievement(
                Acvname.ClickMoney100
            );
        }
    }

    public void ClearStatsNum(StatsEnum stats)
    {
        StatsSave.OtherStats[(int)stats] = 0;
    }

    public void LoadStats()
    {
        ScrollView.SetActive(true);

        ScrollView.transform
            .GetComponent<ScrollRect>()
            .verticalNormalizedPosition = 0f;

        if (LoadedObj.Count == 0)
        {
            for (int i = 0; i < StatsList.Count; i++)
            {
                GameObject gameObject =
                    UnityEngine.Object.Instantiate(StatsDisPreFab);

                Text component =
                    gameObject.transform
                        .Find("TitleText")
                        .GetComponent<Text>();

                Text component2 =
                    gameObject.transform
                        .Find("NumText")
                        .GetComponent<Text>();

                component.text =
                    GetStatsName(StatsList[i]);

                if (i % 2 == 1)
                {
                    gameObject.transform
                        .GetComponent<Image>()
                        .color = new Color(0f, 0f, 0f, 0.1f);
                }

                LoadedObj.Add(component2);

                gameObject.transform.SetParent(Content);

                gameObject.transform.localScale =
                    new Vector3(1.4f, 1.4f);
            }

            Content.GetComponent<RectTransform>().sizeDelta =
                new Vector2(
                    0f,
                    56 * StatsList.Count
                );
        }

        for (int j = 0; j < StatsList.Count; j++)
        {
            if (StatsList[j] == StatsEnum.GameTime)
            {
                int num =
                    StatsSave.OtherStats[(int)StatsList[j]];

                int num2 = num / 86400;
                int num3 = num % 86400;
                int num4 = num3 / 3600;
                int num5 = num3 % 3600 / 60;

                LoadedObj[j].text = "";

                if (num2 > 0)
                {
                    Text text = LoadedObj[j];
                    text.text =
                        text.text + num2 + "d";
                }

                if (num4 > 0)
                {
                    Text text2 = LoadedObj[j];
                    text2.text =
                        text2.text + num4 + "h";
                }

                Text text3 = LoadedObj[j];
                text3.text =
                    text3.text + num5 + "m";
            }
            else
            {
                LoadedObj[j].text =
                    StatsSave.OtherStats[
                        (int)StatsList[j]
                    ].ToString();
            }
        }

        Content.GetComponent<RectTransform>().localPosition =
            new Vector2(0f, 0f);
    }

    public void CloseViwer()
    {
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
