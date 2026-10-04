using System.Collections.Generic;
using SaveClass;
using StartScene;
using UnityEngine;
using UnityEngine.UI;

public class LevelSelector : MonoBehaviour
{
    public static LevelSelector Instance;

    public LevelDisPlay DisPlay1;
    public LevelDisPlay DisPlay2;
    public LevelDisPlay DisPlay3;
    public LevelDisPlay DisPlay4;

    public Text Title;
    public Text WeatherContent;
    public Text PassNumContent;
    public Text PassTimeContent;
    public Text Difficulty;

    public GameObject IconPrefab;
    public Transform IconGroup;

    public TextMesh AdventureText;

    public Image AdvcBtn;
    public Image MiniGBtn;
    public Image PuzzBtn;

    public bool IsEasy = true;

    private int CurrIndex;
    private int CurrLvId;
    private int CurrAdvcId;

    private List<LvSave> lVInfoList;

    public LvSave SelectedLvSave { get; private set; }

    private GameManager gameManager;
    private MapInfoIcon[] loadedIcons;

    private void Awake()
    {
        Instance = this;
        gameManager = GameManager.Instance;

        if (AdvcBtn != null && AdvcBtn.material != null)
            AdvcBtn.material = new Material(AdvcBtn.material);

        if (MiniGBtn != null && MiniGBtn.material != null)
            MiniGBtn.material = new Material(MiniGBtn.material);

        if (PuzzBtn != null && PuzzBtn.material != null)
            PuzzBtn.material = new Material(PuzzBtn.material);
    }

    public void StartAdvcGame()
    {
        if (gameManager != null &&
            !gameManager.isClient &&
            LVManager.Instance != null)
        {
            LVManager.Instance.StartGame(null, CurrAdvcId);
        }
    }

    public void StartCurrGame()
    {
        if (gameManager != null &&
            !gameManager.isClient &&
            LVManager.Instance != null)
        {
            LVManager.Instance.StartGame(null, CurrLvId);
        }
    }

    public void ChangeHard()
    {
        IsEasy = !IsEasy;

        if (Difficulty != null)
        {
            if (IsEasy)
            {
                Difficulty.text = "简单";
                Difficulty.color = new Color32(101, 244, 36, 255);
            }
            else
            {
                Difficulty.text = "困难";
                Difficulty.color = new Color32(244, 36, 39, 255);
            }
        }

        if (SelectedLvSave != null)
            DisLevelInfo(SelectedLvSave);

        PlayButtonSound();
    }

    public void OpenSelector()
    {
        UpdateLevelDis();

        transform.localScale = Vector3.one;
        gameObject.SetActive(true);
    }

    public void CloseSelector()
    {
        transform.localScale = Vector3.zero;
    }

    private void ClearBtn()
    {
        SetButtonBrightness(AdvcBtn, 1f);
        SetButtonBrightness(MiniGBtn, 1f);
        SetButtonBrightness(PuzzBtn, 1f);
    }

    private void SetButtonBrightness(Image button, float value)
    {
        if (button != null && button.material != null)
            button.material.SetFloat("_Brightness", value);
    }

    public void LoadAdventureBtn()
    {
        ClearBtn();
        SetButtonBrightness(AdvcBtn, 1.4f);

        if (gameManager == null)
            gameManager = GameManager.Instance;

        if (gameManager == null ||
            gameManager.CurrLvSeries == null ||
            gameManager.LocalPlayerSave == null)
        {
            return;
        }

        lVInfoList = gameManager.CurrLvSeries.LvSaves;

        LoadLastLv(gameManager.LocalPlayerSave.LastAdventureId);

        if (transform.localScale.x > 0f)
            PlayButtonSound();
    }

    public void LoadMiniGameBtn()
    {
        ClearBtn();
        SetButtonBrightness(MiniGBtn, 1.4f);

        if (gameManager == null)
            gameManager = GameManager.Instance;

        if (gameManager == null ||
            gameManager.CurrLvSeries == null ||
            gameManager.LocalPlayerSave == null)
        {
            return;
        }

        lVInfoList = gameManager.CurrLvSeries.LvSavesMiniGame;

        LoadLastLv(gameManager.LocalPlayerSave.LastMiniGameId);

        PlayButtonSound();
    }

    public void LoadPuzzleBtn()
    {
        ClearBtn();
        SetButtonBrightness(PuzzBtn, 1.4f);

        if (gameManager == null)
            gameManager = GameManager.Instance;

        if (gameManager == null ||
            gameManager.CurrLvSeries == null ||
            gameManager.LocalPlayerSave == null)
        {
            return;
        }

        lVInfoList = gameManager.CurrLvSeries.LvSavesPuzzle;

        LoadLastLv(gameManager.LocalPlayerSave.LastPuzzleId);

        PlayButtonSound();
    }

    public void LoadLastLv()
    {
        LoadAdventureBtn();
    }

    public void LoadLastLv(int lastId)
    {
        if (lVInfoList == null || lVInfoList.Count == 0)
            return;

        int num = lastId % 4;

        if (SelectMap.Instance == null)
            return;

        if (SelectMap.Instance.CurrLvSeriesId != lastId / 10000)
        {
            CurrIndex = 1;
            num = 1;
        }
        else
        {
            CurrIndex = lastId % 1000;

            if (CurrIndex < 1)
                CurrIndex = 1;
        }

        switch (num)
        {
            case 0:
                CurrIndex -= 3;

                if (CurrIndex < 1)
                    CurrIndex = 1;

                UpdateLevelDis();

                if (DisPlay4 != null)
                    DisPlay4.OnPointerClick(null);

                break;

            case 1:
                UpdateLevelDis();

                if (DisPlay1 != null)
                    DisPlay1.OnPointerClick(null);

                break;

            case 2:
                CurrIndex--;

                if (CurrIndex < 1)
                    CurrIndex = 1;

                UpdateLevelDis();

                if (DisPlay2 != null)
                    DisPlay2.OnPointerClick(null);

                break;

            case 3:
                CurrIndex -= 2;

                if (CurrIndex < 1)
                    CurrIndex = 1;

                UpdateLevelDis();

                if (DisPlay3 != null)
                    DisPlay3.OnPointerClick(null);

                break;
        }
    }

    public void SelectThis(LevelDisPlay levelDis)
    {
        if (levelDis == null)
            return;

        SelectedLvSave = levelDis.lVInfo;

        if (SelectedLvSave == null)
            return;

        DisLevelInfo(SelectedLvSave);

        if (DisPlay1 != null)
            DisPlay1.OnPointerExit(null);

        if (DisPlay2 != null)
            DisPlay2.OnPointerExit(null);

        if (DisPlay3 != null)
            DisPlay3.OnPointerExit(null);

        if (DisPlay4 != null)
            DisPlay4.OnPointerExit(null);
    }

    private void DisLevelInfo(LvSave info)
    {
        if (info == null ||
            LV.Instance == null ||
            MapManager.Instance == null)
        {
            return;
        }

        LV.Instance.LoadLV(
            info.LvId,
            IsEasy,
            onlyInfo: true,
            isRun: false
        );

        ClearMapIcons();
        CreateMapIcons();
        UpdateWeatherText();

        int passNum;
        int passTime;

        if (IsEasy)
        {
            passNum = info.PassNum;
            passTime = info.PassTime;
        }
        else
        {
            passNum = info.HardPNum;
            passTime = info.HardPTime;
        }

        if (Title != null)
            Title.text = LV.Instance.LvName;

        if (passNum <= 0 || passTime < 5)
        {
            if (PassNumContent != null)
                PassNumContent.text = "未通过";

            if (PassTimeContent != null)
                PassTimeContent.text = "--:--";
        }
        else
        {
            if (PassNumContent != null)
                PassNumContent.text = passNum + "次";

            if (PassTimeContent != null)
                PassTimeContent.text = FormatPassTime(passTime);
        }

        CurrLvId = info.LvId;

        if (info.LvId % 10000 < 1000)
        {
            CurrAdvcId = info.LvId;

            if (AdventureText != null)
            {
                AdventureText.text =
                    info.LvId / 10000 +
                    "-" +
                    info.LvId % 10000;
            }
        }
    }

    private void ClearMapIcons()
    {
        if (IconGroup == null)
            return;

        loadedIcons = IconGroup.GetComponentsInChildren<MapInfoIcon>(true);

        for (int i = 0; i < loadedIcons.Length; i++)
        {
            MapInfoIcon icon = loadedIcons[i];

            if (icon != null)
                Destroy(icon.gameObject);
        }

        loadedIcons = null;
    }

    private void CreateMapIcons()
    {
        if (IconGroup == null ||
            IconPrefab == null ||
            LV.Instance == null ||
            MapManager.Instance == null)
        {
            return;
        }

        List<MapType> mapTypes = LV.Instance.LoadMapTypes;

        if (mapTypes == null)
            return;

        for (int i = 0; i < mapTypes.Count; i++)
        {
            GameObject mapPrefab =
                MapManager.Instance.GetMapPrefab(mapTypes[i]);

            if (mapPrefab == null)
                continue;

            MapBase mapBase = mapPrefab.GetComponent<MapBase>();

            if (mapBase == null)
                continue;

            MapInfoIcon icon =
                Instantiate(IconPrefab).GetComponent<MapInfoIcon>();

            if (icon == null)
                continue;

            icon.CreateInit(
                mapBase.GotoSprite,
                isYes: true
            );

            icon.transform.SetParent(
                IconGroup,
                false
            );

            icon.transform.localScale = Vector3.one;
        }
    }

    private void UpdateWeatherText()
    {
        if (WeatherContent == null ||
            LV.Instance == null)
        {
            return;
        }

        string weatherText = string.Empty;

        if (HaveWeather(WeatherType.Rain))
            weatherText += " 有雨";

        if (HaveWeather(WeatherType.Thunder))
            weatherText += " 有雷";

        if (HaveWeather(WeatherType.Snow))
            weatherText += " 有雪";

        if (HaveWeather(WeatherType.Hail))
            weatherText += " 冰雹";

        if (HaveWeather(WeatherType.Wind))
            weatherText += " 有风";

        if (string.IsNullOrEmpty(weatherText))
            weatherText = " 晴朗";

        WeatherContent.text = weatherText;
    }

    private string FormatPassTime(int seconds)
    {
        int hours = seconds / 3600;
        int minutes = seconds % 3600 / 60;
        int remainingSeconds = seconds % 60;

        string result = string.Empty;

        if (hours > 0)
            result += hours + "时";

        if (minutes > 0)
            result += minutes + "分";

        result += remainingSeconds + "秒";

        return result;
    }

    private bool HaveWeather(WeatherType weatherType)
    {
        if (LV.Instance == null ||
            LV.Instance.LoadWeathers == null)
        {
            return false;
        }

        for (int i = 0; i < LV.Instance.LoadWeathers.Count; i++)
        {
            if (LV.Instance.LoadWeathers[i].type == weatherType)
                return true;
        }

        return false;
    }

    private void UpdateLevelDis()
    {
        if (lVInfoList == null)
            return;

        LvSave current = GetLvSave(CurrIndex);
        LvSave previous = GetLvSave(CurrIndex - 1);
        LvSave next = GetLvSave(CurrIndex + 1);
        LvSave next2 = GetLvSave(CurrIndex + 2);
        LvSave fourth = GetLvSave(CurrIndex + 3);

        if (DisPlay1 != null)
            DisPlay1.OpenInit(current, previous);

        if (DisPlay2 != null)
            DisPlay2.OpenInit(next, current);

        if (DisPlay3 != null)
            DisPlay3.OpenInit(next2, next);

        if (DisPlay4 != null)
            DisPlay4.OpenInit(fourth, next2);

        if (DisPlay1 != null)
            DisPlay1.OnPointerExit(null);

        if (DisPlay2 != null)
            DisPlay2.OnPointerExit(null);

        if (DisPlay3 != null)
            DisPlay3.OnPointerExit(null);

        if (DisPlay4 != null)
            DisPlay4.OnPointerExit(null);
    }

    private LvSave GetLvSave(int index)
    {
        if (lVInfoList == null ||
            index < 1 ||
            index > lVInfoList.Count)
        {
            return null;
        }

        return lVInfoList[index - 1];
    }

    public void LevelUp()
    {
        if (CurrIndex < 3)
            return;

        CurrIndex -= 4;

        UpdateLevelDis();
        PlayButtonSound();
    }

    public void LevelDown()
    {
        if (lVInfoList == null ||
            CurrIndex > lVInfoList.Count - 4)
        {
            return;
        }

        CurrIndex += 4;

        UpdateLevelDis();
        PlayButtonSound();
    }

    private void PlayButtonSound()
    {
        if (AudioManager.Instance != null &&
            gameManager != null &&
            gameManager.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                gameManager.AudioConf.GraveButton,
                transform.position,
                isAll: true
            );
        }
    }
}
