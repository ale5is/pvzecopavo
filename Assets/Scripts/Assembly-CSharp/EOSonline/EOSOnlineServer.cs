using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Epic.OnlineServices;

public class EOSOnlineServer : MonoBehaviour
{
    public static EOSOnlineServer Instance;

    [SerializeField] private int maxPlayers = 4;

    private readonly List<ProductUserId> players = new List<ProductUserId>();

    public bool IsRunning { get; private set; }

    public ProductUserId LocalHostId =>
        EOSOnlineTransport.Instance != null
            ? EOSOnlineTransport.Instance.LocalUserId
            : null;

    public IReadOnlyList<ProductUserId> Players => players;
    public int MaxPlayers => maxPlayers;

    private bool applicationQuitting;

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

    public bool StartServer()
    {
        if (IsRunning)
            return true;

        if (EOSOnlineTransport.Instance == null)
        {
            Debug.LogError("[EOS SERVER] EOSOnlineTransport no existe.");
            return false;
        }

        ProductUserId localId =
            EOSOnlineTransport.Instance.LocalUserId;

        if (localId == null || !localId.IsValid())
        {
            Debug.LogError("[EOS SERVER] ProductUserId local inválido.");
            return false;
        }

        players.Clear();
        players.Add(localId);
        IsRunning = true;

        SubscribeTransport();

        Debug.Log(
            "[EOS SERVER] ONLINE | Host: " +
            localId +
            " | Jugadores: 1/" +
            maxPlayers
        );

        return true;
    }

    private void OnPeerConnected(ProductUserId playerId)
    {
        if (!IsRunning ||
            playerId == null ||
            !playerId.IsValid() ||
            players.Contains(playerId))
            return;

        if (players.Count >= maxPlayers)
        {
            Debug.LogWarning("[EOS SERVER] Servidor lleno.");
            return;
        }

        players.Add(playerId);

        Debug.Log(
            "[EOS SERVER] Jugador conectado: " +
            playerId +
            " | " +
            players.Count +
            "/" +
            maxPlayers
        );

        SendWelcome(playerId);
        SendPlayerList();
    }

    private void OnPeerDisconnected(ProductUserId playerId)
    {
        if (playerId == null)
            return;

        players.Remove(playerId);

        Debug.Log(
            "[EOS SERVER] Jugador desconectado: " +
            playerId
        );

        if (IsRunning)
            SendPlayerList();
    }

    private void OnPacketReceived(
        ProductUserId sender,
        byte[] packet)
    {
        if (!IsRunning ||
            sender == null ||
            packet == null ||
            packet.Length == 0 ||
            !players.Contains(sender))
            return;

        BroadcastExcept(sender, packet);
        OnGamePacket(sender, packet);
    }

    private void SendWelcome(ProductUserId playerId)
    {
        if (!IsRunning ||
            EOSOnlineTransport.Instance == null)
            return;

        EOSOnlineTransport.Instance.Send(
            playerId,
            Encoding.UTF8.GetBytes("EOS_WELCOME")
        );
    }

    private void SendPlayerList()
    {
        if (!IsRunning ||
            EOSOnlineTransport.Instance == null)
            return;

        byte[] packet = Encoding.UTF8.GetBytes(
            "EOS_PLAYERS|" +
            string.Join(",", players)
        );

        foreach (ProductUserId player in players)
        {
            if (IsValidPlayer(player))
                EOSOnlineTransport.Instance.Send(player, packet);
        }
    }

    private void BroadcastExcept(
        ProductUserId excludedPlayer,
        byte[] packet)
    {
        if (!IsRunning ||
            EOSOnlineTransport.Instance == null)
            return;

        foreach (ProductUserId player in players)
        {
            if (!IsValidPlayer(player) ||
                player == excludedPlayer)
                continue;

            EOSOnlineTransport.Instance.Send(player, packet);
        }
    }

    private bool IsValidPlayer(ProductUserId player)
    {
        return player != null && player.IsValid();
    }

    private void OnGamePacket(
        ProductUserId sender,
        byte[] packet)
    {
        // Aquí se conectará posteriormente el protocolo de SocketServer.
    }

    public void SendToPlayer(
        ProductUserId playerId,
        byte[] packet)
    {
        if (!IsRunning ||
            !IsValidPlayer(playerId) ||
            !players.Contains(playerId) ||
            EOSOnlineTransport.Instance == null)
            return;

        EOSOnlineTransport.Instance.Send(playerId, packet);
    }

    public void Broadcast(byte[] packet)
    {
        if (!IsRunning ||
            EOSOnlineTransport.Instance == null)
            return;

        foreach (ProductUserId player in players)
        {
            if (!IsValidPlayer(player) ||
                player == LocalHostId)
                continue;

            EOSOnlineTransport.Instance.Send(player, packet);
        }
    }

    public bool HasPlayer(ProductUserId playerId)
    {
        return IsValidPlayer(playerId) &&
               players.Contains(playerId);
    }

    public void StopServer()
    {
        if (!IsRunning)
        {
            players.Clear();
            return;
        }

        IsRunning = false;
        players.Clear();

        Debug.Log("[EOS SERVER] SERVIDOR CERRADO.");
    }

    private void OnApplicationQuit()
    {
        if (applicationQuitting)
            return;

        applicationQuitting = true;
        StopServer();
    }

    private void OnDestroy()
    {
        UnsubscribeTransport();

        if (Instance == this)
            Instance = null;
    }
}