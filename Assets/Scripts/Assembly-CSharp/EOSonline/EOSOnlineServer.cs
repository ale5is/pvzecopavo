using System.Collections.Generic;
using UnityEngine;
using Epic.OnlineServices;

public class EOSOnlineServer : MonoBehaviour
{
    public static EOSOnlineServer Instance;

    [Header("Servidor EOS")]
    [SerializeField] private int maxPlayers = 4;

    private readonly List<ProductUserId> players =
        new List<ProductUserId>();

    public bool IsRunning { get; private set; }

    public ProductUserId LocalHostId
    {
        get
        {
            return EOSOnlineTransport.Instance != null
                ? EOSOnlineTransport.Instance.LocalUserId
                : null;
        }
    }

    public IReadOnlyList<ProductUserId> Players =>
        players;

    public int MaxPlayers => maxPlayers;

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
        if (EOSOnlineTransport.Instance != null)
        {
            EOSOnlineTransport.Instance.OnPeerConnected +=
                OnPeerConnected;

            EOSOnlineTransport.Instance.OnPeerDisconnected +=
                OnPeerDisconnected;

            EOSOnlineTransport.Instance.OnPacketReceived +=
                OnPacketReceived;
        }
    }

    private void OnDisable()
    {
        if (EOSOnlineTransport.Instance != null)
        {
            EOSOnlineTransport.Instance.OnPeerConnected -=
                OnPeerConnected;

            EOSOnlineTransport.Instance.OnPeerDisconnected -=
                OnPeerDisconnected;

            EOSOnlineTransport.Instance.OnPacketReceived -=
                OnPacketReceived;
        }
    }

    public bool StartServer()
    {
        if (IsRunning)
        {
            return true;
        }

        if (EOSOnlineTransport.Instance == null)
        {
            Debug.LogError(
                "[EOS SERVER] EOSOnlineTransport no existe."
            );

            return false;
        }

        if (!EOSOnlineTransport.Instance.StartHost())
        {
            return false;
        }

        players.Clear();

        ProductUserId localId =
            EOSOnlineTransport.Instance.LocalUserId;

        if (localId == null ||
            !localId.IsValid())
        {
            Debug.LogError(
                "[EOS SERVER] ProductUserId local inválido."
            );

            EOSOnlineTransport.Instance.Disconnect();

            return false;
        }

        players.Add(localId);

        IsRunning = true;

        Debug.Log(
            "[EOS SERVER] SERVIDOR ONLINE INICIADO."
        );

        Debug.Log(
            "[EOS SERVER] Host ProductUserId: " +
            localId
        );

        Debug.Log(
            "[EOS SERVER] Capacidad: " +
            maxPlayers
        );

        return true;
    }

    private void OnPeerConnected(
        ProductUserId playerId)
    {
        if (!IsRunning)
        {
            return;
        }

        if (playerId == null ||
            !playerId.IsValid())
        {
            return;
        }

        if (players.Contains(playerId))
        {
            return;
        }

        if (players.Count >= maxPlayers)
        {
            Debug.LogWarning(
                "[EOS SERVER] Servidor lleno."
            );

            return;
        }

        players.Add(playerId);

        Debug.Log(
            "[EOS SERVER] Jugador conectado: " +
            playerId
        );

        Debug.Log(
            "[EOS SERVER] Jugadores: " +
            players.Count +
            "/" +
            maxPlayers
        );

        SendWelcome(playerId);

        SendPlayerList();
    }

    private void OnPeerDisconnected(
        ProductUserId playerId)
    {
        if (playerId == null)
        {
            return;
        }

        players.Remove(playerId);

        Debug.Log(
            "[EOS SERVER] Jugador desconectado: " +
            playerId
        );

        if (IsRunning)
        {
            SendPlayerList();
        }
    }

    private void OnPacketReceived(
        ProductUserId sender,
        byte[] packet)
    {
        if (!IsRunning)
        {
            return;
        }

        if (sender == null ||
            packet == null ||
            packet.Length == 0)
        {
            return;
        }

        if (!players.Contains(sender))
        {
            return;
        }

        Debug.Log(
            "[EOS SERVER] Paquete recibido de " +
            sender +
            " | Bytes: " +
            packet.Length
        );

        BroadcastExcept(
            sender,
            packet
        );

        OnGamePacket(
            sender,
            packet
        );
    }

    private void SendWelcome(
        ProductUserId playerId)
    {
        byte[] packet =
            System.Text.Encoding.UTF8.GetBytes(
                "EOS_WELCOME"
            );

        EOSOnlineTransport.Instance.Send(
            playerId,
            packet
        );
    }

    private void SendPlayerList()
    {
        string data =
            "EOS_PLAYERS|" +
            string.Join(
                ",",
                players
            );

        byte[] packet =
            System.Text.Encoding.UTF8.GetBytes(
                data
            );

        foreach (ProductUserId player in players)
        {
            EOSOnlineTransport.Instance.Send(
                player,
                packet
            );
        }
    }

    private void BroadcastExcept(
        ProductUserId excludedPlayer,
        byte[] packet)
    {
        foreach (ProductUserId player in players)
        {
            if (player == null ||
                !player.IsValid())
            {
                continue;
            }

            if (player == excludedPlayer)
            {
                continue;
            }

            EOSOnlineTransport.Instance.Send(
                player,
                packet
            );
        }
    }

    private void OnGamePacket(
        ProductUserId sender,
        byte[] packet)
    {
        /*
         * Este es el punto donde conectaremos
         * el protocolo actual de SocketServer.
         *
         * No modificamos todavía SocketServer.cs.
         */
    }

    public void SendToPlayer(
        ProductUserId playerId,
        byte[] packet)
    {
        if (!IsRunning)
        {
            return;
        }

        if (!players.Contains(playerId))
        {
            return;
        }

        EOSOnlineTransport.Instance.Send(
            playerId,
            packet
        );
    }

    public void Broadcast(
        byte[] packet)
    {
        if (!IsRunning)
        {
            return;
        }

        foreach (ProductUserId player in players)
        {
            if (player == LocalHostId)
            {
                continue;
            }

            EOSOnlineTransport.Instance.Send(
                player,
                packet
            );
        }
    }

    public bool HasPlayer(
        ProductUserId playerId)
    {
        return players.Contains(
            playerId
        );
    }

    public void StopServer()
    {
        if (!IsRunning)
        {
            return;
        }

        players.Clear();

        EOSOnlineTransport.Instance?.Disconnect();

        IsRunning = false;

        Debug.Log(
            "[EOS SERVER] SERVIDOR ONLINE CERRADO."
        );
    }

    private void OnApplicationQuit()
    {
        StopServer();
    }
}