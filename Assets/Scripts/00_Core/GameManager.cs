using System.Collections;
using System.Collections.Generic;
using System.IO;
using SaveClass;
using StartScene;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public string SavePath;
    public string lastSavePath;
    public bool isOnline;
    public bool isAndroid;
    public string VersionCode = "0.7";
    public string HostName;

    public UserSave LocalPlayerSave;
    public LvSeries CurrLvSeries;
    public StatsAndAcvSave StatsAcvSave;

    public static char[] keyChars =
    {
        's', 'm', 'a', 'r', 't', 'f', 'a', 'l', 'c', 'o', 'n'
    };

    public GameConf GameConf { get; private set; }
    public AudioConf AudioConf { get; private set; }

    public StatsManager StatsManager;

    public bool IsStartupReady { get; private set; }
    public float StartupProgress { get; private set; }

    private const string PlayerSaveFileName = "Player.config";
    private const string StatsSaveFileName = "SAAInfo.config";

    public bool isServer =>
        isOnline &&
        SocketServer.Instance != null &&
        SocketServer.Instance.isServerOpen;

    public bool isClient =>
        isOnline &&
        SocketServer.Instance != null &&
        !SocketServer.Instance.isServerOpen;

    public MapStoneBase SelectedStone =>
        SelectMap.Instance != null
            ? SelectMap.Instance.SelectedStone
            : null;

    private bool IsMobilePlatform =>
        Application.platform == RuntimePlatform.Android ||
        Application.platform == RuntimePlatform.IPhonePlayer;

    private string SaveRootPath
    {
        get
        {
            if (IsMobilePlatform)
            {
                return Path.Combine(
                    Application.persistentDataPath,
                    "saves"
                );
            }

            DirectoryInfo directory =
                Directory.GetParent(Application.dataPath);

            return Path.Combine(
                directory.FullName,
                "saves"
            );
        }
    }

    public static string Encrypt(string data)
    {
        if (string.IsNullOrEmpty(data))
            return data;

        char[] chars = data.ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] =
                (char)(chars[i] ^
                keyChars[i % keyChars.Length]);
        }

        return new string(chars);
    }

    public static string Decrypt(string data)
    {
        return Encrypt(data);
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        SavePath = SaveRootPath;

        isAndroid =
            Application.platform == RuntimePlatform.Android;

        DontDestroyOnLoad(gameObject);

        GameConf =
            Resources.Load<GameConf>("GameConf");

        AudioConf =
            Resources.Load<AudioConf>("AudioConf");

        IsStartupReady = false;
        StartupProgress = 0f;
    }

    private void Start()
    {
        StartCoroutine(InitializeStartup());
    }

    private IEnumerator InitializeStartup()
    {
        StartupProgress = 0.05f;

        LoadSetting();

        StartupProgress = 0.15f;

        bool loaded = TryLoadLastSave();

        StartupProgress = 0.45f;

        if (!loaded)
        {
            loaded = TryLoadFirstAvailableSave();
        }

        StartupProgress = 0.60f;

        if (!loaded)
        {
            PrepareEmptySaveState();

            StartupProgress = 1f;
            IsStartupReady = true;

            yield return null;

            if (ChooseSave.Instance != null &&
                ChooseSave.Instance.AddUser != null)
            {
                ChooseSave.Instance.AddUser.Display(false);
            }

            yield break;
        }

        yield return null;

        FirstLoadLvSeries();

        StartupProgress = 0.75f;

        yield return null;

        LoadStatsAndAchievements();

        StartupProgress = 0.95f;

        IsStartupReady = true;

        yield return null;

        SaveUserInfo();

        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.AddStatsNum(
                StatsEnum.GameOpenNum
            );
        }

        yield return null;

        SaveSAInfo();

        StartupProgress = 1f;
    }

    private bool TryLoadLastSave()
    {
        if (string.IsNullOrEmpty(lastSavePath))
            return false;

        string file =
            Path.Combine(
                lastSavePath,
                PlayerSaveFileName
            );

        if (!File.Exists(file))
            return false;

        try
        {
            string data =
                File.ReadAllText(file);

            if (!MyTool.TryParseJson<UserSave>(
                Decrypt(data),
                out var save))
            {
                return false;
            }

            LoadSaveInternal(
                save,
                lastSavePath,
                true
            );

            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryLoadFirstAvailableSave()
    {
        lastSavePath = "";

        if (!Directory.Exists(SavePath))
        {
            Directory.CreateDirectory(SavePath);
            return false;
        }

        DirectoryInfo[] directories =
            new DirectoryInfo(SavePath).GetDirectories();

        for (int i = 0; i < directories.Length; i++)
        {
            DirectoryInfo directory =
                directories[i];

            string file =
                Path.Combine(
                    directory.FullName,
                    PlayerSaveFileName
                );

            if (!File.Exists(file))
                continue;

            try
            {
                string data =
                    File.ReadAllText(file);

                if (!MyTool.TryParseJson<UserSave>(
                    Decrypt(data),
                    out var save))
                {
                    continue;
                }

                LoadSaveInternal(
                    save,
                    directory.FullName,
                    true
                );

                return true;
            }
            catch
            {
                continue;
            }
        }

        return false;
    }

    private void PrepareEmptySaveState()
    {
        LocalPlayerSave = null;
        CurrLvSeries = null;
        StatsAcvSave = new StatsAndAcvSave();
        lastSavePath = "";

        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.LoadStatsSave();
        }

        if (AcvmentManager.Instance != null)
        {
            AcvmentManager.Instance.LoadAcvSave();
        }
    }

    private void LoadStatsAndAchievements()
    {
        if (string.IsNullOrEmpty(lastSavePath))
        {
            StatsAcvSave =
                new StatsAndAcvSave();

            if (StatsManager.Instance != null)
            {
                StatsManager.Instance.LoadStatsSave();
            }

            if (AcvmentManager.Instance != null)
            {
                AcvmentManager.Instance.LoadAcvSave();
            }

            return;
        }

        string file =
            Path.Combine(
                lastSavePath,
                StatsSaveFileName
            );

        if (File.Exists(file))
        {
            try
            {
                if (MyTool.TryParseJson<StatsAndAcvSave>(
                    Decrypt(
                        File.ReadAllText(file)
                    ),
                    out var stats))
                {
                    StatsAcvSave = stats;
                }
            }
            catch
            {
                StatsAcvSave = null;
            }
        }

        if (StatsAcvSave == null)
        {
            StatsAcvSave =
                new StatsAndAcvSave();
        }

        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.LoadStatsSave();
        }

        if (AcvmentManager.Instance != null)
        {
            AcvmentManager.Instance.LoadAcvSave();
        }

        if (LocalPlayerSave != null &&
            LocalPlayerSave.UnlockedPlants != null &&
            LocalPlayerSave.UnlockedPlants.Count >= 49 &&
            AcvmentManager.Instance != null)
        {
            AcvmentManager.Instance.GetAchievement(
                Acvname.Plant49
            );
        }
    }

    private void FirstLoadLvSeries()
    {
        if (LocalPlayerSave == null)
            return;

        if (SelectMap.Instance == null ||
            SelectMap.Instance.Stones == null ||
            SelectMap.Instance.Stones.Count == 0)
        {
            return;
        }

        int series =
            LocalPlayerSave.LastAdventureId / 10000;

        if (series > 0 &&
            series <= SelectMap.Instance.Stones.Count)
        {
            SelectMap.Instance.Stones[
                series - 1
            ].SelectThis();

            LoadLvInfo(series);
        }
        else
        {
            SelectMap.Instance.Stones[0].SelectThis();
            LoadLvInfo(1);
        }

        if (LevelSelector.Instance != null)
        {
            LevelSelector.Instance.LoadLastLv();
        }
    }

    public void LoadSave(
        UserSave save,
        string path)
    {
        LoadSaveInternal(
            save,
            path,
            false
        );
    }

    private void LoadSaveInternal(
        UserSave save,
        string path,
        bool startupLoad)
    {
        if (save == null)
            return;

        if (lastSavePath != path)
        {
            lastSavePath = path;

            if (!startupLoad)
            {
                SaveSetting();
            }
        }

        LocalPlayerSave = save;

        if (ChatInput.Instance != null)
        {
            ChatInput.Instance.ResetAllCmd();
        }

        if (LocalPlayerSave.UnlockedPlants == null)
        {
            LocalPlayerSave.UnlockedPlants =
                new List<PlantType>();
        }

        if (!LocalPlayerSave.UnlockedPlants.Contains(
                PlantType.PeaShooter))
        {
            LocalPlayerSave.UnlockedPlants.Add(
                PlantType.PeaShooter
            );
        }

        if (!LocalPlayerSave.UnlockedPlants.Contains(
                PlantType.SunFlower))
        {
            LocalPlayerSave.UnlockedPlants.Add(
                PlantType.SunFlower
            );
        }

        if (LocalPlayerSave.CardSlotNum < 6)
        {
            LocalPlayerSave.CardSlotNum = 6;
        }

        if (LocalPlayerSave.LastAdventureId <= 10000)
        {
            LocalPlayerSave.LastAdventureId = 10001;
        }

        if (LocalPlayerSave.LastMiniGameId <= 11000)
        {
            LocalPlayerSave.LastMiniGameId = 11001;
        }

        if (LocalPlayerSave.LastPuzzleId <= 12000)
        {
            LocalPlayerSave.LastPuzzleId = 12001;
        }

        if (LocalPlayerSave.MoreOptions == null)
        {
            LocalPlayerSave.MoreOptions =
                new List<bool>();
        }

        if (LocalPlayerSave.MoreOptions.Count == 0)
        {
            for (int i = 0; i < 10; i++)
            {
                LocalPlayerSave.MoreOptions.Add(false);
            }
        }

        if (LocalPlayerSave.QuickChat == null)
        {
            LocalPlayerSave.QuickChat =
                new List<string>();
        }

        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.ReadSave(
                LocalPlayerSave.MoneyNum
            );
        }

        if (StartSceneManager.Instance != null &&
            StartSceneManager.Instance.changeUser != null &&
            StartSceneManager.Instance.changeUser.NameText != null)
        {
            StartSceneManager.Instance.changeUser.NameText.text =
                LocalPlayerSave.playerName + "!";
        }

        if (UIManager.Instance != null &&
            UIManager.Instance.SetPanel != null)
        {
            UIManager.Instance.SetPanel.MoreOptionRead(
                LocalPlayerSave.MoreOptions,
                LocalPlayerSave.OpenQuickChat,
                LocalPlayerSave.QuickChat
            );
        }

        if (SeedBank.Instance != null)
        {
            SeedBank.Instance.LastSelectCard =
                LocalPlayerSave.LastSelectedCard;
        }

        if (StartSceneManager.Instance != null)
        {
            StartSceneManager.Instance.LoadStartScence(false);
        }

        if (!startupLoad)
        {
            SaveUserInfo();

            FirstLoadLvSeries();

            LoadStatsAndAchievements();
        }
    }

    public void SaveUserInfo()
    {
        if (LocalPlayerSave == null)
            return;

        if (PlayerManager.Instance != null)
        {
            LocalPlayerSave.MoneyNum =
                PlayerManager.Instance.Money;
        }

        LocalPlayerSave.VersionCode =
            VersionCode;

        if (LocalPlayerSave.MoreOptions == null)
        {
            LocalPlayerSave.MoreOptions =
                new List<bool>();
        }

        LocalPlayerSave.MoreOptions.Clear();

        if (GobalLight.Instance != null)
        {
            LocalPlayerSave.MoreOptions.Add(
                GobalLight.Instance.LightingNotDark
            );
        }
        else
        {
            LocalPlayerSave.MoreOptions.Add(false);
        }

        if (SeedBank.Instance != null)
        {
            LocalPlayerSave.MoreOptions.Add(
                SeedBank.Instance.IsCdDown
            );

            LocalPlayerSave.LastSelectedCard =
                new List<CardType>(
                    SeedBank.Instance.LastSelectCard
                );
        }
        else
        {
            LocalPlayerSave.MoreOptions.Add(false);
        }

        if (PlayerManager.Instance != null)
        {
            LocalPlayerSave.MoreOptions.Add(
                PlayerManager.Instance.NormalSunUp
            );

            LocalPlayerSave.MoreOptions.Add(
                PlayerManager.Instance.GetSunUp
            );
        }
        else
        {
            LocalPlayerSave.MoreOptions.Add(false);
            LocalPlayerSave.MoreOptions.Add(false);
        }

        if (SkyManager.Instance != null)
        {
            LocalPlayerSave.MoreOptions.Add(
                SkyManager.Instance.SlowSunAutoCollect
            );
        }
        else
        {
            LocalPlayerSave.MoreOptions.Add(false);
        }

        if (UIManager.Instance != null &&
            UIManager.Instance.SetPanel != null)
        {
            LocalPlayerSave.OpenQuickChat =
                UIManager.Instance.SetPanel.OpenQuickChat;

            if (LocalPlayerSave.QuickChat == null)
            {
                LocalPlayerSave.QuickChat =
                    new List<string>();
            }

            LocalPlayerSave.QuickChat.Clear();

            if (UIManager.Instance.SetPanel.QuickChat1 != null)
            {
                LocalPlayerSave.QuickChat.Add(
                    UIManager.Instance.SetPanel.QuickChat1.text
                );
            }

            if (UIManager.Instance.SetPanel.QuickChat2 != null)
            {
                LocalPlayerSave.QuickChat.Add(
                    UIManager.Instance.SetPanel.QuickChat2.text
                );
            }
        }

        if (string.IsNullOrEmpty(lastSavePath))
            return;

        if (!Directory.Exists(lastSavePath))
        {
            Directory.CreateDirectory(lastSavePath);
        }

        string file =
            Path.Combine(
                lastSavePath,
                PlayerSaveFileName
            );

        File.WriteAllText(
            file,
            Encrypt(
                JsonUtility.ToJson(
                    LocalPlayerSave
                )
            )
        );
    }

    public void SaveSAInfo()
    {
        if (StatsAcvSave == null ||
            string.IsNullOrEmpty(lastSavePath))
        {
            return;
        }

        string file =
            Path.Combine(
                lastSavePath,
                StatsSaveFileName
            );

        File.WriteAllText(
            file,
            Encrypt(
                JsonUtility.ToJson(
                    StatsAcvSave
                )
            )
        );
    }

    public void AddNewPlant(
        PlantType plantType)
    {
        if (LocalPlayerSave == null)
            return;

        if (plantType == PlantType.Nope ||
            plantType == PlantType.MoonTombStone ||
            plantType == PlantType.RepeaterReverse ||
            plantType == PlantType.ExplodeNut ||
            plantType == PlantType.HugeNut ||
            LocalPlayerSave.UnlockedPlants.Contains(
                plantType))
        {
            return;
        }

        LocalPlayerSave.UnlockedPlants.Add(
            plantType
        );

        SaveUserInfo();

        if (LocalPlayerSave.UnlockedPlants.Count >= 49 &&
            AcvmentManager.Instance != null)
        {
            AcvmentManager.Instance.GetAchievement(
                Acvname.Plant49
            );
        }
    }

    public LvSave GetLvSave(int LVid)
    {
        if (CurrLvSeries == null ||
            CurrLvSeries.LvSaves == null ||
            CurrLvSeries.LvSaves.Count == 0)
        {
            return null;
        }

        int series =
            LVid / 10000;

        int currentSeries =
            CurrLvSeries.LvSaves[0].LvId / 10000;

        if (series != currentSeries)
        {
            LoadLvInfo(series);
        }

        foreach (LvSave save in
                 GetLvSaves(LV.Instance.CurrLvId))
        {
            if (save.LvId == LVid)
                return save;
        }

        return null;
    }

    public void LoadLvInfo(int SeriesId)
    {
        if (SelectMap.Instance == null ||
            SelectMap.Instance.SelectedStone == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(lastSavePath))
            return;

        string file =
            Path.Combine(
                lastSavePath,
                "Wsave" + SeriesId + ".config"
            );

        bool loaded = false;

        if (File.Exists(file))
        {
            try
            {
                if (MyTool.TryParseJson<LvSeries>(
                    Decrypt(
                        File.ReadAllText(file)
                    ),
                    out var series))
                {
                    CurrLvSeries = series;
                    loaded = true;
                }
            }
            catch
            {
                loaded = false;
            }
        }

        if (!loaded)
        {
            CurrLvSeries = new LvSeries();
        }

        if (CurrLvSeries.LvSaves == null)
        {
            CurrLvSeries.LvSaves =
                new List<LvSave>();
        }

        if (CurrLvSeries.LvSavesMiniGame == null)
        {
            CurrLvSeries.LvSavesMiniGame =
                new List<LvSave>();
        }

        if (CurrLvSeries.LvSavesPuzzle == null)
        {
            CurrLvSeries.LvSavesPuzzle =
                new List<LvSave>();
        }

        int adventure =
            SelectMap.Instance.SelectedStone.AdventureLvNum;

        int miniGame =
            SelectMap.Instance.SelectedStone.MiniGameLvNum;

        int puzzle =
            SelectMap.Instance.SelectedStone.PuzzleLvNum;

        while (CurrLvSeries.LvSaves.Count < adventure)
        {
            CurrLvSeries.LvSaves.Add(
                new LvSave
                {
                    LvId =
                        SeriesId * 10000 +
                        CurrLvSeries.LvSaves.Count + 1
                }
            );
        }

        while (CurrLvSeries.LvSavesMiniGame.Count < miniGame)
        {
            CurrLvSeries.LvSavesMiniGame.Add(
                new LvSave
                {
                    LvId =
                        SeriesId * 11000 +
                        CurrLvSeries.LvSavesMiniGame.Count + 1
                }
            );
        }

        while (CurrLvSeries.LvSavesPuzzle.Count < puzzle)
        {
            CurrLvSeries.LvSavesPuzzle.Add(
                new LvSave
                {
                    LvId =
                        SeriesId * 12000 +
                        CurrLvSeries.LvSavesPuzzle.Count + 1
                }
            );
        }

        while (CurrLvSeries.LvSavesMiniGame.Count > miniGame)
        {
            CurrLvSeries.LvSavesMiniGame.RemoveAt(
                CurrLvSeries.LvSavesMiniGame.Count - 1
            );
        }

        while (CurrLvSeries.LvSavesPuzzle.Count > puzzle)
        {
            CurrLvSeries.LvSavesPuzzle.RemoveAt(
                CurrLvSeries.LvSavesPuzzle.Count - 1
            );
        }
    }

    public void SaveLvInfo(bool saveCurrLv)
    {
        if (CurrLvSeries == null ||
            LV.Instance == null)
        {
            return;
        }

        List<LvSave> saves =
            GetLvSaves(
                LV.Instance.CurrLvId
            );

        if (saves == null ||
            saves.Count == 0)
        {
            return;
        }

        if (saveCurrLv)
        {
            foreach (LvSave save in saves)
            {
                if (save.LvId != LV.Instance.CurrLvId)
                    continue;

                if (LV.Instance.IsEasy)
                {
                    if ((save.PassTime <= 10 ||
                         LVManager.Instance.PassTime <
                         save.PassTime) &&
                        LVManager.Instance.PassTime > 10)
                    {
                        save.PassTime =
                            LVManager.Instance.PassTime;
                    }

                    save.PassNum++;
                }
                else
                {
                    if ((save.HardPTime <= 10 ||
                         LVManager.Instance.PassTime <
                         save.HardPTime) &&
                        LVManager.Instance.PassTime > 10)
                    {
                        save.HardPTime =
                            LVManager.Instance.PassTime;
                    }

                    save.HardPNum++;
                }

                break;
            }
        }

        int series =
            saves[0].LvId / 10000;

        string file =
            Path.Combine(
                lastSavePath,
                "Wsave" + series + ".config"
            );

        File.WriteAllText(
            file,
            Encrypt(
                JsonUtility.ToJson(
                    CurrLvSeries
                )
            )
        );
    }

    private List<LvSave> GetLvSaves(int lvid)
    {
        int type =
            (lvid % 10000) / 1000;

        if (type == 1)
            return CurrLvSeries.LvSavesMiniGame;

        if (type == 2)
            return CurrLvSeries.LvSavesPuzzle;

        return CurrLvSeries.LvSaves;
    }

    private void LoadSetting()
    {
        string file =
            Path.Combine(
                SavePath,
                FixedInfo.SettingInfoName
            );

        if (!File.Exists(file))
            return;

        try
        {
            if (MyTool.TryParseJson<SettingSave>(
                File.ReadAllText(file),
                out var setting))
            {
                if (UIManager.Instance != null &&
                    UIManager.Instance.SetPanel != null)
                {
                    UIManager.Instance.SetPanel.SaveInit(
                        setting
                    );
                }

                lastSavePath =
                    setting.lastLoadSavePath;

                Screen.fullScreen =
                    setting.isFullScreen;
            }
        }
        catch
        {
        }
    }

    public void SaveSetting()
    {
        if (!Directory.Exists(SavePath))
        {
            Directory.CreateDirectory(SavePath);
        }

        SettingSave setting =
            new SettingSave
            {
                bgmVolume =
                    AudioManager.Instance.BgmVolume,

                soundVolume =
                    AudioManager.Instance.SoundVolume,

                lastLoadSavePath =
                    lastSavePath,

                isFullScreen =
                    Screen.fullScreen,

                isF1080P =
                    UIManager.Instance.SetPanel.is1080P,

                isRainFog =
                    SkyManager.Instance.isRainFog,

                isCardSelector =
                    SeedBank.Instance.CardSelector,

                FrameType =
                    UIManager.Instance.SetPanel.FrameType,

                isVsync =
                    UIManager.Instance.SetPanel.isVsync,

                isDisFrame =
                    UIManager.Instance.SetPanel.isDisFrame
            };

        File.WriteAllText(
            Path.Combine(
                SavePath,
                FixedInfo.SettingInfoName
            ),
            JsonUtility.ToJson(setting)
        );
    }

    public List<CustomMapSave> LoadCustomMapFile()
    {
        List<CustomMapSave> list =
            new List<CustomMapSave>();

        string path =
            Path.Combine(
                Directory.GetParent(
                    Application.dataPath
                ).FullName,
                "custom",
                "maps"
            );

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        string[] files =
            Directory.GetFiles(
                path,
                "*.config"
            );

        foreach (string file in files)
        {
            if (!File.Exists(file))
                continue;

            if (MyTool.TryParseJson<CustomMapSave>(
                Decrypt(
                    File.ReadAllText(file)
                ),
                out var map))
            {
                map.SavePath = file;
                list.Add(map);
            }
        }

        if (list.Count == 0)
        {
            CreateNewCustomMap();
            return LoadCustomMapFile();
        }

        return list;
    }

    public void CreateNewCustomMap()
    {
        string path =
            Path.Combine(
                Directory.GetParent(
                    Application.dataPath
                ).FullName,
                "custom",
                "maps"
            );

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        CustomMapSave map =
            new CustomMapSave
            {
                MapBrief =
                    "Un nuevo mapa personalizado",

                mapType =
                    MapType.CustomYard,

                VerticalNum = 5,
                HorizontalNum = 9,
                MapName = "newmap"
            };

        for (int i = 0; i < 45; i++)
        {
            map.tileTypes.Add(
                TileType.Grass
            );
        }

        int n = 0;
        string name = map.MapName;

        while (File.Exists(
            Path.Combine(
                path,
                name + ".config"
            )))
        {
            n++;
            name =
                "newmap" + n;
        }

        map.MapName = name;

        File.WriteAllText(
            Path.Combine(
                path,
                name + ".config"
            ),
            Encrypt(
                JsonUtility.ToJson(map)
            )
        );
    }

    public List<CustomLevelSave> LoadCustomLevelFile()
    {
        List<CustomLevelSave> list =
            new List<CustomLevelSave>();

        string path =
            Path.Combine(
                Directory.GetParent(
                    Application.dataPath
                ).FullName,
                "custom",
                "levels"
            );

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        string[] files =
            Directory.GetFiles(
                path,
                "*.config"
            );

        foreach (string file in files)
        {
            if (!File.Exists(file))
                continue;

            if (MyTool.TryParseJson<CustomLevelSave>(
                Decrypt(
                    File.ReadAllText(file)
                ),
                out var level))
            {
                level.SavePath = file;
                list.Add(level);
            }
        }

        if (list.Count == 0)
        {
            CreateNewCustomLv();
            return LoadCustomLevelFile();
        }

        return list;
    }

    public void CreateNewCustomLv()
    {
        string path =
            Path.Combine(
                Directory.GetParent(
                    Application.dataPath
                ).FullName,
                "custom",
                "levels"
            );

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        CustomLevelSave level =
            new CustomLevelSave
            {
                LvBrief =
                    "Un nuevo nivel personalizado",

                lVType =
                    LVType.Normal,

                series =
                    MapSeries.Yard,

                mapTypes =
                    new List<MapType>
                    {
                        MapType.FrontYard
                    },

                LvName = "newlv"
            };

        int n = 0;
        string name = level.LvName;

        while (File.Exists(
            Path.Combine(
                path,
                name + ".config"
            )))
        {
            n++;
            name =
                "newlv" + n;
        }

        level.LvName = name;

        File.WriteAllText(
            Path.Combine(
                path,
                name + ".config"
            ),
            Encrypt(
                JsonUtility.ToJson(level)
            )
        );
    }
}