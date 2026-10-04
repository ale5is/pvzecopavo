using System.Collections;
using System.Collections.Generic;
using SaveClass;
using UnityEngine;
using UnityEngine.UI;

public class SetPanel : MonoBehaviour
{
    [Header("Audio")]
    public Slider MusicSlider;
    public Slider SoundSlider;

    [Header("Textos")]
    public Text ContinueText;
    public Text ContinueText2;
    public Text FrameText;
    public Text ResolutionText;

    [Header("Opciones de batalla")]
    public GameObject BackMenu;
    public GameObject Restart;
    public GameObject ClientQuit;
    public GameObject MoreOption;

    [Header("Más opciones")]
    public GameObject AllMoreOptionPage;
    public GameObject MOMainPage;
    public GameObject MOHardPage;
    public GameObject MOGraphicsPage;
    public GameObject MOQuickChatPage;
    public GameObject MOStatisticsPage;

    [Header("Gráficos")]
    public GameObject GrapNoAndroid;
    public GameObject FrameDisObj;

    [Header("Indicadores")]
    public Image RainFogBt;
    public Image VsyncBt;
    public Image SunUpBt;
    public Image NormalSunBt;
    public Image CdDownBt;
    public Image LightningBt;
    public Image SlowSunBt;
    public Image CardSlectorBt;
    public Image FrameDisplayBt;
    public Image OpenQChatBt;
    public Image FullScreen;
    public Image X2Speed;

    [Header("Chat rápido")]
    public InputField QuickChat1;
    public InputField QuickChat2;
    public InputField QuickChat3;

    [Header("Estado")]
    public bool isOpen;
    public int FrameType;
    public bool is1080P;
    public bool is2xSpeed;
    public bool isVsync;
    public bool isOpenMapFade;
    public bool isDisFrame;
    public bool OpenQuickChat;

    private bool isFullScreen;
    private bool QuickChatLimit;
    private bool wasBattle;

    private static readonly WaitForSeconds QuickChatWait =
        new WaitForSeconds(1f);

    private void Start()
    {
        if (GameManager.Instance.isAndroid)
            GrapNoAndroid.SetActive(false);
    }

    private void PlayButtonSound()
    {
        AudioManager.Instance.PlayEFAudio(
            GameManager.Instance.AudioConf.ButtonClick,
            transform.position,
            true
        );
    }

    private void SetActive(GameObject obj, bool value)
    {
        obj.SetActive(value);
    }

    private void SetActive(Image image, bool value)
    {
        image.gameObject.SetActive(value);
    }

    private void SetCheck(Image image, bool value)
    {
        image.sprite = value
            ? NormalSprite.Instance.CheckBoxYes
            : NormalSprite.Instance.CheckBox;
    }

    private void SetMorePage(GameObject page)
    {
        MOMainPage.SetActive(page == MOMainPage);
        MOHardPage.SetActive(page == MOHardPage);
        MOGraphicsPage.SetActive(page == MOGraphicsPage);
        MOQuickChatPage.SetActive(page == MOQuickChatPage);
        MOStatisticsPage.SetActive(page == MOStatisticsPage);
    }

    private void SetGameSpeed()
    {
        if (GameManager.Instance.isOnline)
            return;

        Time.timeScale = is2xSpeed ? 2f : 1f;
    }

    public void ShowPanel(bool show, bool battle)
    {
        isOpen = show;

        if (show)
            wasBattle = battle;

        PlayButtonSound();

        if (!show)
        {
            GameManager.Instance.SaveSetting();

            if (!GameManager.Instance.isOnline)
            {
                SetGameSpeed();

                if (wasBattle)
                    AudioManager.Instance.ResumeBgAudio();
            }

            AllMoreOptionPage.SetActive(false);
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (battle)
        {
            if (!GameManager.Instance.isOnline)
            {
                Time.timeScale = 0f;
                AudioManager.Instance.StopBgAudio();

                if (GameManager.Instance.AudioConf.Pause != null)
                {
                    AudioManager.Instance.PlayEFAudio(
                        GameManager.Instance.AudioConf.Pause,
                        transform.position,
                        true
                    );
                }
            }

            X2Speed.gameObject.SetActive(
                !GameManager.Instance.isOnline
            );

            SetCheck(X2Speed, is2xSpeed);

            if (GameManager.Instance.isClient)
            {
                ClientQuit.SetActive(true);
                BackMenu.SetActive(false);
                Restart.SetActive(false);
            }
            else
            {
                ClientQuit.SetActive(false);
                BackMenu.SetActive(true);

                Restart.SetActive(
                    LV.Instance.CurrLVType != LVType.PvP
                );
            }

            BackMenu.SetActive(true);
            MoreOption.SetActive(false);

            ContinueText.text = "Continuar";
            ContinueText2.text = "Continuar";
        }
        else
        {
            X2Speed.gameObject.SetActive(false);
            BackMenu.SetActive(false);
            MoreOption.SetActive(true);
            Restart.SetActive(false);
            ClientQuit.SetActive(false);

            ContinueText.text = "Aceptar";
            ContinueText2.text = "Aceptar";

            if (!GameManager.Instance.isOnline)
                SetGameSpeed();
        }
    }

    public void LvReset()
    {
        is2xSpeed = false;
        Time.timeScale = 1f;
        SetCheck(X2Speed, false);
    }

    public void VolumeChange()
    {
        AudioManager.Instance.SetVolume(
            SoundSlider.value,
            MusicSlider.value
        );
    }

    public void SaveInit(SettingSave save)
    {
        MusicSlider.value = save.bgmVolume;
        SoundSlider.value = save.soundVolume;

        isFullScreen = save.isFullScreen;
        is1080P = save.isF1080P;
        isVsync = save.isVsync;
        FrameType = save.FrameType;
        isDisFrame = save.isDisFrame;

        SkyManager.Instance.isRainFog = save.isRainFog;
        SeedBank.Instance.CardSelector = save.isCardSelector;

        SetCheck(FullScreen, isFullScreen);
        SetCheck(VsyncBt, isVsync);
        SetCheck(RainFogBt, SkyManager.Instance.isRainFog);
        SetCheck(FrameDisplayBt, isDisFrame);
        SetCheck(CardSlectorBt, SeedBank.Instance.CardSelector);

        QualitySettings.vSyncCount = isVsync ? 1 : 0;
        Application.targetFrameRate = SetFrameType(FrameType);

        if (!GameManager.Instance.isAndroid)
            ApplyResolution();

        FrameDisObj.SetActive(isDisFrame);
    }

    private int SetFrameType(int frameType)
    {
        int fps;

        switch (frameType)
        {
            case 0:
                fps = 60;
                break;

            case 1:
                fps = 120;
                break;

            case 2:
                fps = 160;
                break;

            case 3:
                fps = 320;
                break;

            case 4:
                fps = -1;
                break;

            case 5:
                fps = 30;
                break;

            default:
                FrameType = 0;
                fps = 60;
                break;
        }

        FrameText.text =
            fps == -1
                ? "Sin límite"
                : fps.ToString();

        return fps;
    }

    public void RestartScene()
    {
        if (GameManager.Instance.isClient)
            return;

        UIManager.Instance.ConfirmPanel.InitEvent(
            () =>
            {
                Time.timeScale = 1f;

                PlayButtonSound();

                LVManager.Instance.ReStartGame();
                CloseSetPanel();
            },
            "¿Reiniciar este nivel?",
            "¿Seguro que quieres reiniciar este nivel?",
            "",
            transform
        );
    }

    public void BackMainScene()
    {
        if (GameManager.Instance.isClient)
            return;

        string warn = "";

        if (LVManager.Instance.GameIsStart)
            warn = "*Esta versión no puede guardar el progreso actual*";

        UIManager.Instance.ConfirmPanel.InitEvent(
            () =>
            {
                Time.timeScale = 1f;

                PlayButtonSound();

                LVManager.Instance.QuitBattleGame();
                CloseSetPanel();
            },
            "¿Volver al menú principal?",
            "¿Seguro que quieres volver al menú principal?",
            warn,
            transform
        );
    }

    public void DoClientQuit()
    {
        if (!GameManager.Instance.isClient)
            return;

        OnlineNetworkClient.Instance.CloseClient();
        CloseSetPanel();
    }

    private void InitMoreOption()
    {
        SetMorePage(MOMainPage);
        StatsManager.Instance.CloseViwer();
    }

    public void QuickChat1Btn()
    {
        SendQuickChat(1);
    }

    public void QuickChat2Btn()
    {
        SendQuickChat(2);
    }

    public void QuickChat3Btn()
    {
        SendQuickChat(3);
    }

    public void SendQuickChat(int num)
    {
        if (!QuickChatLimit)
        {
            string text = "";

            switch (num)
            {
                case 1:
                    text = QuickChat1.text;
                    break;

                case 2:
                    text = QuickChat2.text;
                    break;

                case 3:
                    text = QuickChat3.text;
                    break;
            }

            if (!string.IsNullOrEmpty(text))
            {
                StartCoroutine(WaitLimit());

                ChatInput.Instance.SendMessageToAll(
                    text,
                    true
                );
            }
        }

        PlayButtonSound();
    }

    private IEnumerator WaitLimit()
    {
        QuickChatLimit = true;

        yield return QuickChatWait;

        QuickChatLimit = false;
    }

    public void OpenMoreOption()
    {
        AllMoreOptionPage.SetActive(true);
        InitMoreOption();
        PlayButtonSound();
    }

    public void MoreOptionBack()
    {
        if (MOMainPage.activeSelf)
        {
            GameManager.Instance.SaveUserInfo();
            AllMoreOptionPage.SetActive(false);
        }
        else
        {
            InitMoreOption();
        }

        PlayButtonSound();
    }

    public void OpenQuickChatPage()
    {
        SetMorePage(MOQuickChatPage);
        PlayButtonSound();
    }

    public void OpenStatisticsPage()
    {
        SetMorePage(MOStatisticsPage);
        StatsManager.Instance.LoadStats();
        PlayButtonSound();
    }

    public void OpenHardPage()
    {
        SetMorePage(MOHardPage);
        PlayButtonSound();
    }

    public void OpenGraphicsPage()
    {
        SetMorePage(MOGraphicsPage);
        PlayButtonSound();
    }

    public void MoreOptionRead(
        List<bool> bools,
        bool openQuickChat,
        List<string> chats)
    {
        if (bools.Count > 0)
            GobalLight.Instance.LightingNotDark = bools[0];

        if (bools.Count > 1)
            SeedBank.Instance.IsCdDown = bools[1];

        if (bools.Count > 2)
            PlayerManager.Instance.NormalSunUp = bools[2];

        if (bools.Count > 3)
            PlayerManager.Instance.GetSunUp = bools[3];

        if (bools.Count > 4)
            SkyManager.Instance.SlowSunAutoCollect = bools[4];

        SetCheck(
            CdDownBt,
            SeedBank.Instance.IsCdDown
        );

        SetCheck(
            LightningBt,
            GobalLight.Instance.LightingNotDark
        );

        SetCheck(
            NormalSunBt,
            PlayerManager.Instance.NormalSunUp
        );

        SetCheck(
            SunUpBt,
            PlayerManager.Instance.GetSunUp
        );

        SetCheck(
            SlowSunBt,
            SkyManager.Instance.SlowSunAutoCollect
        );

        OpenQuickChat = openQuickChat;

        SetCheck(
            OpenQChatBt,
            OpenQuickChat
        );

        if (chats.Count > 0)
            QuickChat1.text = chats[0];

        if (chats.Count > 1)
            QuickChat2.text = chats[1];

        if (chats.Count > 2)
            QuickChat3.text = chats[2];
    }

    public void CDDownBtn()
    {
        SeedBank.Instance.IsCdDown =
            !SeedBank.Instance.IsCdDown;

        SetCheck(
            CdDownBt,
            SeedBank.Instance.IsCdDown
        );

        PlayButtonSound();
    }

    public void LightningBtn()
    {
        GobalLight.Instance.LightingNotDark =
            !GobalLight.Instance.LightingNotDark;

        SetCheck(
            LightningBt,
            GobalLight.Instance.LightingNotDark
        );

        PlayButtonSound();
    }

    public void NormalSunBtn()
    {
        PlayerManager.Instance.NormalSunUp =
            !PlayerManager.Instance.NormalSunUp;

        SetCheck(
            NormalSunBt,
            PlayerManager.Instance.NormalSunUp
        );

        PlayButtonSound();
    }

    public void SunUpBtn()
    {
        PlayerManager.Instance.GetSunUp =
            !PlayerManager.Instance.GetSunUp;

        SetCheck(
            SunUpBt,
            PlayerManager.Instance.GetSunUp
        );

        PlayButtonSound();
    }

    public void SlowSunBtn()
    {
        SkyManager.Instance.SlowSunAutoCollect =
            !SkyManager.Instance.SlowSunAutoCollect;

        SetCheck(
            SlowSunBt,
            SkyManager.Instance.SlowSunAutoCollect
        );

        PlayButtonSound();
    }

    public void RainFogBtn()
    {
        SkyManager.Instance.isRainFog =
            !SkyManager.Instance.isRainFog;

        SetCheck(
            RainFogBt,
            SkyManager.Instance.isRainFog
        );

        PlayButtonSound();
    }

    public void VsyncBtn()
    {
        isVsync = !isVsync;

        QualitySettings.vSyncCount =
            isVsync ? 1 : 0;

        SetCheck(VsyncBt, isVsync);

        PlayButtonSound();
    }

    public void FrameUpBtn()
    {
        ChangeFrame(1);
    }

    public void FrameDownBtn()
    {
        ChangeFrame(-1);
    }

    private void ChangeFrame(int amount)
    {
        FrameType += amount;

        if (FrameType > 5)
            FrameType = 0;

        if (FrameType < 0)
            FrameType = 5;

        Application.targetFrameRate =
            SetFrameType(FrameType);

        PlayButtonSound();
    }

    public void ResolutionUpBtn()
    {
        ChangeResolution();
    }

    public void ResolutionDownBtn()
    {
        ChangeResolution();
    }

    private void ChangeResolution()
    {
        is1080P = !is1080P;

        if (!GameManager.Instance.isAndroid)
            ApplyResolution();

        PlayButtonSound();
    }

    private void ApplyResolution()
    {
        int width = is1080P ? 1920 : 1280;
        int height = is1080P ? 1080 : 720;

        Screen.SetResolution(
            width,
            height,
            isFullScreen
        );

        ResolutionText.text =
            width + "*" + height;
    }

    public void FrameDisplayBtn()
    {
        isDisFrame = !isDisFrame;

        FrameDisObj.SetActive(isDisFrame);

        SetCheck(
            FrameDisplayBt,
            isDisFrame
        );

        PlayButtonSound();
    }

    public void CardSlectorBtn()
    {
        SeedBank.Instance.CardSelector =
            !SeedBank.Instance.CardSelector;

        SetCheck(
            CardSlectorBt,
            SeedBank.Instance.CardSelector
        );

        PlayButtonSound();
    }

    public void OpenQuickChatBtn()
    {
        OpenQuickChat = !OpenQuickChat;

        SetCheck(
            OpenQChatBt,
            OpenQuickChat
        );

        PlayButtonSound();
    }

    public void CloseSetPanel()
    {
        ShowPanel(false, wasBattle);
    }

    public void FullScreenButton()
    {
        isFullScreen = !isFullScreen;

        SetCheck(
            FullScreen,
            isFullScreen
        );

        if (isFullScreen)
        {
            Screen.SetResolution(
                1920,
                1080,
                true
            );
        }
        else
        {
            ApplyResolution();
        }

        Screen.fullScreen = isFullScreen;

        PlayButtonSound();
    }

    public void XSpeedButton()
    {
        if (GameManager.Instance.isOnline)
            return;

        is2xSpeed = !is2xSpeed;

        SetCheck(
            X2Speed,
            is2xSpeed
        );

        PlayButtonSound();
    }
}