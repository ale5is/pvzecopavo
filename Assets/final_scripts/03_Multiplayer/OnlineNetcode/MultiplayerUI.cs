using System;
using System.Collections;
using System.Net;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class MultiplayerUI : MonoBehaviour
{
    public static MultiplayerUI Instance;

    [Header("Canvas")]
    [SerializeField] private GameObject hostCanvas;
    [SerializeField] private GameObject joinCanvas;
    [SerializeField] private GameObject relayHostCanvas;
    [SerializeField] private GameObject relayJoinCanvas;

    [Header("Local Host")]
    [SerializeField] private TMP_InputField hostIPInput;
    [SerializeField] private TMP_InputField hostPortInput;
    [SerializeField] private TMP_InputField hostPasswordInput;
    [SerializeField] private TMP_Text hostStatusText;

    [Header("Local Join")]
    [SerializeField] private TMP_InputField joinIPInput;
    [SerializeField] private TMP_InputField joinPortInput;
    [SerializeField] private TMP_InputField joinPasswordInput;
    [SerializeField] private TMP_Text joinStatusText;

    [Header("Relay Host")]
    [SerializeField] private TMP_InputField relayHostPasswordInput;
    [SerializeField] private TMP_Text relayJoinCodeText;
    [SerializeField] private TMP_Text relayHostStatusText;
    [SerializeField] private int relayMaxConnections = 3;

    [Header("Relay Join")]
    [SerializeField] private TMP_InputField relayJoinCodeInput;
    [SerializeField] private TMP_InputField relayJoinPasswordInput;
    [SerializeField] private TMP_Text relayJoinStatusText;

    [Header("Code UI")]
    [SerializeField] private CodeUI codeUI;

    [Header("Connection")]
    [SerializeField] private int defaultPort = 7777;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeFields();
    }

    private void InitializeFields()
    {
        if (hostPortInput != null)
            hostPortInput.text = defaultPort.ToString();

        if (joinPortInput != null)
            joinPortInput.text = defaultPort.ToString();

        if (hostIPInput != null)
            hostIPInput.text = "";

        if (joinIPInput != null)
            joinIPInput.text = "";

        if (hostPasswordInput != null)
            hostPasswordInput.text = "";

        if (joinPasswordInput != null)
            joinPasswordInput.text = "";

        if (relayHostPasswordInput != null)
            relayHostPasswordInput.text = "";

        if (relayJoinPasswordInput != null)
            relayJoinPasswordInput.text = "";

        if (relayJoinCodeInput != null)
            relayJoinCodeInput.text = "";

        if (relayJoinCodeText != null)
            relayJoinCodeText.text = "";

        ShowHostStatus("");
        ShowJoinStatus("");
        ShowRelayHostStatus("");
        ShowRelayJoinStatus("");

        if (hostCanvas != null)
            hostCanvas.SetActive(false);

        if (joinCanvas != null)
            joinCanvas.SetActive(false);

        if (relayHostCanvas != null)
            relayHostCanvas.SetActive(false);

        if (relayJoinCanvas != null)
            relayJoinCanvas.SetActive(false);

        if (codeUI != null)
            codeUI.gameObject.SetActive(false);
    }

    public void OpenHostCanvas()
    {
        if (hostCanvas != null)
            hostCanvas.SetActive(true);

        if (joinCanvas != null)
            joinCanvas.SetActive(false);

        if (relayHostCanvas != null)
            relayHostCanvas.SetActive(false);

        if (relayJoinCanvas != null)
            relayJoinCanvas.SetActive(false);

        if (codeUI != null)
            codeUI.gameObject.SetActive(false);

        if (hostPortInput != null &&
            string.IsNullOrWhiteSpace(hostPortInput.text))
        {
            hostPortInput.text =
                defaultPort.ToString();
        }

        if (hostIPInput != null &&
            string.IsNullOrWhiteSpace(hostIPInput.text))
        {
            StartCoroutine(GetPublicIP());
        }
    }

    public void CloseHostCanvas()
    {
        if (hostCanvas != null)
            hostCanvas.SetActive(false);
    }

    public void OpenJoinCanvas()
    {
        if (joinCanvas != null)
            joinCanvas.SetActive(true);

        if (hostCanvas != null)
            hostCanvas.SetActive(false);

        if (relayHostCanvas != null)
            relayHostCanvas.SetActive(false);

        if (relayJoinCanvas != null)
            relayJoinCanvas.SetActive(false);

        if (codeUI != null)
            codeUI.gameObject.SetActive(false);

        if (joinIPInput != null)
            joinIPInput.text = "";

        if (joinPortInput != null)
            joinPortInput.text =
                defaultPort.ToString();

        if (joinPasswordInput != null)
            joinPasswordInput.text = "";

        ShowJoinStatus("");
    }

    public void CloseJoinCanvas()
    {
        if (joinCanvas != null)
            joinCanvas.SetActive(false);
    }

    public void OpenRelayHostCanvas()
    {
        if (relayHostCanvas != null)
            relayHostCanvas.SetActive(true);

        if (hostCanvas != null)
            hostCanvas.SetActive(false);

        if (joinCanvas != null)
            joinCanvas.SetActive(false);

        if (relayJoinCanvas != null)
            relayJoinCanvas.SetActive(false);

        if (codeUI != null)
            codeUI.gameObject.SetActive(false);

        ShowRelayHostStatus("");

        if (relayJoinCodeText != null)
            relayJoinCodeText.text = "";
    }

    public void CloseRelayHostCanvas()
    {
        if (relayHostCanvas != null)
            relayHostCanvas.SetActive(false);
    }

    public void OpenRelayJoinCanvas()
    {
        if (relayJoinCanvas != null)
            relayJoinCanvas.SetActive(true);

        if (hostCanvas != null)
            hostCanvas.SetActive(false);

        if (joinCanvas != null)
            joinCanvas.SetActive(false);

        if (relayHostCanvas != null)
            relayHostCanvas.SetActive(false);

        if (codeUI != null)
            codeUI.gameObject.SetActive(false);

        if (relayJoinCodeInput != null)
            relayJoinCodeInput.text = "";

        if (relayJoinPasswordInput != null)
            relayJoinPasswordInput.text = "";

        ShowRelayJoinStatus("");
    }

    public void CloseRelayJoinCanvas()
    {
        if (relayJoinCanvas != null)
            relayJoinCanvas.SetActive(false);
    }

    public void CreateGame()
    {
        if (GameManager.Instance == null)
        {
            ShowHostStatus(
                "GameManager no encontrado.");
            return;
        }

        if (GameManager.Instance.isOnline)
        {
            ShowHostStatus(
                "Ya estás conectado.");
            return;
        }

        if (OnlineNetworkServer.Instance == null)
        {
            ShowHostStatus(
                "Servidor de red no encontrado.");
            return;
        }

        if (!TryGetPort(
                hostPortInput,
                out int port))
        {
            ShowHostStatus(
                "Puerto inválido.");
            return;
        }

        string password =
            hostPasswordInput != null
                ? hostPasswordInput.text.Trim()
                : "";

        try
        {
            OnlineNetworkServer.Instance.StartServer(
                IPAddress.Parse("0.0.0.0"),
                port,
                password);

            GameManager.Instance.isOnline = true;

            ShowHostStatus(
                "Servidor iniciado. IP: " +
                (hostIPInput != null
                    ? hostIPInput.text
                    : "") +
                " Puerto: " +
                port);

            CloseHostCanvas();
        }
        catch (Exception ex)
        {
            ShowHostStatus(
                "No se pudo iniciar el servidor: " +
                ex.Message);
        }
    }

    public void JoinGame()
    {
        if (GameManager.Instance == null)
        {
            ShowJoinStatus(
                "GameManager no encontrado.");
            return;
        }

        if (GameManager.Instance.isOnline)
        {
            ShowJoinStatus(
                "Ya estás conectado.");
            return;
        }

        if (OnlineNetworkClient.Instance == null)
        {
            ShowJoinStatus(
                "Cliente de red no encontrado.");
            return;
        }

        string ipText =
            joinIPInput != null
                ? joinIPInput.text.Trim()
                : "";

        if (string.IsNullOrWhiteSpace(ipText))
        {
            ShowJoinStatus(
                "Ingrese la IP del host.");
            return;
        }

        if (!IPAddress.TryParse(
                ipText,
                out IPAddress ip))
        {
            ShowJoinStatus(
                "La IP ingresada no es válida.");
            return;
        }

        if (!TryGetPort(
                joinPortInput,
                out int port))
        {
            ShowJoinStatus(
                "Puerto inválido.");
            return;
        }

        string password =
            joinPasswordInput != null
                ? joinPasswordInput.text.Trim()
                : "";

        try
        {
            OnlineNetworkClient.Instance.JoinGame(
                ip,
                port,
                password);

            ShowJoinStatus(
                "Conectando a " +
                ip +
                ":" +
                port +
                "...");

            CloseJoinCanvas();
        }
        catch (Exception ex)
        {
            ShowJoinStatus(
                "No se pudo conectar: " +
                ex.Message);
        }
    }

    public void CreateRelayGame()
    {
        _ = CreateRelayGameAsync();
    }

    private async System.Threading.Tasks.Task CreateRelayGameAsync()
    {
        if (GameManager.Instance == null)
        {
            ShowRelayHostStatus(
                "GameManager no encontrado.");
            return;
        }

        if (GameManager.Instance.isOnline)
        {
            ShowRelayHostStatus(
                "Ya estás conectado.");
            return;
        }

        if (OnlineNetworkServer.Instance == null)
        {
            ShowRelayHostStatus(
                "Servidor de red no encontrado.");
            return;
        }

        string password =
            relayHostPasswordInput != null
                ? relayHostPasswordInput.text.Trim()
                : "";

        ShowRelayHostStatus(
            "Creando partida...");

        string joinCode =
            await OnlineNetworkServer.Instance
                .StartRelayServer(
                    relayMaxConnections,
                    password);

        if (string.IsNullOrEmpty(joinCode))
        {
            ShowRelayHostStatus(
                "No se pudo crear la partida Relay.");
            return;
        }

        if (relayJoinCodeText != null)
            relayJoinCodeText.text =
                joinCode;

        CloseRelayHostCanvas();

        if (codeUI != null)
        {
            codeUI.Show(
                joinCode);

            codeUI.gameObject.SetActive(true);
        }
    }

    public void JoinRelayGame()
    {
        _ = JoinRelayGameAsync();
    }

    private async System.Threading.Tasks.Task JoinRelayGameAsync()
    {
        if (GameManager.Instance == null)
        {
            ShowRelayJoinStatus(
                "GameManager no encontrado.");
            return;
        }

        if (GameManager.Instance.isOnline)
        {
            ShowRelayJoinStatus(
                "Ya estás conectado.");
            return;
        }

        if (OnlineNetworkClient.Instance == null)
        {
            ShowRelayJoinStatus(
                "Cliente de red no encontrado.");
            return;
        }

        string joinCode =
            relayJoinCodeInput != null
                ? relayJoinCodeInput.text.Trim()
                : "";

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            ShowRelayJoinStatus(
                "Ingrese el código de partida.");
            return;
        }

        string password =
            relayJoinPasswordInput != null
                ? relayJoinPasswordInput.text.Trim()
                : "";

        ShowRelayJoinStatus(
            "Conectando...");

        bool started =
            await OnlineNetworkClient.Instance
                .JoinRelayGame(
                    joinCode,
                    password);

        if (!started)
        {
            ShowRelayJoinStatus(
                "No se pudo conectar a la partida.");
            return;
        }

        ShowRelayJoinStatus(
            "Conectando mediante Relay...");
    }

    private bool TryGetPort(
        TMP_InputField input,
        out int port)
    {
        port = defaultPort;

        if (input == null)
            return true;

        if (string.IsNullOrWhiteSpace(
                input.text))
        {
            input.text =
                defaultPort.ToString();

            return true;
        }

        if (!int.TryParse(
                input.text,
                out port))
        {
            return false;
        }

        return port >= 1 &&
               port <= 65535;
    }

    public void ConnectSuccess()
    {
        ShowJoinStatus(
            "Conectado correctamente.");

        ShowRelayJoinStatus(
            "Conectado correctamente.");

        CloseJoinCanvas();
        CloseRelayJoinCanvas();
    }

    public void ReJoinGame()
    {
        OpenJoinCanvas();
    }

    public void LeaveGame()
    {
        var manager =
            Unity.Netcode.NetworkManager.Singleton;

        if (manager == null ||
            (!manager.IsClient &&
             !manager.IsServer))
        {
            return;
        }

        if (manager.IsServer)
        {
            manager.Shutdown();

            if (GameManager.Instance != null)
                GameManager.Instance.isOnline = false;

            if (codeUI != null)
                codeUI.gameObject.SetActive(false);

            return;
        }

        if (OnlineNetworkClient.Instance != null)
        {
            OnlineNetworkClient.Instance.CloseClient();
            return;
        }

        manager.Shutdown();

        if (GameManager.Instance != null)
            GameManager.Instance.isOnline = false;

        if (codeUI != null)
            codeUI.gameObject.SetActive(false);
    }

    private IEnumerator GetPublicIP()
    {
        using UnityWebRequest request =
            UnityWebRequest.Get(
                "https://api.ipify.org");

        yield return request.SendWebRequest();

        if (request.result !=
            UnityWebRequest.Result.Success)
        {
            ShowHostStatus(
                "No se pudo obtener la IP pública.");

            yield break;
        }

        string publicIP =
            request.downloadHandler.text.Trim();

        if (hostIPInput != null &&
            string.IsNullOrWhiteSpace(
                hostIPInput.text))
        {
            hostIPInput.text =
                publicIP;
        }

        ShowHostStatus(
            "IP pública: " +
            publicIP);
    }

    private void ShowHostStatus(
        string message)
    {
        if (hostStatusText != null)
            hostStatusText.text = message;
    }

    private void ShowJoinStatus(
        string message)
    {
        if (joinStatusText != null)
            joinStatusText.text = message;
    }

    private void ShowRelayHostStatus(
        string message)
    {
        if (relayHostStatusText != null)
            relayHostStatusText.text = message;
    }

    private void ShowRelayJoinStatus(
        string message)
    {
        if (relayJoinStatusText != null)
            relayJoinStatusText.text = message;
    }
}