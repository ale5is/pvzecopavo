using System;
using System.Collections;
using System.Collections.Generic;
using SocketSave;
using StartScene;
using UnityEngine;
using UnityEngine.Events;

public class LVManager : MonoBehaviour
{
    public static LVManager Instance;

    [Header("Battle UI")]
    

    public bool LvSpawnisOver;
    public bool BootyIsAppeared;
    public Booty OnlyBooty;
    public bool CanHandNextWave;
    public bool LastStandNoCd;
    public bool isBigWave;
    public bool LvDontFail;
    public bool StopSpawn;
    public int PassTime;

    private LVState currLVState;
    private int AllWeight;
    private int currLVWave;
    private int BIGwaveNum;

    [SerializeField] private int CurrSubLv;
    [SerializeField] private int lvTotalTime;

    private UnityAction LvStartAction;
    private Coroutine RestSpawnCoroutine;
    private Coroutine AutoNextWaveCoroutine;
    private bool isWaitSpawn;
    private bool waitingGoNextWave;

    public bool InGame => MapManager.Instance.mapList.Count > 0;
    public bool GameIsStart => currLVState == LVState.Fighting;
    public bool IsRestTime { get; private set; }

    public LVState CurrLVState
    {
        get => currLVState;
        private set
        {
            currLVState = value;

            if (value != LVState.Start)
                return;

            LvSpawnisOver = false;
            BootyIsAppeared = false;

            if (OnlyBooty != null)
                OnlyBooty.DestroyThis();

            OnlyBooty = null;
        }
    }

    public int CurrLVWave
    {
        get => currLVWave;
        set
        {
            currLVWave = value;

            if (value < LV.Instance.Weights[0].Count)
            {
                AutoStartNextWave();
                return;
            }

            LvSpawnisOver = true;
            SocketServer.Instance.BigWaveComing(
                new WaveComing { WaveType = 3 }
            );
        }
    }

    public int LvTotalTime
    {
        get => lvTotalTime;
        set
        {
            if (GameManager.Instance.isClient)
                return;

            int previous = lvTotalTime;

            if (!StopSpawn)
                lvTotalTime = value;

            if (lvTotalTime < 0)
                lvTotalTime = 0;

            if (lvTotalTime == 0)
                FlagMeter.Instance.SetRestTimeText(null);

            if (previous > 10 && lvTotalTime < 10)
                SkipRestBtn.Instance.CloseBtn();

            if (LV.Instance.SubLvs.Count > CurrSubLv || IsRestTime)
            {
                if (previous > 0 && value == 0 && !waitingGoNextWave)
                {
                    if (!isWaitSpawn)
                        GoNextSubLv();

                    AllTime();
                    IsRestTime = false;
                    isWaitSpawn = false;
                    AutoStartNextWave();

                    PlayEffect(GameManager.Instance.AudioConf.Awooga);
                }
            }

            FlagMeter.Instance.SetRestTimeText(
                IsRestTime
                    ? "下次进攻还有" + LvTotalTime + "秒"
                    : null
            );
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    public string LvTypeName(LVType type)
    {
        if (type == LVType.Normal)
            return "普通模式";

        if (type == LVType.PvP)
            return "玩家对战";

        if (type == LVType.IZombie)
            return "我是僵尸";

        if (type == LVType.VaseBreaker)
            return "砸罐子";

        return "";
    }

    private void PlayEffect(AudioClip audio)
    {
        if (AudioManager.Instance != null && audio != null)
        {
            AudioManager.Instance.PlayEFAudio(
                audio,
                transform.position,
                isAll: true
            );
        }
    }

    public void StartGame(LoadLVBag loadBag, int LVId)
    {
        if (InGame || (GameManager.Instance.isClient && loadBag == null))
            return;

        AudioManager.Instance.StopBgAudio();
        CameraControl.Instance.InAcvment = false;
        PvPSelector.Instance.CloseSelector();
        LevelSelector.Instance.CloseSelector();
        UIManager.Instance.LogPanel.Close();
        UIManager.Instance.SetPanel.CloseSetPanel();
        AlmanacScence.Instance.BackMenu();
        GobalLight.Instance.InitIntensity();

        StartLv(loadBag, LVId, true);

        PlayerManager.Instance.ResetSunNum();
        GoOtherMap.Instance.LoadInit();
        PlantManager.Instance.LoadLvStartPlant();
        Timetable.Instance.UpdateTempt(CameraControl.Instance.CurrMap);

        Timetable.Instance.gameObject.SetActive(
            LV.Instance.CurrLVType != LVType.IZombie &&
            LV.Instance.CurrLVType != LVType.VaseBreaker
        );
    }

    private void StartLv(
        LoadLVBag loadBag,
        int LVId,
        bool needloadLv)
    {
        int lvSeed = UnityEngine.Random.Range(
            1000000,
            9999999
        );

        CurrLVState = LVState.Start;

        if (GameManager.Instance.isClient && loadBag != null)
        {
            lvSeed = loadBag.LvSeed;

            if (needloadLv)
                LV.Instance.ClientLoadLv(loadBag);

            if (SpectatorList.Instance.LocalIsSpectator)
            {
                SeedBank.Instance.CardNum = 0;
            }
            else
            {
                for (int i = 0; i < loadBag.NameList.Count; i++)
                {
                    if (
                        loadBag.NameList[i] !=
                        GameManager.Instance.LocalPlayerSave.playerName
                    )
                        continue;

                    SeedBank.Instance.CardNum =
                        loadBag.CardNumList[i];

                    break;
                }
            }

            BattlePlayerList.Instance.LoadAllSeedBank(
                loadBag.NameList,
                loadBag.CardNumList
            );

            FlagMeter.Instance.SetLvlName(
                LV.Instance.LvName,
                loadBag.BoolTypes[0]
            );
        }

        List<string> players =
            SpectatorList.Instance.GetNoSpectatorPlayerList();

        List<int> cardNums = new();

        if (GameManager.Instance.isServer)
        {
            if (needloadLv)
            {
                LV.Instance.LoadLV(
                    LVId,
                    LevelSelector.Instance.IsEasy,
                    onlyInfo: false,
                    isRun: false
                );
            }

            if (LV.Instance.CurrLVType == LVType.PvP)
            {
                int cardNum = PvPSelector.Instance.CardNum;
                int redCount =
                    PvPSelector.Instance.RedTeamNames.Count;
                int blueCount =
                    PvPSelector.Instance.BlueTeamNames.Count;

                int redBase = cardNum / redCount;
                int redExtra = cardNum % redCount;

                int blueBase = cardNum / blueCount;
                int blueExtra = cardNum % blueCount;

                bool redAssigned = false;
                bool blueAssigned = false;

                foreach (string player in players)
                {
                    if (
                        PvPSelector.Instance.RedTeamNames
                            .Contains(player)
                    )
                    {
                        cardNums.Add(
                            redAssigned
                                ? redBase
                                : redBase + redExtra
                        );

                        redAssigned = true;
                    }
                    else if (
                        PvPSelector.Instance.BlueTeamNames
                            .Contains(player)
                    )
                    {
                        cardNums.Add(
                            blueAssigned
                                ? blueBase
                                : blueBase + blueExtra
                        );

                        blueAssigned = true;
                    }
                }

                SeedBank.Instance.CardNum = 0;

                for (int i = 0; i < players.Count; i++)
                {
                    if (
                        players[i] !=
                        GameManager.Instance.LocalPlayerSave.playerName
                    )
                        continue;

                    SeedBank.Instance.CardNum =
                        cardNums[i];

                    break;
                }
            }
            else
            {
                int cardNum =
                    LV.Instance.CardNum >= 0
                        ? LV.Instance.CardNum
                        : GameManager.Instance.LocalPlayerSave.CardSlotNum +
                          GameManager.Instance.LocalPlayerSave.SpItems
                              .FindAll(
                                  x => x == SpItem.StoreCardSlot
                              )
                              .Count;

                if (!LV.Instance.BanMultyCardAdd)
                    cardNum += players.Count - 1;

                List<int> slots = new(cardNum);

                for (int i = 0; i < cardNum; i++)
                    slots.Add(0);

                List<List<int>> split =
                    MyTool.SplitByNumberOfLists(
                        slots,
                        players.Count
                    );

                foreach (List<int> list in split)
                    cardNums.Add(list.Count);

                SeedBank.Instance.CardNum =
                    split[0].Count;
            }

            if (SpectatorList.Instance.LocalIsSpectator)
                SeedBank.Instance.CardNum = 0;

            LoadLVBag bag = new()
            {
                LvId = LVId,
                LvSeed = lvSeed,
                LvName = LV.Instance.LvName,
                dayBgm = LV.Instance.DayBgm,
                nightBgm = LV.Instance.NightBgm,
                BankType = LV.Instance.CurrBankType,
                SeedBankType = LV.Instance.CurrSeedBankType,
                LoadMapTypes = LV.Instance.LoadMapTypes,
                LvSpStates = LV.Instance.LvSpStates,
                CardNumList = cardNums,
                NameList = players,
                BoolTypes = new List<bool>
                {
                    LevelSelector.Instance.IsEasy,
                    LV.Instance.EnableShovel,
                    LV.Instance.EnablePlantGlove,
                    LV.Instance.EnableZombieGlove
                }
            };

            List<ZombieType> zombieTypes = new();
            List<int> zombieSplits = new();

            for (
                int i = 0;
                i < LV.Instance.ZombieTypes.Count;
                i++
            )
            {
                zombieSplits.Add(
                    LV.Instance.ZombieTypes[i].Count
                );

                zombieTypes.AddRange(
                    LV.Instance.ZombieTypes[i]
                );
            }

            bag.ZTypesSplit = zombieSplits;
            bag.ZombieTypes = zombieTypes;

            SocketServer.Instance.LoadLv(bag);
        }

        if (!GameManager.Instance.isClient)
        {
            if (needloadLv)
            {
                LV.Instance.LoadLV(
                    LVId,
                    LevelSelector.Instance.IsEasy,
                    onlyInfo: false,
                    isRun: true
                );
            }

            if (!GameManager.Instance.isServer)
            {
                if (LV.Instance.CardNum >= 0)
                {
                    SeedBank.Instance.CardNum =
                        LV.Instance.CardNum;
                }
                else
                {
                    int extraCards =
                        GameManager.Instance.LocalPlayerSave.SpItems
                            .FindAll(
                                x => x == SpItem.StoreCardSlot
                            )
                            .Count;

                    SeedBank.Instance.CardNum =
                        GameManager.Instance.LocalPlayerSave.CardSlotNum +
                        extraCards;
                }
            }

            if (
                needloadLv &&
                LV.Instance.CurrLVType == LVType.Normal
            )
            {
                MapManager.Instance.CreateAllMower();
            }
        }

        if (GameManager.Instance.isServer)
        {
            BattlePlayerList.Instance.LoadAllSeedBank(
                players,
                cardNums
            );
        }

        UIManager.Instance.BattleUI?.SetActive(true);
        UIManager.Instance.OpenBattleUI();

        if (!GameManager.Instance.isClient)
        {
            SumWeight();

            FlagMeter.Instance.CreateFlag(
                AllWeight
            );

            FlagMeter.Instance.SetLvlName(
                LV.Instance.LvName,
                LevelSelector.Instance.IsEasy
            );
        }

        SeedBank.Instance.SpawnCardSlot();

        if (
            CurrSubLv == 0 ||
            CurrSubLv > 0 && LvTotalTime > 0
        )
        {
            Vector2 position = new(-3.5f, 0f);

            if (LV.Instance.CurrLVType == LVType.PvP)
            {
                CameraControl.Instance.SetPosition(
                    Vector2.zero
                );

                StartCoroutine(
                    WaitTimeDo(
                        LVStartCameraAction,
                        1f
                    )
                );

                PlayChooseCardBgAudio();
            }
            else if (
                LV.Instance.CurrLVType == LVType.IZombie
            )
            {
                LoadFixedCard(
                    LV.Instance.FixedCard
                );

                LVStartEFOver();
                LV.Instance.SpSeedBankMove();

                MapManager.Instance.CreateAllBrain();
                MapManager.Instance.LoadIZombieMap();

                CameraControl.Instance.SetPosition(
                    position
                );
            }
            else if (
                LV.Instance.CurrLVType ==
                LVType.VaseBreaker
            )
            {
                LoadFixedCard(
                    LV.Instance.FixedCard
                );

                LVStartEFOver();

                if (LV.Instance.FixedCard.Count > 0)
                    LV.Instance.SpSeedBankMove();

                MapManager.Instance.LoadVaseBreaker();

                CameraControl.Instance.SetPosition(
                    position
                );
            }
            else if (
                LV.Instance.CurrBankType ==
                    BankType.ConveryorBelt ||
                LV.Instance.CurrBankType ==
                    BankType.SlotMachine ||
                LV.Instance.LvSpStates.Contains(
                    LVSpState.RainPlant
                )
            )
            {
                LoadFixedCard(
                    LV.Instance.FixedCard
                );

                CameraControl.Instance.SetPosition(
                    position
                );

                LV.Instance.SpSeedBankMove();

                LVStartCameraBackAction();
                PlayChooseCardBgAudio();
            }
            else
            {
                UnityEngine.Random.InitState(lvSeed);

                PlayChooseCardBgAudio();
                ZombieManager.Instance.ShowZombie();

                CameraControl.Instance.MoveForLVStart(
                    LVStartCameraAction
                );
            }
        }

        if (CurrSubLv > 0 && LvTotalTime == 0)
        {
            LoadFixedCard(
                LV.Instance.FixedCard
            );

            LoadFixedCard(
                SeedBank.Instance.LastSelectCard,
                false
            );

            LVStartEFOver();
            LV.Instance.SpSeedBankMove();
        }
    }

    private void PlayChooseCardBgAudio()
    {
        if (GameManager.Instance.isClient)
            StartCoroutine(
                WaitTimeDo(
                    PLayChooseCard,
                    0.1f
                )
            );
        else
            PLayChooseCard();
    }

    private void PLayChooseCard()
    {
        AudioManager.Instance.PlayBgAudio(
            SkyManager.Instance.GetIsDay()
                ? BgmType.ChooseYourSeeds
                : BgmType.NightChooseSeeds
        );
    }

    public void QuitBattleGame(
        bool StartScenceAnim = true)
    {
        if (GameManager.Instance.isServer)
        {
            SocketServer.Instance.LoadLv(
                new LoadLVBag
                {
                    LoadType = 2
                }
            );
        }

        ResetScence();

        LevelSelector.Instance.LoadLastLv();
        PvPSelector.Instance.ResetPvPInfo();
        CreatePanel.Instance.LvRest();

        AudioManager.Instance.PlayBgAudio(
            BgmType.Nor
        );

        CameraControl.Instance.SetPosition(
            new Vector2(0f, -30f)
        );

        StartSceneManager.Instance.LoadStartScence(
            StartScenceAnim
        );
    }

    public void ReStartGame()
    {
        // StatsManager puede no existir en determinadas escenas.
        // El reinicio no debe detenerse por eso.
        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.AddStatsNum(
                StatsEnum.ReStartNum
            );
        }

        if (GameManager.Instance.isServer)
        {
            SocketServer.Instance.LoadLv(
                new LoadLVBag
                {
                    LoadType = 1
                }
            );
        }

        ResetScence();
        LevelSelector.Instance.StartCurrGame();
    }

    private void ResetScence()
    {
        ResetLv();

        PassTime = 0;
        CurrSubLv = 0;

        GobalLight.Instance.gobalLight.intensity = 1f;

        AudioManager.Instance.ChangeMapReset();

        SeedBank.Instance.isCanClick = true;

        UIManager.Instance.StopLVStartEF();

        UIManager.Instance.OverPanel.gameObject.SetActive(
            false
        );

        PlantManager.Instance.LvReset();
        ZombieManager.Instance.LvReset();
        SkyManager.Instance.ResetAll();
        MapManager.Instance.ResetScence();
        PoolManager.Instance.ClearPool();

        UIManager.Instance.LogPanel.Close();

        UIManager.Instance.BattleUI?.SetActive(false);

        Timetable.Instance.LvReset();
        EffectPanel.Instance.LvReset();

        UIManager.Instance.LastStandBtn.gameObject.SetActive(
            false
        );

        for (
            int i = transform.childCount - 1;
            i >= 0;
            i--
        )
        {
            Destroy(
                transform.GetChild(i).gameObject
            );
        }

        StopAllCoroutines();

        GameManager.Instance.SaveSAInfo();

        LvItemManager.Instance.LvReset();
        BigTitle.Instance.LvReset();
    }

    private void ResetLv()
    {
        currLVWave = 0;
        BIGwaveNum = 0;
        AllWeight = 0;
        IsRestTime = false;
        isBigWave = false;
        LastStandNoCd = false;
        LvStartAction = null;
        CurrLVState = LVState.Start;

        PlayerManager.Instance.ClearMoney();
        CobCannonTarget.Instance.StopAim();
        Shovel.Instance.CancelShovel();

        SeedBank.Instance.ClearCardSlot();
        SeedBank.Instance.StartMoveBack(false);

        SetChoosers(false, false);

        NextWaveBtn.Instance.CloseBtn();
        SkipRestBtn.Instance.CloseBtn();

        AcvmentManager.Instance.LVResetThis();

        UIManager.Instance.SetPanel.LvReset();

        SkyManager.Instance.StopTime();
        PlayerManager.Instance.LvReset();
    }

    private void SetChoosers(
        bool seedChooser,
        bool zombieChooser,
        bool start = false)
    {
        if (SeedChooser.Instance != null)
        {
            SeedChooser.Instance.StopAllCoroutines();
            SeedChooser.Instance.ResetThis();
            SeedChooser.Instance.gameObject.SetActive(
                seedChooser
            );

            if (seedChooser && start)
                SeedChooser.Instance.StartMove();
        }

        if (ZombieChooser.Instance != null)
        {
            ZombieChooser.Instance.StopAllCoroutines();
            ZombieChooser.Instance.ResetThis();
            ZombieChooser.Instance.gameObject.SetActive(
                zombieChooser
            );

            if (zombieChooser && start)
                ZombieChooser.Instance.StartMove();
        }
    }

    public void ZombieGameOver(Vector2 overPos)
    {
        if (LvDontFail)
            return;

        if (GameManager.Instance.isServer)
        {
            SocketServer.Instance.GameOver(
                new GameOver
                {
                    pos = overPos
                }
            );
        }

        StatsManager.Instance.AddStatsNum(
            StatsEnum.FailNum
        );

        StopAllCoroutines();

        CameraControl.Instance.GoOtherYard(
            MapManager.Instance.mapList.IndexOf(
                MapManager.Instance.GetCurrMap(overPos)
            )
        );

        Shovel.Instance.CancelShovel();
        SkyManager.Instance.ResetAll();
        MapManager.Instance.GameOverPause();
        PlantManager.Instance.GameOverPause();
        ZombieManager.Instance.GameOverPause();
        AudioManager.Instance.StopBgAudio();
        SeedBank.Instance.AllCancelPlace(true);

        PlayEffect(
            GameManager.Instance.AudioConf.GameOver
        );

        PlayEffect(
            GameManager.Instance.AudioConf.EatPlant1
        );

        UIManager.Instance.OverPanel.Over();
    }

    public void GameOver2()
    {
        if (LvDontFail)
            return;

        StopAllCoroutines();

        Shovel.Instance.CancelShovel();
        SkyManager.Instance.ResetAll();
        MapManager.Instance.GameOverPause();
        PlantManager.Instance.GameOverPause();
        ZombieManager.Instance.GameOverPause();
        AudioManager.Instance.StopBgAudio();
        SeedBank.Instance.AllCancelPlace(true);

        PlayEffect(
            GameManager.Instance.AudioConf.GameOver
        );

        OverPanelEvent();
    }

    public void PvPGameOver(
        Vector2 overPos,
        bool isRedFail)
    {
        if (LvDontFail)
            return;

        if (GameManager.Instance.isServer)
        {
            SocketServer.Instance.GameOver(
                new GameOver
                {
                    pos = overPos,
                    isRedFail = isRedFail
                }
            );
        }

        StopAllCoroutines();

        CameraControl.Instance.GoOtherYard(
            MapManager.Instance.mapList.IndexOf(
                MapManager.Instance.GetCurrMap(overPos)
            )
        );

        Shovel.Instance.CancelShovel();
        SkyManager.Instance.ResetAll();
        MapManager.Instance.GameOverPause();
        PlantManager.Instance.GameOverPause();
        ZombieManager.Instance.GameOverPause();
        AudioManager.Instance.StopBgAudio();
        SeedBank.Instance.AllCancelPlace(true);

        if (PvPSelector.Instance.LocalIsRedTeam)
        {
            PlayEffect(
                isRedFail
                    ? GameManager.Instance.AudioConf.GameOver
                    : GameManager.Instance.AudioConf.GameWin
            );
        }
        else
        {
            PlayEffect(
                isRedFail
                    ? GameManager.Instance.AudioConf.GameWin
                    : GameManager.Instance.AudioConf.GameOver
            );
        }

        UIManager.Instance.LogPanel.DisplayLog(
            isRedFail
                ? "蓝方获得胜利！"
                : "红方获得胜利！",
            () =>
            {
                if (!GameManager.Instance.isClient)
                    QuitBattleGame();
            }
        );

        UIManager.Instance.LogPanel.ButtonText.text =
            "返回主界面";

        if (GameManager.Instance.isClient)
        {
            UIManager.Instance.LogPanel.Button.gameObject
                .SetActive(false);
        }
    }

    public void OverPanelEvent()
    {
        if (GameManager.Instance.isClient)
            return;

        UIManager.Instance.LogPanel.DisplayLog(
            "游戏结束。",
            () =>
            {
                if (!GameManager.Instance.isClient)
                    ReStartGame();
            }
        );

        UIManager.Instance.LogPanel.ButtonText.text =
            "重新开始";
    }

    public void GameWin(Booty booty)
    {
        CurrLVState = LVState.End;

        if (LV.Instance.CurrLVType == LVType.PvP)
        {
            QuitBattleGame();
            return;
        }

        OnlyBooty = null;

        ResetScence();

        AwardScence.Instance.JumpTo(
            booty,
            () =>
            {
                if (GameManager.Instance.isClient)
                    return;

                if (
                    LV.Instance.CurrLvId % 10000 <
                    GameManager.Instance.SelectedStone
                        .AdventureLvNum
                )
                {
                    LevelSelector.Instance.LoadLastLv();
                    StartGame(
                        null,
                        LV.Instance.CurrLvId
                    );
                }
                else
                {
                    QuitBattleGame();
                }
            }
        );
    }

    public void StartRunLv()
    {
        SeedBank.Instance.isCanClick = false;

        if (LV.Instance.CurrLVType == LVType.PvP)
        {
            StartCoroutine(
                WaitTimeDo(
                    LVStartCameraBackAction,
                    1f
                )
            );
        }
        else
        {
            CameraControl.Instance.MoveBackForLVStart(
                LVStartCameraBackAction
            );
        }
    }

    public void LVStartCameraAction()
    {
        SeedBank.Instance.StartMove(
            LV.Instance.CurrSeedBankType
        );

        SeedBankType type =
            LV.Instance.CurrSeedBankType;

        bool useSeedChooser =
            type == SeedBankType.SunBank ||
            type == SeedBankType.SunAndMoonBank;

        bool useZombieChooser =
            type == SeedBankType.MoonBank ||
            type == SeedBankType.SunAndMoonBank;

        SetChoosers(
            useSeedChooser,
            useZombieChooser,
            true
        );

        LoadFixedCard(
            LV.Instance.FixedCard
        );
    }

    private void LoadFixedCard(
        List<CardType> cards,
        bool canAddSlot = true)
    {
        if (!GameManager.Instance.isOnline)
        {
            SeedBank.Instance.AddCards(
                cards,
                true
            );

            return;
        }

        if (!GameManager.Instance.isServer)
            return;

        List<string> players =
            SpectatorList.Instance.GetNoSpectatorPlayerList();

        List<List<CardType>> splitCards =
            MyTool.SplitByNumberOfLists(
                cards,
                players.Count
            );

        for (
            int i = 1;
            i < splitCards.Count;
            i++
        )
        {
            SocketServer.Instance.SendAddCard(
                new AddCardBag
                {
                    CardTypes = splitCards[i]
                },
                players[i]
            );
        }

        SeedBank.Instance.AddCards(
            splitCards[0],
            canAddSlot
        );
    }

    public void LVStartCameraBackAction()
    {
        if (LV.Instance.CurrLVType == LVType.Normal)
            ZombieManager.Instance.ClearAllZombie();

        if (
            LV.Instance.LvSpStates.Contains(
                LVSpState.LastStand
            )
        )
        {
            LVStartEFOver();
        }
        else
        {
            UIManager.Instance.ShowLVStartEF();
        }
    }

    public void LVStartEFOver()
    {
        startRunLV();

        SeedBank.Instance.isCanClick = true;
        CurrLVState = LVState.Fighting;

        if (
            LV.Instance.CurrLVType == LVType.IZombie ||
            LV.Instance.CurrLVType == LVType.VaseBreaker
        )
        {
            AudioManager.Instance.PlayBgAudio(
                BgmType.Cerebrawl
            );
        }
        else if (SkyManager.Instance.GetIsDay())
        {
            AudioManager.Instance.PlayBgAudio(
                LV.Instance.DayBgm
            );
        }
        else
        {
            AudioManager.Instance.PlayBgAudio(
                LV.Instance.NightBgm
            );
        }

        LV.Instance.StartAction?.Invoke();
        LvStartAction?.Invoke();
    }

    public void AddLVStartActionListenr(
        UnityAction action)
    {
        LvStartAction =
            (UnityAction)Delegate.Combine(
                LvStartAction,
                action
            );
    }

    private void OnAllZombieDeadAction()
    {
        CurrLVWave++;

        ZombieManager.Instance
            .RemoveAllZombieDeadAction(
                OnAllZombieDeadAction
            );

        if (AutoNextWaveCoroutine != null)
        {
            StopCoroutine(
                AutoNextWaveCoroutine
            );
        }

        AutoNextWaveCoroutine = null;
    }

    public void StartLastStand()
    {
        RunLv();

        LastStandNoCd = false;

        SeedBank.Instance.StartAllCD();

        UIManager.Instance.LastStandBtn.gameObject
            .SetActive(false);

        if (GameManager.Instance.isServer)
        {
            SocketServer.Instance.BigWaveComing(
                new WaveComing
                {
                    WaveType = 6
                }
            );
        }
    }

    private void startRunLV()
    {
        if (LV.Instance.CurrLVType == LVType.PvP)
        {
            SkyManager.Instance.StartTime();
            return;
        }

        if (
            LV.Instance.CurrLVType == LVType.IZombie ||
            LV.Instance.CurrLVType == LVType.VaseBreaker
        )
        {
            return;
        }

        if (
            LV.Instance.LvSpStates.Contains(
                LVSpState.LastStand
            )
        )
        {
            LastStandNoCd = true;

            SeedBank.Instance.ClearAllCD();

            ChatInput.Instance.AddMessage(
                "开始建造你的阵型！！"
            );

            if (!GameManager.Instance.isClient)
            {
                UIManager.Instance.LastStandBtn.gameObject
                    .SetActive(true);
            }
        }
        else
        {
            RunLv();
        }
    }

    private void RunLv()
    {
        SkyManager.Instance.StartTime();

        if (GameManager.Instance.isClient)
            return;

        isWaitSpawn = true;
        waitingGoNextWave = false;

        if (CurrSubLv == 0)
        {
            float time = UnityEngine.Random.Range(
                LV.Instance.SetupTime.x,
                LV.Instance.SetupTime.y
            );

            StartCoroutine(
                WaitTimeDo(
                    () =>
                    {
                        AllTime();
                        isWaitSpawn = false;
                        AutoStartNextWave();

                        PlayEffect(
                            GameManager.Instance
                                .AudioConf.Awooga
                        );
                    },
                    time
                )
            );
        }
        else
        {
            IsRestTime = true;

            SkipRestBtn.Instance.CanSkipWave();

            if (RestSpawnCoroutine != null)
            {
                StopCoroutine(
                    RestSpawnCoroutine
                );
            }

            RestSpawnCoroutine =
                StartCoroutine(
                    RestZombie()
                );
        }
    }

    private void AutoStartNextWave()
    {
        StartCoroutine(
            NextWave()
        );
    }

    public void OnZombieDeadEvent()
    {
        CheckNextWaveBtn();
    }

    private void CheckNextWaveBtn()
    {
        int limit =
            MapManager.Instance.mapList.Count * 2;

        if (
            LV.Instance.Weights.Count > 0 &&
            CanHandNextWave &&
            ZombieManager.Instance.GetZombieNum() <= limit &&
            CurrLVWave <
                LV.Instance.Weights[0].Count - 1 &&
            !LastStandNoCd
        )
        {
            NextWaveBtn.Instance.CanNextWave();
        }
    }

    public void NextWaveBtnEvent()
    {
        if (CanHandNextWave)
            OnAllZombieDeadAction();
    }

    private IEnumerator AutoNextWave(float time)
    {
        CanHandNextWave = false;

        int handTime =
            15 +
            MapManager.Instance.mapList.Count * 5;

        if (time > handTime)
        {
            yield return new WaitForSeconds(
                handTime
            );

            CanHandNextWave = true;
            CheckNextWaveBtn();

            yield return new WaitForSeconds(
                time - handTime
            );
        }
        else
        {
            yield return new WaitForSeconds(
                time
            );
        }

        CanHandNextWave = false;

        NextWaveBtn.Instance.CloseBtn();

        OnAllZombieDeadAction();
    }

    private IEnumerator NextWave()
    {
        isBigWave = false;
        CanHandNextWave = false;

        NextWaveBtn.Instance.CloseBtn();

        List<int> waveWeights = new();

        for (
            int i = 0;
            i < MapManager.Instance.mapList.Count;
            i++
        )
        {
            waveWeights.Add(
                LV.Instance.Weights[i][CurrLVWave]
            );
        }

        bool bigWave =
            CurrLVWave ==
            LV.Instance.BigWaveNum[BIGwaveNum];

        if (GameManager.Instance.isServer)
        {
            WaveComing wave = new();

            if (bigWave)
            {
                wave.WaveType =
                    BIGwaveNum ==
                    LV.Instance.BigWaveNum.Count - 1
                        ? 2
                        : 1;

                wave.WaitTime = 4f;
            }

            SocketServer.Instance.BigWaveComing(
                wave
            );
        }

        if (bigWave)
        {
            if (
                BIGwaveNum ==
                LV.Instance.BigWaveNum.Count - 1
            )
            {
                UIManager.Instance.ShowFinalWaveEF();
            }
            else
            {
                UIManager.Instance.ShowBigWaveEF();
            }

            isBigWave = true;

            yield return new WaitForSeconds(4f);

            PlayEffect(
                GameManager.Instance.AudioConf.Awooga
            );

            FlagMeter.Instance.FlagRise(
                BIGwaveNum++
            );

            for (
                int i = 0;
                i < MapManager.Instance.mapList.Count;
                i++
            )
            {
                Vector3 mapPos =
                    MapManager.Instance.mapList[i]
                        .transform.position;

                for (
                    int j = 0;
                    j <
                    LV.Instance.BigWaveFixedZombie.Count;
                    j++
                )
                {
                    ZombieManager.Instance
                        .UpdateZombieOnRandomLine(
                            LV.Instance
                                .BigWaveFixedZombie[j],
                            mapPos
                        );
                }
            }

            LV.Instance.BigWaveAction?.Invoke();

            MapManager.Instance.AllGraveOutZombie();
            MapManager.Instance.AllMapWaterOutZombie();
        }

        float spawnTime = 5f;

        while (true)
        {
            if (StopSpawn)
            {
                Debug.Log("等待中");

                yield return new WaitForSeconds(
                    1f
                );

                continue;
            }

            int totalWeight = 0;

            foreach (int weight in waveWeights)
                totalWeight += weight;

            if (totalWeight == 0)
                break;

            int attempts = 0;

            for (
                int i = 0;
                i < waveWeights.Count;
                i++
            )
            {
                if (waveWeights[i] == 0)
                    continue;

                int index;

                while (true)
                {
                    attempts++;

                    index =
                        UnityEngine.Random.Range(
                            0,
                            LV.Instance.ZombieTypes[i].Count
                        );

                    ZombieType type =
                        LV.Instance.ZombieTypes[i][index];

                    if (
                        (
                            ZombieManager.Instance
                                .GetZombieWeight(type) <=
                            waveWeights[i] ||
                            !LV.Instance.WeightLimit
                        ) &&
                        (
                            CurrLVWave >=
                                LV.Instance.ProphaseLimitWave ||
                            !LV.Instance
                                .ProphaseLimitZombie
                                .Contains(type)
                        )
                    )
                    {
                        break;
                    }

                    if (attempts > 10)
                    {
                        index = 0;
                        break;
                    }
                }

                if (
                    i >=
                    MapManager.Instance.mapList.Count
                )
                {
                    break;
                }

                Vector3 position =
                    MapManager.Instance.mapList[i]
                        .transform.position;

                ZombieType zombieType =
                    LV.Instance.ZombieTypes[i][index];

                bool spawned = false;

                if (
                    MapManager.Instance.mapList[i]
                        .SpSpawnZombie(
                            out Vector2 pos,
                            out int spCode
                        )
                )
                {
                    spawned = true;

                    if (spCode == 0)
                    {
                        ZombieManager.Instance
                            .MapSPZombie(
                                zombieType,
                                pos,
                                MapManager.Instance.mapList[i]
                            );
                    }
                    else if (spCode == 1)
                    {
                        ZombieManager.Instance.OutGround(
                            zombieType,
                            pos,
                            null,
                            needArm: false,
                            isHyp: false,
                            purple: false
                        );
                    }
                }

                int spawnCode =
                    CurrLVWave <
                        LV.Instance.ProphaseLimitWave &&
                    attempts <= 10
                        ? 1
                        : 0;

                if (
                    !spawned &&
                    LV.Instance.LvSpStates.Contains(
                        LVSpState.BungiMode
                    ) &&
                    !ZombieManager.Instance.CantBungiSky
                        .Contains(zombieType) &&
                    UnityEngine.Random.Range(
                        0f,
                        1f
                    ) <= LV.Instance.BungiSpRate &&
                    ZombieManager.Instance
                        .UpdateBungiZombieOnRandomLine(
                            zombieType,
                            position
                        )
                )
                {
                    spawned = true;
                }

                if (
                    !spawned &&
                    ZombieManager.Instance
                        .UpdateZombieOnRandomLine(
                            zombieType,
                            position,
                            spawnCode
                        )
                )
                {
                    spawned = true;
                }

                if (spawned || attempts > 10)
                {
                    int weight = Mathf.Min(
                        ZombieManager.Instance
                            .GetZombieWeight(
                                zombieType
                            ),
                        waveWeights[i]
                    );

                    waveWeights[i] -= weight;

                    FlagMeter.Instance.UpdateHead(
                        weight
                    );
                }
                else
                {
                    i--;
                }
            }

            if (
                MapManager.Instance.mapList.Count == 0
            )
            {
                break;
            }

            float delay =
                UnityEngine.Random.Range(
                    0.2f,
                    1.5f
                );

            if (spawnTime >= delay)
            {
                spawnTime -= delay;

                yield return new WaitForSeconds(
                    delay
                );
            }
        }

        ZombieManager.Instance.AddAllZombieDeadAction(
            OnAllZombieDeadAction
        );

        AutoNextWaveCoroutine =
            StartCoroutine(
                AutoNextWave(
                    GetAutoTime(waveWeights) +
                    spawnTime
                )
            );
    }

    private IEnumerator RestZombie()
    {
        do
        {
            float sunNum =
                PlayerManager.Instance.GetSunNum(
                    true,
                    null
                );

            yield return new WaitForSeconds(
                20f - sunNum / 1000f
            );

            if (!IsRestTime)
                break;

            int item =
                (int)(sunNum / 500f) + 1;

            List<int> waveWeights = new();

            for (
                int i = 0;
                i < MapManager.Instance.mapList.Count;
                i++
            )
            {
                waveWeights.Add(item);
            }

            while (true)
            {
                int totalWeight = 0;

                foreach (int weight in waveWeights)
                    totalWeight += weight;

                if (totalWeight == 0)
                    break;

                int attempts = 0;

                for (
                    int i = 0;
                    i < waveWeights.Count;
                    i++
                )
                {
                    if (waveWeights[i] == 0)
                        continue;

                    int index;

                    while (true)
                    {
                        attempts++;

                        index =
                            UnityEngine.Random.Range(
                                0,
                                LV.Instance.ZombieTypes[i].Count
                            );

                        ZombieType type =
                            LV.Instance.ZombieTypes[i][index];

                        if (
                            ZombieManager.Instance
                                .GetZombieWeight(type) <=
                            waveWeights[i] ||
                            !LV.Instance.WeightLimit
                        )
                        {
                            break;
                        }

                        if (attempts > 10)
                        {
                            index = 0;
                            break;
                        }
                    }

                    if (
                        i >=
                        MapManager.Instance.mapList.Count
                    )
                    {
                        break;
                    }

                    Vector3 position =
                        MapManager.Instance.mapList[i]
                            .transform.position;

                    ZombieType typeToSpawn =
                        LV.Instance.ZombieTypes[i][index];

                    bool spawned = false;

                    if (
                        MapManager.Instance.mapList[i]
                            .SpSpawnZombie(
                                out Vector2 pos,
                                out int spCode
                            )
                    )
                    {
                        spawned = true;

                        if (spCode == 0)
                        {
                            ZombieManager.Instance
                                .MapSPZombie(
                                    typeToSpawn,
                                    pos,
                                    MapManager.Instance.mapList[i]
                                );
                        }
                        else if (spCode == 1)
                        {
                            ZombieManager.Instance.OutGround(
                                typeToSpawn,
                                pos,
                                null,
                                needArm: false,
                                isHyp: false,
                                purple: false
                            );
                        }
                    }

                    if (
                        !spawned &&
                        ZombieManager.Instance
                            .UpdateZombieOnRandomLine(
                                typeToSpawn,
                                position
                            )
                    )
                    {
                        spawned = true;
                    }

                    if (spawned || attempts > 10)
                    {
                        int weight = Mathf.Min(
                            ZombieManager.Instance
                                .GetZombieWeight(
                                    typeToSpawn
                                ),
                            waveWeights[i]
                        );

                        waveWeights[i] -= weight;
                    }
                    else
                    {
                        i--;
                    }
                }

                yield return new WaitForSeconds(
                    UnityEngine.Random.Range(
                        2f,
                        8f
                    )
                );
            }
        }
        while (IsRestTime);
    }

    private int GetAutoTime(
        List<int> waveWeights)
    {
        int totalWeight = 0;

        foreach (int weight in waveWeights)
            totalWeight += weight;

        totalWeight +=
            totalWeight / 2 +
            LV.Instance.NextWaveLossTime;

        return Mathf.Clamp(
            Mathf.Max(
                totalWeight,
                50 +
                LV.Instance.NextWaveLossTime
            ),
            10,
            85
        );
    }

    private void AllTime()
    {
        int totalTime =
            CurrSubLv == 0
                ? (int)LV.Instance.SetupTime.y
                : 0;

        totalTime +=
            LV.Instance.BigWaveNum.Count * 4;

        totalTime +=
            LV.Instance.Weights[0].Count * 5;

        for (
            int i = 0;
            i < LV.Instance.Weights[0].Count;
            i++
        )
        {
            List<int> weights = new();

            for (
                int j = 0;
                j < MapManager.Instance.mapList.Count;
                j++
            )
            {
                weights.Add(
                    LV.Instance.Weights[j][i]
                );
            }

            totalTime += GetAutoTime(weights);
        }

        LvTotalTime = totalTime;
    }

    private IEnumerator WaitTimeDo(
        UnityAction action,
        float time)
    {
        yield return new WaitForSeconds(time);
        action?.Invoke();
    }

    public void ZombieNumChange(int num)
    {
        if (!IsRestTime)
            return;

        if (num > 0)
            SkipRestBtn.Instance.CloseBtn();
        else if (LvTotalTime > 10)
            SkipRestBtn.Instance.CanSkipWave();
    }

    public void SumWeight()
    {
        if (LV.Instance.Weights.Count == 0)
        {
            AllWeight = 0;
            return;
        }

        AllWeight = 0;
        int bigWave = 0;

        for (
            int i = 0;
            i < LV.Instance.Weights[0].Count;
            i++
        )
        {
            if (
                bigWave <
                    LV.Instance.BigWaveNum.Count &&
                i ==
                    LV.Instance.BigWaveNum[bigWave]
            )
            {
                bigWave++;
                continue;
            }

            for (
                int j = 0;
                j < LV.Instance.Weights.Count;
                j++
            )
            {
                AllWeight +=
                    LV.Instance.Weights[j][i];
            }
        }
    }

    public void SpawnBooty(
        Vector3 pos,
        SynBooty syn1 = null)
    {
        if (BootyIsAppeared)
            return;

        BootyIsAppeared = true;

        if (
            PlayerManager.Instance.GetSunNum(
                true,
                GameManager.Instance.LocalPlayerSave
                    .playerName
            ) >= 10000f &&
            !PlayerManager.Instance.SunInfinite &&
            LV.Instance.CurrLVType == LVType.Normal
        )
        {
            AcvmentManager.Instance.GetAchievement(
                Acvname.SunFull
            );
        }

        if (
            MapManager.Instance.mapList.Count >= 3 &&
            PlayerList.Instance.PlayerNum >= 4
        )
        {
            AcvmentManager.Instance.GetAchievement(
                Acvname.UnitAsOne
            );
        }

        StatsManager.Instance.AddStatsNum(
            StatsEnum.WinNum
        );

        Booty booty =
            Instantiate(
                GameManager.Instance.GameConf.Moneybag
            )
            .GetComponent<Booty>();

        booty.transform.position = pos;

        if (
            GameManager.Instance.isClient &&
            syn1 != null
        )
        {
            AwardScence.Instance.LoadText(
                syn1.info1,
                syn1.info2,
                syn1.info3
            );

            booty.InitThis(syn1);
        }
        else
        {
            booty.InitThis();
        }

        Grid grid =
            MapManager.Instance.GetGridByWorldPos(
                booty.transform.position
            );

        if (grid == null)
            return;

        if (
            Vector2.Distance(
                booty.transform.position,
                grid.Position
            ) > 0.8f
        )
        {
            booty.transform.position =
                grid.Position;
        }

        OnlyBooty = booty;

        if (GameManager.Instance.isServer)
        {
            SocketServer.Instance.SendSynBooty(
                new SynBooty
                {
                    isSpawn = true,
                    pos = pos,
                    BootyPlant = LV.Instance.BootyPlant,
                    sprite = LV.Instance.BootySprite,
                    info1 = AwardScence.Instance.Title.text,
                    info2 = AwardScence.Instance.ItemName.text,
                    info3 = AwardScence.Instance.Content.text
                }
            );
        }
    }

    public void SettleLv(Vector3 pos)
    {
        if (
            LV.Instance.SubLvs.Count <= CurrSubLv
        )
        {
            SpawnBooty(pos);
            return;
        }

        if (GameManager.Instance.isServer)
        {
            SocketServer.Instance.BigWaveComing(
                new WaveComing
                {
                    WaveType = 4
                }
            );
        }

        waitingGoNextWave = true;

        PlayEffect(
            GameManager.Instance.AudioConf.HugeWave
        );

        AudioManager.Instance.FadeBgAndPlayNew(
            BgmType.Nope,
            faster: true
        );

        BigTitle.Instance.DisPlayInfo(
            "更多的僵尸要来了！",
            4,
            GoNextSubLv
        );
    }

    private void GoNextSubLv()
    {
        ResetLv();

        LV.Instance.ReadSubLv(
            LV.Instance.SubLvs[CurrSubLv]
        );

        CurrSubLv++;

        StartLv(
            null,
            0,
            false
        );
    }

    public void SkipRest()
    {
        if (LvTotalTime <= 5)
            return;

        if (GameManager.Instance.isServer)
        {
            SocketServer.Instance.BigWaveComing(
                new WaveComing
                {
                    WaveType = 5
                }
            );
        }

        FadeEffPanel.Instance.FadeOutIn(
            () =>
            {
                SkyManager.Instance.DirectSetTime(
                    SkyManager.Instance.Time +
                    LvTotalTime -
                    5
                );

                LvTotalTime = 5;
            }
        );
    }

    public void ClientShowBigWave(
        WaveComing bigWave)
    {
        if (bigWave.WaveType == 0)
        {
            isBigWave = false;
        }
        else if (
            bigWave.WaveType == 1 ||
            bigWave.WaveType == 2
        )
        {
            isBigWave = true;

            if (bigWave.WaveType == 1)
                UIManager.Instance.ShowBigWaveEF();
            else
                UIManager.Instance.ShowFinalWaveEF();

            StartCoroutine(
                WaitTimeDo(
                    () =>
                    {
                        PlayEffect(
                            GameManager.Instance
                                .AudioConf.Awooga
                        );

                        FlagMeter.Instance.FlagRise(
                            BIGwaveNum++
                        );
                    },
                    bigWave.WaitTime
                )
            );
        }
        else if (bigWave.WaveType == 3)
        {
            LvSpawnisOver = true;
        }
        else if (bigWave.WaveType == 5)
        {
            FadeEffPanel.Instance.FadeOutIn(
                () => LvTotalTime = 5
            );
        }
        else if (bigWave.WaveType == 6)
        {
            LastStandNoCd = false;
            SeedBank.Instance.StartAllCD();
        }
    }
}