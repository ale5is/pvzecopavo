using System;
using System.Text;
using UnityEngine;
using Epic.OnlineServices;

public class EOSOnlineClient : MonoBehaviour
{
    public static EOSOnlineClient Instance;

    private ProductUserId hostUserId;

    public bool IsConnected { get; private set; }

    public ProductUserId HostUserId => hostUserId;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SubscribeTransport();
    }

    private void OnDisable()
    {
        UnsubscribeTransport();
    }

    private void SubscribeTransport()
    {
        if (EOSOnlineTransport.Instance == null)
            return;

        EOSOnlineTransport.Instance.OnPeerConnected -= OnPeerConnected;
        EOSOnlineTransport.Instance.OnPeerDisconnected -= OnPeerDisconnected;
        EOSOnlineTransport.Instance.OnPacketReceived -= OnPacketReceived;

        EOSOnlineTransport.Instance.OnPeerConnected += OnPeerConnected;
        EOSOnlineTransport.Instance.OnPeerDisconnected += OnPeerDisconnected;
        EOSOnlineTransport.Instance.OnPacketReceived += OnPacketReceived;
    }

    private void UnsubscribeTransport()
    {
        if (EOSOnlineTransport.Instance == null)
            return;

        EOSOnlineTransport.Instance.OnPeerConnected -= OnPeerConnected;
        EOSOnlineTransport.Instance.OnPeerDisconnected -= OnPeerDisconnected;
        EOSOnlineTransport.Instance.OnPacketReceived -= OnPacketReceived;
    }

    public bool ConnectToHost(ProductUserId productUserId)
    {
        if (productUserId == null || !productUserId.IsValid())
        {
            Debug.LogError("[EOS CLIENT] ProductUserId del host inválido.");
            return false;
        }

        if (EOSOnlineTransport.Instance == null)
        {
            Debug.LogError("[EOS CLIENT] EOSOnlineTransport no existe.");
            return false;
        }

        if (IsConnected)
            return true;

        hostUserId = productUserId;

        Debug.Log(
            "[EOS CLIENT] Conectando al host: " +
            hostUserId
        );

        if (!EOSOnlineTransport.Instance.StartClient(hostUserId))
        {
            hostUserId = null;
            IsConnected = false;

            Debug.LogError(
                "[EOS CLIENT] No se pudo iniciar la conexión."
            );

            return false;
        }

        return true;
    }

    private void OnPeerConnected(ProductUserId playerId)
    {
        if (hostUserId == null ||
            playerId != hostUserId)
            return;

        IsConnected = true;

        Debug.Log(
            "[EOS CLIENT] CONECTADO AL SERVIDOR: " +
            hostUserId
        );
    }

    private void OnPeerDisconnected(ProductUserId playerId)
    {
        if (hostUserId == null ||
            playerId != hostUserId)
            return;

        IsConnected = false;

        Debug.LogWarning(
            "[EOS CLIENT] CONEXIÓN CON EL SERVIDOR PERDIDA."
        );
    }

    private void OnPacketReceived(
        ProductUserId sender,
        byte[] packet)
    {
        if (sender == null ||
            packet == null ||
            packet.Length == 0 ||
            (hostUserId != null && sender != hostUserId))
            return;

        string text;

        try
        {
            text = Encoding.UTF8.GetString(packet);
        }
        catch
        {
            OnGamePacket(packet);
            return;
        }

        if (text == "EOS_WELCOME")
        {
            Debug.Log(
                "[EOS CLIENT] Servidor aceptó la conexión."
            );

            return;
        }

        if (text.StartsWith(
            "EOS_PLAYERS|",
            StringComparison.Ordinal))
        {
            Debug.Log(
                "[EOS CLIENT] Lista de jugadores: " +
                text
            );

            return;
        }

        OnGamePacket(packet);
    }

    private void OnGamePacket(byte[] packet)
    {
        // Aquí se conectará posteriormente SocketClient.
    }

    public void Send(byte[] packet)
    {
        if (!IsConnected)
        {
            Debug.LogWarning(
                "[EOS CLIENT] Todavía no estás conectado."
            );

            return;
        }

        if (hostUserId == null ||
            !hostUserId.IsValid() ||
            EOSOnlineTransport.Instance == null)
            return;

        EOSOnlineTransport.Instance.SendToHost(
            hostUserId,
            packet
        );
    }

    public void SendText(string text)
    {
        if (!string.IsNullOrEmpty(text))
            Send(Encoding.UTF8.GetBytes(text));
    }

    public void Disconnect()
    {
        IsConnected = false;
        hostUserId = null;

        Debug.Log(
            "[EOS CLIENT] Estado del cliente limpiado."
        );
    }

    private void OnApplicationQuit()
    {
        IsConnected = false;
        hostUserId = null;
    }

    private void OnDestroy()
    {
        UnsubscribeTransport();

        IsConnected = false;
        hostUserId = null;

        if (Instance == this)
            Instance = null;
    }
}