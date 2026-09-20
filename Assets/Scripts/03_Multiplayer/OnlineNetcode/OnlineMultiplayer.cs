using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using System.Net;

public class OnlineMultiplayer : MonoBehaviour
{
    public string address = "127.0.0.1";
    public ushort port = 7777;

    public TMP_Text onlineStatusText;
    public TMP_Text onlineClientsText;
    public TMP_Text onlineDiagnosticText;

    private NetworkManager networkManager;
    private UnityTransport transport;

    private float diagnosticTimer;

    private void Awake()
    {
        networkManager = NetworkManager.Singleton;

        if (networkManager != null)
        {
            transport = networkManager.GetComponent<UnityTransport>();

            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnect;
            networkManager.OnTransportFailure += OnTransportFailure;
        }
    }

    private void Start()
    {
        ActualizarDiagnostico();
    }

    private void Update()
    {
        diagnosticTimer += Time.unscaledDeltaTime;

        if (diagnosticTimer >= 1f)
        {
            diagnosticTimer = 0f;
            ActualizarDiagnostico();
        }
    }

    public void CrearPartida()
    {
        networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError("[OnlineMultiplayer] NetworkManager.Singleton sigue siendo NULL al pulsar CrearPartida.");
            MostrarEstado("ERROR: NetworkManager.Singleton NULL");
            return;
        }

        transport = networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError("[OnlineMultiplayer] UnityTransport no encontrado en el NetworkManager.");
            MostrarEstado("ERROR: UnityTransport NULL");
            return;
        }

        if (networkManager.IsListening)
        {
            MostrarEstado("El NetworkManager ya está iniciado.");
            return;
        }

        if (OnlineNetworkServer.Instance == null)
        {
            Debug.LogError("[OnlineMultiplayer] OnlineNetworkServer.Instance es NULL.");
            MostrarEstado("ERROR: OnlineNetworkServer NULL");
            return;
        }

        OnlineNetworkServer.Instance.StartServer(
            IPAddress.Parse("0.0.0.0"),
            port);

        if (networkManager.IsHost)
        {
            MostrarEstado("HOST INICIADO");
        }
        else
        {
            MostrarEstado("ERROR AL INICIAR HOST");
        }

        ActualizarDiagnostico();
    }

    public void UnirsePartida()
    {
        networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError("[OnlineMultiplayer] NetworkManager.Singleton sigue siendo NULL al pulsar UnirsePartida.");
            MostrarEstado("ERROR: NetworkManager.Singleton NULL");
            return;
        }

        transport = networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError("[OnlineMultiplayer] UnityTransport no encontrado en el NetworkManager.");
            MostrarEstado("ERROR: UnityTransport NULL");
            return;
        }

        if (networkManager.IsListening)
        {
            MostrarEstado("El NetworkManager ya está iniciado.");
            return;
        }

        if (OnlineNetworkClient.Instance == null)
        {
            Debug.LogError("[OnlineMultiplayer] OnlineNetworkClient.Instance es NULL.");
            MostrarEstado("ERROR: OnlineNetworkClient NULL");
            return;
        }

        IPAddress ip;

        if (!IPAddress.TryParse(address, out ip))
        {
            Debug.LogError(
                "[OnlineMultiplayer] Dirección IP no válida: " +
                address);

            MostrarEstado("ERROR: IP NO VÁLIDA");
            return;
        }

        OnlineNetworkClient.Instance.JoinGame(
            ip,
            port,
            "");

        MostrarEstado("CLIENTE INICIADO");

        ActualizarDiagnostico();
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log("[OnlineMultiplayer] Cliente conectado: " + clientId);

        if (networkManager != null && networkManager.IsHost)
        {
            MostrarEstado("CLIENTE CONECTADO: " + clientId);
        }
        else
        {
            MostrarEstado("CLIENTE CONECTADO");
        }

        ActualizarDiagnostico();
    }

    private void OnClientDisconnect(ulong clientId)
    {
        Debug.Log("[OnlineMultiplayer] Cliente desconectado: " + clientId);

        MostrarEstado("CLIENTE DESCONECTADO: " + clientId);

        ActualizarDiagnostico();
    }

    private void OnTransportFailure()
    {
        Debug.LogError("[OnlineMultiplayer] TRANSPORT FAILURE");

        MostrarEstado("TRANSPORT FAILURE");

        ActualizarDiagnostico();
    }

    private void ActualizarDiagnostico()
    {
        if (networkManager == null)
        {
            networkManager = NetworkManager.Singleton;
        }

        if (networkManager == null)
        {
            if (onlineDiagnosticText != null)
            {
                onlineDiagnosticText.text = "NetworkManager: NULL";
            }

            return;
        }

        if (transport == null)
        {
            transport = networkManager.GetComponent<UnityTransport>();
        }

        string modo = "DETENIDO";

        if (networkManager.IsHost)
        {
            modo = "HOST";
        }
        else if (networkManager.IsClient)
        {
            modo = "CLIENTE";
        }
        else if (networkManager.IsServer)
        {
            modo = "SERVER";
        }

        ulong clientId = networkManager.IsListening
            ? networkManager.LocalClientId
            : 0;

        int clientesRemotos = 0;

        if (networkManager.IsServer && networkManager.ConnectedClientsIds != null)
        {
            clientesRemotos = Mathf.Max(
                0,
                networkManager.ConnectedClientsIds.Count - 1
            );
        }

        string transportEstado = transport != null && transport.enabled
            ? "ENABLED"
            : "NULL/DISABLED";

        string texto =
            "Modo: " + modo + "\n" +
            "IsListening: " + networkManager.IsListening + "\n" +
            "IsHost: " + networkManager.IsHost + "\n" +
            "IsClient: " + networkManager.IsClient + "\n" +
            "LocalClientId: " + clientId + "\n" +
            "Clientes remotos: " + clientesRemotos + "\n" +
            "UnityTransport: " + transportEstado + "\n" +
            "Address: " + address + "\n" +
            "Port: " + port;

        if (onlineDiagnosticText != null)
        {
            onlineDiagnosticText.text = texto;
        }

        if (onlineClientsText != null)
        {
            onlineClientsText.text = "Clientes: " + clientesRemotos;
        }
    }

    private void MostrarEstado(string mensaje)
    {
        Debug.Log("[OnlineMultiplayer] " + mensaje);

        if (onlineStatusText != null)
        {
            onlineStatusText.text = mensaje;
        }
    }

    public void Salir()
    {
        if (networkManager == null)
        {
            networkManager = NetworkManager.Singleton;
        }

        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        MostrarEstado("PARTIDA CERRADA");

        ActualizarDiagnostico();
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
            networkManager.OnTransportFailure -= OnTransportFailure;
        }
    }
}