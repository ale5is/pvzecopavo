using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    public Transform UIBackground;
    public LogPanel LogPanel;
    public ConfirmPanel ConfirmPanel;
    public Transform BattleUI;

    private LVStartEF LVStartEF;

    public SetPanel SetPanel;
    public OverPanel OverPanel;
    public Transform LastStandBtn;

    public Transform HostGame;
    public InputField IpInput;
    public InputField PortInput;

    public Transform HostPassword;
    public InputField HostPasswordInput;

    public Transform JoinGame;
    public InputField JoinIpInput;
    public InputField JoinPasswordInput;

    private bool isChatBoxOpen;

    public ChatInput chatInput;

    public Transform ChatBox;
    public Transform OutChatBox;
    public Transform OpenChatButton;
    public Transform QuickChatGroup;

    public bool IsChatBoxOpen
    {
        get
        {
            return isChatBoxOpen;
        }
        set
        {
            isChatBoxOpen = value;

            if (value)
            {
                if (ChatBox != null)
                    ChatBox.gameObject.SetActive(true);

                if (OutChatBox != null)
                    OutChatBox.gameObject.SetActive(false);

                if (chatInput != null && chatInput.InputField != null)
                {
                    chatInput.InputField.gameObject.SetActive(true);
                    chatInput.InputField.ActivateInputField();
                    chatInput.InputField.MoveTextEnd(false);
                }
            }
            else
            {
                if (chatInput != null)
                    chatInput.ClearInput();

                if (ChatBox != null)
                    ChatBox.gameObject.SetActive(false);

                if (OutChatBox != null)
                    OutChatBox.gameObject.SetActive(true);
            }
        }
    }

    private void Awake()
    {
        Instance = this;

        if (BattleUI != null)
        {
            Transform lvEffect = BattleUI.Find("LVEF");

            if (lvEffect != null)
                LVStartEF = lvEffect.GetComponent<LVStartEF>();

            Transform overPanel = BattleUI.Find("OverPanel");

            if (overPanel != null)
                OverPanel = overPanel.GetComponent<OverPanel>();
        }

        Transform setPanel = transform.Find("SetPanel");

        if (setPanel != null)
            SetPanel = setPanel.GetComponent<SetPanel>();
    }

    private void Start()
    {
        if (OpenChatButton != null)
        {
            OpenChatButton.gameObject.SetActive(
                GameManager.Instance != null &&
                GameManager.Instance.isAndroid
            );
        }

        if (LastStandBtn != null)
            LastStandBtn.gameObject.SetActive(false);

        if (ChatBox != null)
            ChatBox.gameObject.SetActive(false);

        if (OutChatBox != null)
            OutChatBox.gameObject.SetActive(true);

        SetHostAddress();
    }

    private void Update()
    {
        if (SetPanel != null &&
            (SetPanel.isOpen || isChatBoxOpen))
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (SetPanel.isOpen)
                {
                    SetPanel.CloseSetPanel();
                }
                else
                {
                    IsChatBoxOpen = false;
                }
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) &&
            LVManager.Instance != null &&
            LVManager.Instance.InGame)
        {
            ShowBattleSetPanel();
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            IsChatBoxOpen = true;
        }

        if (Input.GetKeyDown(KeyCode.Slash))
        {
            IsChatBoxOpen = true;

            if (chatInput != null)
                chatInput.SlashOpen();
        }
    }

    private string GetLocalIPAddress()
    {
        NetworkInterface[] interfaces =
            NetworkInterface.GetAllNetworkInterfaces();

        for (int i = 0; i < interfaces.Length; i++)
        {
            NetworkInterface network = interfaces[i];

            if (network.OperationalStatus != OperationalStatus.Up ||
                network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            IPInterfaceProperties properties =
                network.GetIPProperties();

            for (int j = 0; j < properties.UnicastAddresses.Count; j++)
            {
                IPAddress address =
                    properties.UnicastAddresses[j].Address;

                if (address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address) &&
                    !address.ToString().StartsWith("169.254."))
                {
                    return address.ToString();
                }
            }
        }

        return "127.0.0.1";
    }

    private void SetHostAddress()
    {
        if (IpInput != null)
            IpInput.text = GetLocalIPAddress();

        if (PortInput != null)
            PortInput.text = "45678";
    }

    public void SetChooserType(SeedBankType type)
    {
        if (type == SeedBankType.SunBank)
        {
            SeedChooser.Instance.transform.localScale = Vector3.one;
            ZombieChooser.Instance.transform.localScale = Vector3.zero;
            SeedChooser.Instance.ChangeChooserBtn.localScale = Vector3.zero;
        }
        else if (type == SeedBankType.MoonBank)
        {
            SeedChooser.Instance.transform.localScale = Vector3.zero;
            ZombieChooser.Instance.transform.localScale = Vector3.one;
            SeedChooser.Instance.ChangeChooserBtn.localScale = Vector3.zero;
        }
        else if (type == SeedBankType.SunAndMoonBank)
        {
            SeedChooser.Instance.transform.localScale = Vector3.one;
            ZombieChooser.Instance.transform.localScale = Vector3.zero;
            SeedChooser.Instance.ChangeChooserBtn.localScale = Vector3.one;
            ZombieChooser.Instance.ChangeChooserBtn.localScale = Vector3.one;
        }
    }

    public void ChangeChooser()
    {
        bool seed =
            SeedChooser.Instance.transform.localScale.x == 0f;

        SeedChooser.Instance.transform.localScale =
            seed ? Vector3.one : Vector3.zero;

        ZombieChooser.Instance.transform.localScale =
            seed ? Vector3.zero : Vector3.one;
    }

    public void OpenChatBtn()
    {
        if (IsChatBoxOpen)
        {
            if (chatInput != null)
                chatInput.SendContent();
        }
        else
        {
            IsChatBoxOpen = true;
        }

        if (AudioManager.Instance != null &&
            GameManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.GraveButton,
                transform.position,
                isAll: true
            );
        }
    }

    public void ShowLVStartEF()
    {
        if (LVStartEF != null)
            LVStartEF.Show();
    }

    public void StopLVStartEF()
    {
        if (LVStartEF != null)
            LVStartEF.StopAll();
    }

    public void ShowBigWaveEF()
    {
        if (LVStartEF != null)
            LVStartEF.ShowBigWave();
    }

    public void ShowFinalWaveEF()
    {
        if (LVStartEF != null)
            LVStartEF.ShowFinalWave();
    }

    public void ShowSetPanel()
    {
        if (SetPanel != null)
            SetPanel.ShowPanel(true, false);
    }

    public void ShowBattleSetPanel()
    {
        if (SetPanel != null)
            SetPanel.ShowPanel(true, true);
    }

    public void ConfirmOpenServer()
    {
        if (AudioManager.Instance != null &&
            GameManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.GraveButton,
                transform.position,
                isAll: true
            );
        }

        if (!IPAddress.TryParse(
                IpInput.text,
                out IPAddress address) ||
            !int.TryParse(
                PortInput.text,
                out int port) ||
            port < 1025 ||
            port > 65535)
        {
            LogPanel.DisplayLog(
                "Ingrese una dirección IP y un puerto válidos",
                () => HostGame.gameObject.SetActive(true)
            );

            return;
        }

        SocketServer.Instance.StartServer(address, port);
        CloseHostGame();
    }

    public void ConfirmJoinGame()
    {
        if (GameManager.Instance.isOnline)
            return;

        if (AudioManager.Instance != null &&
            GameManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.GraveButton,
                transform.position,
                isAll: true
            );
        }

        string[] address =
            JoinIpInput.text.Split(':');

        if (address.Length < 2 ||
            !int.TryParse(address[1], out int port) ||
            port < 1025 ||
            port > 65535)
        {
            LogPanel.DisplayLog(
                "Ingrese una dirección válida",
                () => JoinGame.gameObject.SetActive(true)
            );

            return;
        }

        try
        {
            IPAddress[] addresses =
                Dns.GetHostAddresses(address[0]);

            if (addresses.Length == 0)
            {
                LogPanel.DisplayLog(
                    "No se pudo encontrar la dirección",
                    () => JoinGame.gameObject.SetActive(true)
                );

                return;
            }

            LogPanel.DisplayLog(
                "Conectando...",
                () => JoinGame.gameObject.SetActive(true)
            );

            LogPanel.ButtonText.text = "Cancelar";
            LogPanel.CancelConfirm();

            SocketClient.Instance.JoinGame(
                addresses[0],
                port,
                JoinPasswordInput.text
            );
        }
        catch
        {
            LogPanel.DisplayLog(
                "No se pudo encontrar la dirección",
                () => JoinGame.gameObject.SetActive(true)
            );
        }
    }

    public void ReJoinGame()
    {
        if (GameManager.Instance.isOnline)
            return;

        string[] address =
            JoinIpInput.text.Split(':');

        if (address.Length < 2 ||
            !int.TryParse(address[1], out int port))
        {
            return;
        }

        try
        {
            IPAddress[] addresses =
                Dns.GetHostAddresses(address[0]);

            if (addresses.Length == 0)
                return;

            LogPanel.DisplayLog("Conectando...", null);
            LogPanel.CancelConfirm();

            SocketClient.Instance.JoinGame(
                addresses[0],
                port,
                JoinPasswordInput.text
            );
        }
        catch
        {
        }
    }

    public void ConnectSuccess()
    {
        LogPanel.gameObject.SetActive(false);
        CloseJoinGame();
    }

    public void CloseHostGame()
    {
        if (AudioManager.Instance != null &&
            GameManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.GraveButton,
                transform.position,
                isAll: true
            );
        }

        SetHostAddress();
        HostGame.gameObject.SetActive(false);
    }

    public void CloseJoinGame()
    {
        if (AudioManager.Instance != null &&
            GameManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.GraveButton,
                transform.position,
                isAll: true
            );
        }

        JoinGame.gameObject.SetActive(false);
    }

    public void ConfirmPassword()
    {
        if (AudioManager.Instance != null &&
            GameManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.GraveButton,
                transform.position,
                isAll: true
            );
        }

        HostPassword.gameObject.SetActive(false);
    }

    public void OpenAndFocusUI(bool isBlack = true)
    {
        UIBackground.GetComponent<Image>().color =
            new Color(
                0f,
                0f,
                0f,
                isBlack ? 0.4f : 0f
            );

        UIBackground.gameObject.SetActive(true);
    }

    public void CloseUI()
    {
        UIBackground.gameObject.SetActive(false);
        UIBackground.SetSiblingIndex(0);
    }

    public void OpenBattleUI()
    {
        Shovel.Instance.LvStart();
        Glove.Instance.LvStart();
        CreatePanel.Instance.BattleUIOpen();

        QuickChatGroup.gameObject.SetActive(
            SetPanel.OpenQuickChat
        );

        BattleUI.gameObject.SetActive(true);
    }

    public void StartLastStand()
    {
        LVManager.Instance.StartLastStand();

        if (AudioManager.Instance != null &&
            GameManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.GraveButton,
                transform.position,
                isAll: true
            );
        }
    }
}