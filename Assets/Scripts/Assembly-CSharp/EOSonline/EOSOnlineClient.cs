using System;
using UnityEngine;
using Epic.OnlineServices;

public class EOSOnlineClient : MonoBehaviour
{
    public static EOSOnlineClient Instance;

    private ProductUserId hostUserId;

    public bool IsConnected { get; private set; }

    public ProductUserId HostUserId =>
        hostUserId;

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
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
        {
            return;
        }

        EOSOnlineTransport.Instance.OnPeerConnected -=
            OnPeerConnected;

        EOSOnlineTransport.Instance.OnPeerDisconnected -=
            OnPeerDisconnected;

        EOSOnlineTransport.Instance.OnPacketReceived -=
            OnPacketReceived;

        EOSOnlineTransport.Instance.OnPeerConnected +=
            OnPeerConnected;

        EOSOnlineTransport.Instance.OnPeerDisconnected +=
            OnPeerDisconnected;

        EOSOnlineTransport.Instance.OnPacketReceived +=
            OnPacketReceived;
    }

    private void UnsubscribeTransport()
    {
        if (EOSOnlineTransport.Instance == null)
        {
            return;
        }

        EOSOnlineTransport.Instance.OnPeerConnected -=
            OnPeerConnected;

        EOSOnlineTransport.Instance.OnPeerDisconnected -=
            OnPeerDisconnected;

        EOSOnlineTransport.Instance.OnPacketReceived -=
            OnPacketReceived;
    }

    public bool ConnectToHost(
        ProductUserId productUserId)
    {
        if (productUserId == null ||
            !productUserId.IsValid())
        {
            Debug.LogError(
                "[EOS CLIENT] ProductUserId del host inválido."
            );

            return false;
        }

        if (EOSOnlineTransport.Instance == null)
        {
            Debug.LogError(
                "[EOS CLIENT] EOSOnlineTransport no existe."
            );

            return false;
        }

        if (IsConnected)
        {
            Debug.LogWarning(
                "[EOS CLIENT] Ya está conectado al host."
            );

            return true;
        }

        hostUserId =
            productUserId;

        Debug.Log(
            "[EOS CLIENT] Conectando al host: " +
            hostUserId
        );

        bool result =
            EOSOnlineTransport.Instance.StartClient(
                hostUserId
            );

        if (!result)
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

    private void OnPeerConnected(
        ProductUserId playerId)
    {
        if (hostUserId == null)
        {
            return;
        }

        if (playerId != hostUserId)
        {
            return;
        }

        IsConnected = true;

        Debug.Log(
            "[EOS CLIENT] CONECTADO AL SERVIDOR."
        );

        Debug.Log(
            "[EOS CLIENT] Host: " +
            hostUserId
        );
    }

    private void OnPeerDisconnected(
        ProductUserId playerId)
    {
        if (hostUserId == null)
        {
            return;
        }

        if (playerId != hostUserId)
        {
            return;
        }

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
            packet.Length == 0)
        {
            return;
        }

        if (hostUserId != null &&
            sender != hostUserId)
        {
            return;
        }

        string text = null;

        try
        {
            text =
                System.Text.Encoding.UTF8.GetString(
                    packet
                );
        }
        catch
        {
        }

        if (text == "EOS_WELCOME")
        {
            Debug.Log(
                "[EOS CLIENT] Servidor aceptó la conexión."
            );

            return;
        }

        if (text != null &&
            text.StartsWith(
                "EOS_PLAYERS|",
                StringComparison.Ordinal))
        {
            Debug.Log(
                "[EOS CLIENT] Lista de jugadores: " +
                text
            );

            return;
        }

        OnGamePacket(
            packet
        );
    }

    private void OnGamePacket(
        byte[] packet)
    {
        /*
         * Este es el punto donde conectaremos
         * el protocolo actual de SocketClient.
         *
         * No modificamos todavía SocketClient.cs.
         */
    }

    public void Send(
        byte[] packet)
    {
        if (!IsConnected)
        {
            Debug.LogWarning(
                "[EOS CLIENT] Todavía no estás conectado."
            );

            return;
        }

        if (hostUserId == null ||
            !hostUserId.IsValid())
        {
            return;
        }

        if (EOSOnlineTransport.Instance == null)
        {
            return;
        }

        EOSOnlineTransport.Instance.SendToHost(
            hostUserId,
            packet
        );
    }

    public void SendText(
        string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Send(
            System.Text.Encoding.UTF8.GetBytes(
                text
            )
        );
    }

    public void Disconnect()
    {
        /*
         * El transporte es el único responsable
         * del estado P2P.
         *
         * EOSOnlineSession se encarga del orden:
         * Server -> Client -> Transport -> Session.
         *
         * Por eso NO llamamos Transport.Disconnect()
         * aquí.
         */

        IsConnected = false;
        hostUserId = null;

        Debug.Log(
            "[EOS CLIENT] Estado del cliente limpiado."
        );
    }

    private void OnApplicationQuit()
    {
        /*
         * No tocamos el transporte aquí.
         *
         * EOSOnlineTransport es el único encargado
         * del ciclo de vida P2P al cerrar Unity.
         */
        IsConnected = false;
        hostUserId = null;
    }

    private void OnDestroy()
    {
        UnsubscribeTransport();

        IsConnected = false;
        hostUserId = null;

        if (Instance == this)
        {
            Instance = null;
        }
    }
}