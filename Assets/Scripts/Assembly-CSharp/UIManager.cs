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
        get => isChatBoxOpen;
        set
        {
            isChatBoxOpen = value;
            if (value)
            {
                chatInput.InputField.ActivateInputField();
                ChatBox.localScale = Vector3.one;
                OutChatBox.localScale = Vector3.zero;
            }
            else
            {
                chatInput.ClearInput();
                ChatBox.localScale = Vector3.zero;
                OutChatBox.localScale = Vector3.one;
            }
        }
    }

    private void Awake()
    {
        Instance = this;
        LVStartEF = BattleUI.Find("LVEF").GetComponent<LVStartEF>();
        OverPanel = BattleUI.Find("OverPanel").GetComponent<OverPanel>();
        SetPanel = transform.Find("SetPanel").GetComponent<SetPanel>();
    }

    private void Start()
    {
        OpenChatButton.gameObject.SetActive(GameManager.Instance.isAndroid);
        LastStandBtn.gameObject.SetActive(false);
        SetHostAddress();
    }

    private void Update()
    {
        if (SetPanel.isOpen || isChatBoxOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (SetPanel.isOpen) SetPanel.CloseSetPanel();
                else IsChatBoxOpen = false;
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) && LVManager.Instance.InGame)
            ShowBattleSetPanel();

        if (Input.GetKeyDown(KeyCode.T))
            IsChatBoxOpen = true;

        if (Input.GetKeyDown(KeyCode.Slash))
        {
            IsChatBoxOpen = true;
            chatInput.SlashOpen();
        }
    }

    private string GetLocalIPAddress()
    {
        NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

        for (int i = 0; i < interfaces.Length; i++)
        {
            NetworkInterface network = interfaces[i];

            if (network.OperationalStatus != OperationalStatus.Up ||
                network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            IPInterfaceProperties properties = network.GetIPProperties();

            for (int j = 0; j < properties.UnicastAddresses.Count; j++)
            {
                IPAddress address = properties.UnicastAddresses[j].Address;

                if (address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address) &&
                    !address.ToString().StartsWith("169.254."))
                    return address.ToString();
            }
        }

        return "127.0.0.1";
    }

    private void SetHostAddress()
    {
        IpInput.text = GetLocalIPAddress();
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
            ZombieChooser.Instance.ChangeChooserBtn.localScale = Vector3.zero;
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
        bool seed = SeedChooser.Instance.transform.localScale.x == 0f;
        SeedChooser.Instance.transform.localScale = seed ? Vector3.one : Vector3.zero;
        ZombieChooser.Instance.transform.localScale = seed ? Vector3.zero : Vector3.one;
    }

    public void OpenChatBtn()
    {
        if (IsChatBoxOpen) chatInput.SendContent();
        else IsChatBoxOpen = true;
        AudioManager.Instance.PlayEFAudio(GameManager.Instance.AudioConf.GraveButton, transform.position, isAll: true);
    }

    public void ShowLVStartEF() => LVStartEF.Show();
    public void StopLVStartEF() => LVStartEF.StopAll();
    public void ShowBigWaveEF() => LVStartEF.ShowBigWave();
    public void ShowFinalWaveEF() => LVStartEF.ShowFinalWave();
    public void ShowSetPanel() => SetPanel.ShowPanel(true, false);
    public void ShowBattleSetPanel() => SetPanel.ShowPanel(true, true);

    public void ConfirmOpenServer()
    {
        AudioManager.Instance.PlayEFAudio(GameManager.Instance.AudioConf.GraveButton, transform.position, isAll: true);

        if (!IPAddress.TryParse(IpInput.text, out IPAddress address) ||
            !int.TryParse(PortInput.text, out int port) ||
            port < 1025 || port > 65535)
        {
            LogPanel.DisplayLog("Ingrese una dirección IP y un puerto válidos", () => HostGame.gameObject.SetActive(true));
            return;
        }

        SocketServer.Instance.StartServer(address, port);
        CloseHostGame();
    }

    public void ConfirmJoinGame()
    {
        if (GameManager.Instance.isOnline) return;

        AudioManager.Instance.PlayEFAudio(GameManager.Instance.AudioConf.GraveButton, transform.position, isAll: true);

        string[] address = JoinIpInput.text.Split(':');

        if (address.Length < 2 ||
            !int.TryParse(address[1], out int port) ||
            port < 1025 ||
            port > 65535)
        {
            LogPanel.DisplayLog("Ingrese una dirección válida", () => JoinGame.gameObject.SetActive(true));
            return;
        }

        try
        {
            IPAddress[] addresses = Dns.GetHostAddresses(address[0]);

            if (addresses.Length == 0)
            {
                LogPanel.DisplayLog("No se pudo encontrar la dirección", () => JoinGame.gameObject.SetActive(true));
                return;
            }

            LogPanel.DisplayLog("Conectando...", () => JoinGame.gameObject.SetActive(true));
            LogPanel.ButtonText.text = "Cancelar";
            LogPanel.CancelConfirm();
            SocketClient.Instance.JoinGame(addresses[0], port, JoinPasswordInput.text);
        }
        catch
        {
            LogPanel.DisplayLog("No se pudo encontrar la dirección", () => JoinGame.gameObject.SetActive(true));
        }
    }

    public void ReJoinGame()
    {
        if (GameManager.Instance.isOnline) return;

        string[] address = JoinIpInput.text.Split(':');

        if (address.Length < 2 || !int.TryParse(address[1], out int port))
            return;

        try
        {
            IPAddress[] addresses = Dns.GetHostAddresses(address[0]);

            if (addresses.Length == 0)
                return;

            LogPanel.DisplayLog("Conectando...", null);
            LogPanel.CancelConfirm();
            SocketClient.Instance.JoinGame(addresses[0], port, JoinPasswordInput.text);
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
        AudioManager.Instance.PlayEFAudio(GameManager.Instance.AudioConf.GraveButton, transform.position, isAll: true);
        SetHostAddress();
        HostGame.gameObject.SetActive(false);
    }

    public void CloseJoinGame()
    {
        AudioManager.Instance.PlayEFAudio(GameManager.Instance.AudioConf.GraveButton, transform.position, isAll: true);
        JoinGame.gameObject.SetActive(false);
    }

    public void ConfirmPassword()
    {
        AudioManager.Instance.PlayEFAudio(GameManager.Instance.AudioConf.GraveButton, transform.position, isAll: true);
        HostPassword.gameObject.SetActive(false);
    }

    public void OpenAndFocusUI(bool isBlack = true)
    {
        UIBackground.GetComponent<Image>().color = new Color(0f, 0f, 0f, isBlack ? 0.4f : 0f);
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
        QuickChatGroup.gameObject.SetActive(SetPanel.OpenQuickChat);
        BattleUI.localScale = Vector3.one;
    }

    public void StartLastStand()
    {
        LVManager.Instance.StartLastStand();
        AudioManager.Instance.PlayEFAudio(GameManager.Instance.AudioConf.GraveButton, transform.position, isAll: true);
    }
}