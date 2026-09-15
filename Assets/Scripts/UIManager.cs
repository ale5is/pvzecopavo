using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    // =========================================================
    // REFERENCIAS DIRECTAS - INSPECTOR
    // =========================================================

    [Header("Managers")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private LVManager lvManager;
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private SocketServer socketServer;
    [SerializeField] private SocketClient socketClient;
    [SerializeField] private SeedChooser seedChooser;
    [SerializeField] private ZombieChooser zombieChooser;
    [SerializeField] private Shovel shovel;
    [SerializeField] private Glove glove;
    [SerializeField] private CreatePanel createPanel;

    [Header("UI Principal")]
    [SerializeField] public GameObject UIBackground;
    [SerializeField] public Image UIBackgroundImage;
    [SerializeField] public LogPanel LogPanel;
    [SerializeField] public ConfirmPanel ConfirmPanel;

    [Header("Battle UI")]
    [SerializeField] public GameObject BattleUI;
    [SerializeField] public LVStartEF LVStartEF;
    [SerializeField] public OverPanel OverPanel;
    [SerializeField] public SetPanel SetPanel;

    [Header("Battle")]
    [SerializeField] public GameObject LastStandBtn;

    [Header("Host")]
    [SerializeField] public GameObject HostGame;
    [SerializeField] public InputField IpInput;
    [SerializeField] public InputField PortInput;

    [Header("Host Password")]
    [SerializeField] public GameObject HostPassword;
    [SerializeField] public InputField HostPasswordInput;

    [Header("Join")]
    [SerializeField] public GameObject JoinGame;
    [SerializeField] public InputField JoinIpInput;
    [SerializeField] public InputField JoinPasswordInput;

    [Header("Chat")]
    [SerializeField] public ChatInput chatInput;
    [SerializeField] public GameObject ChatBox;
    [SerializeField] public GameObject OutChatBox;
    [SerializeField] public GameObject OpenChatButton;
    [SerializeField] public GameObject QuickChatGroup;

    private bool isChatBoxOpen;

    // =========================================================
    // CHAT
    // =========================================================

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
                {
                    ChatBox.SetActive(true);
                }

                if (OutChatBox != null)
                {
                    OutChatBox.SetActive(false);
                }

                if (
                    chatInput != null &&
                    chatInput.InputField != null
                )
                {
                    chatInput.InputField.gameObject.SetActive(true);
                    chatInput.InputField.ActivateInputField();
                    chatInput.InputField.MoveTextEnd(false);
                }
            }
            else
            {
                if (chatInput != null)
                {
                    chatInput.ClearInput();
                }

                if (ChatBox != null)
                {
                    ChatBox.SetActive(false);
                }

                if (OutChatBox != null)
                {
                    OutChatBox.SetActive(true);
                }
            }
        }
    }

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (OpenChatButton != null)
        {
            OpenChatButton.SetActive(
                gameManager != null &&
                gameManager.isAndroid
            );
        }

        if (LastStandBtn != null)
        {
            LastStandBtn.SetActive(false);
        }

        if (ChatBox != null)
        {
            ChatBox.SetActive(false);
        }

        if (OutChatBox != null)
        {
            OutChatBox.SetActive(true);
        }

        SetHostAddress();
    }

    private void Update()
    {
        if (
            SetPanel != null &&
            (SetPanel.isOpen || isChatBoxOpen)
        )
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

        if (
            Input.GetKeyDown(KeyCode.Escape) &&
            lvManager != null &&
            lvManager.InGame
        )
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
            {
                chatInput.SlashOpen();
            }
        }
    }

    // =========================================================
    // IP
    // =========================================================

    private string GetLocalIPAddress()
    {
        NetworkInterface[] interfaces =
            NetworkInterface.GetAllNetworkInterfaces();

        for (int i = 0; i < interfaces.Length; i++)
        {
            NetworkInterface network = interfaces[i];

            if (
                network.OperationalStatus != OperationalStatus.Up ||
                network.NetworkInterfaceType == NetworkInterfaceType.Loopback
            )
            {
                continue;
            }

            IPInterfaceProperties properties =
                network.GetIPProperties();

            for (int j = 0; j < properties.UnicastAddresses.Count; j++)
            {
                IPAddress address =
                    properties.UnicastAddresses[j].Address;

                if (
                    address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address) &&
                    !address.ToString().StartsWith("169.254.")
                )
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
        {
            IpInput.text = GetLocalIPAddress();
        }

        if (PortInput != null)
        {
            PortInput.text = "45678";
        }
    }

    // =========================================================
    // CHOOSER
    // =========================================================

    public void SetChooserType(SeedBankType type)
    {
        if (
            seedChooser == null ||
            zombieChooser == null
        )
        {
            return;
        }

        if (type == SeedBankType.SunBank)
        {
            seedChooser.gameObject.SetActive(true);
            zombieChooser.gameObject.SetActive(false);

            seedChooser.ChangeChooserBtn.gameObject.SetActive(false);
            zombieChooser.ChangeChooserBtn.gameObject.SetActive(false);
        }
        else if (type == SeedBankType.MoonBank)
        {
            seedChooser.gameObject.SetActive(false);
            zombieChooser.gameObject.SetActive(true);

            seedChooser.ChangeChooserBtn.gameObject.SetActive(false);
            zombieChooser.ChangeChooserBtn.gameObject.SetActive(false);
        }
        else if (type == SeedBankType.SunAndMoonBank)
        {
            seedChooser.gameObject.SetActive(true);
            zombieChooser.gameObject.SetActive(false);

            seedChooser.ChangeChooserBtn.gameObject.SetActive(true);
            zombieChooser.ChangeChooserBtn.gameObject.SetActive(true);
        }
    }

    public void ChangeChooser()
    {
        if (
            seedChooser == null ||
            zombieChooser == null
        )
        {
            return;
        }

        bool seed =
            !seedChooser.gameObject.activeSelf;

        seedChooser.gameObject.SetActive(seed);
        zombieChooser.gameObject.SetActive(!seed);
    }

    // =========================================================
    // AUDIO
    // =========================================================

    private void PlayButtonAudio()
    {
        if (
            audioManager == null ||
            gameManager == null ||
            gameManager.AudioConf == null
        )
        {
            return;
        }

        audioManager.PlayEFAudio(
            gameManager.AudioConf.GraveButton,
            transform.position,
            isAll: true
        );
    }

    // =========================================================
    // CHAT
    // =========================================================

    public void OpenChatBtn()
    {
        if (IsChatBoxOpen)
        {
            if (chatInput != null)
            {
                chatInput.SendContent();
            }
        }
        else
        {
            IsChatBoxOpen = true;
        }

        PlayButtonAudio();
    }

    // =========================================================
    // EF
    // =========================================================

    public void ShowLVStartEF()
    {
        if (LVStartEF != null)
        {
            LVStartEF.Show();
        }
    }

    public void StopLVStartEF()
    {
        if (LVStartEF != null)
        {
            LVStartEF.StopAll();
        }
    }

    public void ShowBigWaveEF()
    {
        if (LVStartEF != null)
        {
            LVStartEF.ShowBigWave();
        }
    }

    public void ShowFinalWaveEF()
    {
        if (LVStartEF != null)
        {
            LVStartEF.ShowFinalWave();
        }
    }

    // =========================================================
    // SET PANEL
    // =========================================================

    public void ShowSetPanel()
    {
        if (SetPanel != null)
        {
            SetPanel.ShowPanel(true, false);
        }
    }

    public void ShowBattleSetPanel()
    {
        if (SetPanel != null)
        {
            SetPanel.ShowPanel(true, true);
        }
    }

    // =========================================================
    // HOST
    // =========================================================

    public void ConfirmOpenServer()
    {
        PlayButtonAudio();

        if (
            IpInput == null ||
            PortInput == null
        )
        {
            return;
        }

        if (
            !IPAddress.TryParse(
                IpInput.text,
                out IPAddress address
            ) ||
            !int.TryParse(
                PortInput.text,
                out int port
            ) ||
            port < 1025 ||
            port > 65535
        )
        {
            if (LogPanel != null)
            {
                LogPanel.DisplayLog(
                    "Ingrese una dirección IP y un puerto válidos",
                    () =>
                    {
                        if (HostGame != null)
                        {
                            HostGame.SetActive(true);
                        }
                    }
                );
            }

            return;
        }

        if (socketServer != null)
        {
            socketServer.StartServer(address, port);
        }

        CloseHostGame();
    }

    // =========================================================
    // JOIN
    // =========================================================

    public void ConfirmJoinGame()
    {
        if (
            gameManager == null ||
            gameManager.isOnline
        )
        {
            return;
        }

        PlayButtonAudio();

        if (
            JoinIpInput == null ||
            JoinPasswordInput == null
        )
        {
            return;
        }

        string[] address =
            JoinIpInput.text.Split(':');

        if (
            address.Length < 2 ||
            !int.TryParse(
                address[1],
                out int port
            ) ||
            port < 1025 ||
            port > 65535
        )
        {
            if (LogPanel != null)
            {
                LogPanel.DisplayLog(
                    "Ingrese una dirección válida",
                    () =>
                    {
                        if (JoinGame != null)
                        {
                            JoinGame.SetActive(true);
                        }
                    }
                );
            }

            return;
        }

        try
        {
            IPAddress[] addresses =
                Dns.GetHostAddresses(address[0]);

            if (addresses.Length == 0)
            {
                if (LogPanel != null)
                {
                    LogPanel.DisplayLog(
                        "No se pudo encontrar la dirección",
                        () =>
                        {
                            if (JoinGame != null)
                            {
                                JoinGame.SetActive(true);
                            }
                        }
                    );
                }

                return;
            }

            if (LogPanel != null)
            {
                LogPanel.DisplayLog(
                    "Conectando...",
                    () =>
                    {
                        if (JoinGame != null)
                        {
                            JoinGame.SetActive(true);
                        }
                    }
                );

                LogPanel.ButtonText.text = "Cancelar";
                LogPanel.CancelConfirm();
            }

            if (socketClient != null)
            {
                socketClient.JoinGame(
                    addresses[0],
                    port,
                    JoinPasswordInput.text
                );
            }
        }
        catch
        {
            if (LogPanel != null)
            {
                LogPanel.DisplayLog(
                    "No se pudo encontrar la dirección",
                    () =>
                    {
                        if (JoinGame != null)
                        {
                            JoinGame.SetActive(true);
                        }
                    }
                );
            }
        }
    }

    public void ReJoinGame()
    {
        if (
            gameManager == null ||
            gameManager.isOnline
        )
        {
            return;
        }

        if (JoinIpInput == null)
        {
            return;
        }

        string[] address =
            JoinIpInput.text.Split(':');

        if (
            address.Length < 2 ||
            !int.TryParse(
                address[1],
                out int port
            )
        )
        {
            return;
        }

        try
        {
            IPAddress[] addresses =
                Dns.GetHostAddresses(address[0]);

            if (addresses.Length == 0)
            {
                return;
            }

            if (LogPanel != null)
            {
                LogPanel.DisplayLog(
                    "Conectando...",
                    null
                );

                LogPanel.CancelConfirm();
            }

            if (socketClient != null)
            {
                socketClient.JoinGame(
                    addresses[0],
                    port,
                    JoinPasswordInput.text
                );
            }
        }
        catch
        {
        }
    }

    public void ConnectSuccess()
    {
        if (LogPanel != null)
        {
            LogPanel.gameObject.SetActive(false);
        }

        CloseJoinGame();
    }

    // =========================================================
    // CERRAR HOST / JOIN
    // =========================================================

    public void CloseHostGame()
    {
        PlayButtonAudio();

        SetHostAddress();

        if (HostGame != null)
        {
            HostGame.SetActive(false);
        }
    }

    public void CloseJoinGame()
    {
        PlayButtonAudio();

        if (JoinGame != null)
        {
            JoinGame.SetActive(false);
        }
    }

    public void ConfirmPassword()
    {
        PlayButtonAudio();

        if (HostPassword != null)
        {
            HostPassword.SetActive(false);
        }
    }

    // =========================================================
    // UI BACKGROUND
    // =========================================================

    public void OpenAndFocusUI(bool isBlack = true)
    {
        if (UIBackground == null)
        {
            return;
        }

        if (UIBackgroundImage != null)
        {
            UIBackgroundImage.color =
                new Color(
                    0f,
                    0f,
                    0f,
                    isBlack ? 0.4f : 0f
                );
        }

        UIBackground.SetActive(true);
    }

    public void CloseUI()
    {
        if (UIBackground == null)
        {
            return;
        }

        UIBackground.SetActive(false);
        UIBackground.transform.SetSiblingIndex(0);
    }

    // =========================================================
    // BATTLE UI
    // =========================================================

    public void OpenBattleUI()
    {
        if (shovel != null)
        {
            shovel.LvStart();
        }

        if (glove != null)
        {
            glove.LvStart();
        }

        if (createPanel != null)
        {
            createPanel.BattleUIOpen();
        }

        if (
            QuickChatGroup != null &&
            SetPanel != null
        )
        {
            QuickChatGroup.SetActive(
                SetPanel.OpenQuickChat
            );
        }

        if (BattleUI != null)
        {
            BattleUI.SetActive(true);
        }
    }

    public void CloseBattleUI()
    {
        if (BattleUI != null)
        {
            BattleUI.SetActive(false);
        }
    }

    // =========================================================
    // LAST STAND
    // =========================================================

    public void StartLastStand()
    {
        if (lvManager != null)
        {
            lvManager.StartLastStand();
        }

        PlayButtonAudio();
    }
}