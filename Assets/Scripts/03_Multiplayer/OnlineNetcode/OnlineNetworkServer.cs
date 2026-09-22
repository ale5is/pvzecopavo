using SocketSave;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class OnlineNetworkServer : NetworkBehaviour
{
    public static OnlineNetworkServer Instance;

    private const string MessageName = "PVZ_ONLINE_MESSAGE";
    private const int MaxPacket = 1048576;
    private const int DiscoveryPort = 47777;
    private const string DiscoveryRequest = "PVZ_DISCOVERY_REQUEST";
    private const string DiscoveryResponse = "PVZ_DISCOVERY_RESPONSE";
    private const string RelayConnectionType = "udp";

    [Header("UI")]
    [SerializeField] private BattlePlayerList battlePlayerList;
    [SerializeField] private PlayerList playerList;

    public bool isServerOpen;

    private readonly List<PlayerInfo> players = new();
    private readonly Dictionary<ulong, PlayerInfo> clientPlayers = new();
    private readonly List<PlayerInfo> reConnectPlayer = new();
    private readonly List<PlayerInfo> handQuitPlayer = new();

    private PlayerInfo hostPlayer;
    private string hostPassword = "";

    private UdpClient discoverySocket;
    private Thread discoveryThread;
    private volatile bool discoveryRunning;

    private int discoveryGamePort = 7777;
    private string discoveryHostName = "Host";
    private string discoveryVersion = "";
    private bool discoveryPasswordRequired;

    private int reConnectCode;
    private int itemId;

    public int noHostPlayerNum => players.Count;
    public int ItemId => ++itemId;

    private void Awake()
    {
        Instance = this;

        battlePlayerList ??= BattlePlayerList.Instance;
        playerList ??= PlayerList.Instance;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
            return;

        RegisterMessageHandler();
        RegisterNetworkCallbacks();

        isServerOpen = true;

        InitializeHostPlayer();

        StartCoroutine(SendHeartbeat());
        StartCoroutine(CheckConnect());

        UpdatePlayerLists();
    }

    public override void OnNetworkDespawn()
    {
        UnregisterNetworkCallbacks();

        if (IsServer)
            UnregisterMessageHandler();

        StopDiscovery();
        StopAllCoroutines();

        if (IsServer)
            ClearServerState(true);

        base.OnNetworkDespawn();
    }

    private void RegisterNetworkCallbacks()
    {
        var manager = NetworkManager.Singleton;

        if (manager == null)
            return;

        manager.OnClientDisconnectCallback -= HandleClientDisconnected;
        manager.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void UnregisterNetworkCallbacks()
    {
        var manager = NetworkManager.Singleton;

        if (manager == null)
            return;

        manager.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    private void RegisterMessageHandler()
    {
        var manager = NetworkManager.Singleton;

        if (manager?.CustomMessagingManager == null)
            return;

        try
        {
            manager.CustomMessagingManager
                .UnregisterNamedMessageHandler(MessageName);
        }
        catch
        {
        }

        manager.CustomMessagingManager.RegisterNamedMessageHandler(
            MessageName,
            ReceiveNamedMessage);
    }

    private void UnregisterMessageHandler()
    {
        var manager = NetworkManager.Singleton;

        if (manager?.CustomMessagingManager == null)
            return;

        try
        {
            manager.CustomMessagingManager
                .UnregisterNamedMessageHandler(MessageName);
        }
        catch
        {
        }
    }

    private void ReceiveNamedMessage(
        ulong clientId,
        FastBufferReader reader)
    {
        try
        {
            reader.ReadValueSafe(out byte type1);
            reader.ReadValueSafe(out byte type2);
            reader.ReadValueSafe(out string content);

            ReceiveClientMessage(
                clientId,
                type1,
                type2,
                content ?? "");
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "[OnlineNetworkServer] Error leyendo mensaje: " +
                ex);
        }
    }

    private void InitializeHostPlayer()
    {
        var gm = GameManager.Instance;

        if (gm == null)
            return;

        string name =
            gm.LocalPlayerSave?.playerName ??
            "Host";

        hostPlayer = new PlayerInfo
        {
            Name = name,
            VersionCode = gm.VersionCode,
            Password = hostPassword
        };

        gm.HostName = name;
        gm.isOnline = true;
    }

    private void ClearPlayerUI()
    {
        battlePlayerList?.UpdatePlayerList(
            null,
            new List<PlayerInfo>());

        playerList?.UpdatePlayerList(
            null,
            new List<PlayerInfo>());
    }

    private void ClearServerState(
        bool clearUI)
    {
        isServerOpen = false;

        if (GameManager.Instance != null)
            GameManager.Instance.isOnline = false;

        players.Clear();
        clientPlayers.Clear();
        reConnectPlayer.Clear();
        handQuitPlayer.Clear();

        hostPlayer = null;
        hostPassword = "";

        discoveryGamePort = 7777;
        discoveryHostName = "Host";
        discoveryVersion = "";
        discoveryPasswordRequired = false;

        reConnectCode = 0;
        itemId = 0;

        if (clearUI)
            ClearPlayerUI();
    }

    public void StartServer(
        IPAddress ip,
        int port,
        string password = "")
    {
        _ = StartLocalServer(
            ip,
            port,
            password);
    }

    public async System.Threading.Tasks.Task<bool> StartLocalServer(
        IPAddress ip,
        int port,
        string password = "")
    {
        var gm = GameManager.Instance;

        if (gm == null ||
            gm.isOnline ||
            port < 1 ||
            port > ushort.MaxValue)
        {
            return false;
        }

        var manager = NetworkManager.Singleton;

        if (manager == null)
            return false;

        var transport =
            manager.GetComponent<UnityTransport>();

        if (transport == null)
            return false;

        StopAllCoroutines();
        StopDiscovery();
        ClearServerState(true);

        hostPassword =
            (password ?? "").Trim();

        discoveryGamePort = port;

        discoveryHostName =
            gm.LocalPlayerSave?.playerName ??
            "Host";

        discoveryVersion =
            gm.VersionCode ??
            "";

        discoveryPasswordRequired =
            !string.IsNullOrEmpty(
                hostPassword);

        string address =
            ip?.ToString() ??
            "0.0.0.0";

        transport.SetConnectionData(
            address,
            (ushort)port,
            "0.0.0.0");

        if (!manager.StartHost())
        {
            ClearServerState(true);
            return false;
        }

        isServerOpen = true;

        InitializeHostPlayer();
        StartDiscovery();
        UpdatePlayerLists();

        return true;
    }

    public async System.Threading.Tasks.Task<string> StartRelayServer(
        int maxConnections,
        string password = "")
    {
        var gm = GameManager.Instance;

        if (gm == null ||
            gm.isOnline)
        {
            return "";
        }

        var manager =
            NetworkManager.Singleton;

        if (manager == null)
            return "";

        var transport =
            manager.GetComponent<UnityTransport>();

        if (transport == null)
            return "";

        var services =
            UnityServicesInitializer.Instance;

        if (services == null)
            return "";

        try
        {
            if (!await services.InitializeServices())
                return "";

            if (manager.IsListening)
                manager.Shutdown();

            StopAllCoroutines();
            StopDiscovery();
            ClearServerState(true);

            hostPassword =
                (password ?? "").Trim();

            Allocation allocation =
                await RelayService.Instance
                    .CreateAllocationAsync(
                        Mathf.Clamp(
                            maxConnections,
                            1,
                            100));

            transport.SetRelayServerData(
                new RelayServerData(
                    allocation,
                    RelayConnectionType));

            string joinCode =
                await RelayService.Instance
                    .GetJoinCodeAsync(
                        allocation.AllocationId);

            if (!manager.StartHost())
            {
                ClearServerState(true);
                return "";
            }

            isServerOpen = true;

            InitializeHostPlayer();
            UpdatePlayerLists();

            Debug.Log(
                "[OnlineNetworkServer] Relay creado. " +
                "JoinCode=" +
                joinCode);

            return joinCode;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "[OnlineNetworkServer] Error creando Relay: " +
                ex);

            if (manager.IsListening)
                manager.Shutdown();

            ClearServerState(true);

            return "";
        }
    }

    private void StartDiscovery()
    {
        StopDiscovery();

        discoveryRunning = true;

        discoveryThread =
            new Thread(DiscoveryLoop)
            {
                IsBackground = true
            };

        discoveryThread.Start();
    }

    private void DiscoveryLoop()
    {
        try
        {
            using UdpClient socket =
                new(DiscoveryPort);

            socket.EnableBroadcast = true;
            socket.Client.ReceiveTimeout = 500;
            discoverySocket = socket;

            while (discoveryRunning)
            {
                try
                {
                    IPEndPoint endpoint =
                        new(
                            IPAddress.Any,
                            0);

                    byte[] data =
                        socket.Receive(
                            ref endpoint);

                    string message =
                        Encoding.UTF8.GetString(data);

                    if (message != DiscoveryRequest)
                        continue;

                    string json =
                        "{\"name\":\"" +
                        EscapeJson(discoveryHostName) +
                        "\",\"version\":\"" +
                        EscapeJson(discoveryVersion) +
                        "\",\"port\":" +
                        discoveryGamePort +
                        ",\"passwordRequired\":" +
                        (discoveryPasswordRequired
                            ? "true"
                            : "false") +
                        "}";

                    byte[] response =
                        Encoding.UTF8.GetBytes(
                            DiscoveryResponse +
                            "|" +
                            json);

                    socket.Send(
                        response,
                        response.Length,
                        endpoint);
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            if (discoveryRunning)
            {
                Debug.LogWarning(
                    "[OnlineNetworkServer] Error en descubrimiento: " +
                    ex.Message);
            }
        }
        finally
        {
            discoverySocket = null;
            discoveryRunning = false;
        }
    }

    private static string EscapeJson(
        string value)
    {
        return (value ?? "")
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private void StopDiscovery()
    {
        discoveryRunning = false;

        try
        {
            discoverySocket?.Close();
        }
        catch
        {
        }

        discoverySocket = null;

        if (discoveryThread != null &&
            discoveryThread.IsAlive)
        {
            try
            {
                discoveryThread.Join(150);
            }
            catch
            {
            }
        }

        discoveryThread = null;
    }

    public void CloseServer()
    {
        StopDiscovery();
        StopAllCoroutines();

        var manager =
            NetworkManager.Singleton;

        if (manager != null &&
            manager.IsListening)
        {
            manager.Shutdown();
        }

        ClearServerState(true);
    }

    private void OnApplicationQuit()
    {
        try
        {
            StopAllCoroutines();
            StopDiscovery();
            ClearServerState(false);
        }
        catch
        {
        }
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        try
        {
            UnregisterNetworkCallbacks();
            StopAllCoroutines();
            StopDiscovery();
            ClearServerState(false);
        }
        catch
        {
        }

        Instance = null;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReceiveMessageServerRpc(
        byte type1,
        byte type2,
        string content,
        ServerRpcParams rpcParams = default)
    {
        if (!IsServer ||
            !isServerOpen)
            return;

        HandleClientMessage(
            rpcParams.Receive.SenderClientId,
            type1,
            type2,
            content ?? "");
    }

    public void ReceiveClientMessage(
        ulong clientId,
        byte type1,
        byte type2,
        string content)
    {
        if (!IsServer ||
            !isServerOpen)
            return;

        HandleClientMessage(
            clientId,
            type1,
            type2,
            content ?? "");
    }

    private void HandleClientMessage(
        ulong clientId,
        byte type1,
        byte type2,
        string content)
    {
        if (type1 == 0)
        {
            HandleConnectionMessage(
                clientId,
                type2,
                content);

            return;
        }

        if (!clientPlayers.TryGetValue(
                clientId,
                out var player))
        {
            return;
        }

        if (type1 == 1)
        {
            ProcessGameMessage(
                type2,
                content,
                player,
                clientId);

            return;
        }

        if (type1 == 2)
        {
            HandleChatMessage(
                type2,
                content,
                player,
                clientId);
        }
    }

    private void HandleConnectionMessage(
        ulong clientId,
        byte type,
        string msg)
    {
        if (type == 1)
        {
            PlayerInfo info;

            try
            {
                info =
                    JsonUtility.FromJson<PlayerInfo>(
                        msg);
            }
            catch
            {
                info = null;
            }

            if (!ValidatePlayer(
                    info,
                    clientId))
            {
                DisconnectClient(clientId);
                return;
            }

            info.Heartbeat = true;

            clientPlayers[clientId] = info;

            AddPlayerFromClient(
                clientId,
                info);

            return;
        }

        if (type == 2)
        {
            RemovePlayerByClient(
                clientId,
                true);

            return;
        }

        if (type == byte.MaxValue)
        {
            if (clientPlayers.TryGetValue(
                    clientId,
                    out var player))
            {
                player.Heartbeat = true;
            }

            SendMessageToClient(
                clientId,
                0,
                byte.MaxValue);
        }
    }

    private bool ValidatePlayer(
        PlayerInfo info,
        ulong clientId)
    {
        if (info == null)
        {
            SendFailConnectMsg(
                "Información del jugador no válida.",
                clientId);

            return false;
        }

        var gm =
            GameManager.Instance;

        if (gm == null)
        {
            SendFailConnectMsg(
                "GameManager no está disponible.",
                clientId);

            return false;
        }

        if (info.VersionCode != gm.VersionCode)
        {
            SendFailConnectMsg(
                "Diferente a la versión del juego del servidor. " +
                "Versión del juego del servidor :v" +
                gm.VersionCode,
                clientId);

            return false;
        }

        if (CanReConnect(info))
        {
            if (info.ReCntCode != reConnectCode)
            {
                SendFailConnectMsg(
                    "Código de reconexión incorrecto.",
                    clientId);

                return false;
            }

            return true;
        }

        string clientPassword =
            (info.Password ?? "").Trim();

        string serverPassword =
            (hostPassword ?? "").Trim();

        if (!string.IsNullOrEmpty(serverPassword) &&
            !string.Equals(
                clientPassword,
                serverPassword,
                StringComparison.Ordinal))
        {
            SendFailConnectMsg(
                "Contraseña incorrecta; no se pudo unir.",
                clientId);

            return false;
        }

        if (players.Count >= 3)
        {
            SendFailConnectMsg(
                "Server lleno; error al unirse.",
                clientId);

            return false;
        }

        if (hostPlayer?.Name == info.Name)
        {
            SendFailConnectMsg(
                "El nombre entra en conflicto con el de un jugador en línea; " +
                "no se pudo unir a la partida.",
                clientId);

            return false;
        }

        if (LVManager.Instance?.InGame == true)
        {
            SendFailConnectMsg(
                "La partida ya ha comenzado; no se pudo unir.",
                clientId);

            return false;
        }

        if (gm.LocalPlayerSave == null)
        {
            SendFailConnectMsg(
                "No se pudo cargar la información del jugador.",
                clientId);

            return false;
        }

        if (info.CmdEnable !=
            gm.LocalPlayerSave.CmdEnable)
        {
            SendFailConnectMsg(
                info.CmdEnable
                    ? "Has habilitado los comandos, pero el servidor no; no puedes unirte"
                    : "No has habilitado los comandos, pero el servidor sí; no puedes unirte",
                clientId);

            return false;
        }

        if (players.Any(
                p => p?.Name == info.Name))
        {
            SendFailConnectMsg(
                "El nombre entra en conflicto con el de un jugador en línea; " +
                "no se pudo unir a la partida.",
                clientId);

            return false;
        }

        return true;
    }

    private void AddPlayerFromClient(
        ulong clientId,
        PlayerInfo player)
    {
        if (player == null)
            return;

        if (CanReConnect(player))
        {
            players.Add(player);

            reConnectPlayer.RemoveAll(
                p => p?.Name == player.Name);

            ReConnectListChange();

            SendJsonToClient(
                clientId,
                0,
                2,
                new OnlinePlayerInfo
                {
                    HostPlayer = hostPlayer,
                    players = players
                });

            return;
        }

        AddNewPlayer(
            player,
            clientId);

        SendCommandBag(clientId);

        if (PvPSelector.Instance != null)
        {
            SynPvPMode(
                new PvPModeSyn
                {
                    Mode =
                        PvPSelector.Instance.CurrMode
                },
                clientId);

            PvPSelector.Instance.ServerSynTeam();
        }

        SpectatorList.Instance?
            .ServerSynSpectList();
    }

    private void DisconnectClient(
        ulong clientId)
    {
        var manager =
            NetworkManager.Singleton;

        if (manager?.ConnectedClientsIds.Contains(
                clientId) == true)
        {
            manager.DisconnectClient(clientId);
        }
    }

    private void ProcessGameMessage(
        byte type,
        string msg,
        PlayerInfo player,
        ulong clientId)
    {
        if (type == 0)
        {
            PlacePlant(
                JsonUtility.FromJson<PlantSpawn>(msg),
                player,
                clientId);

            return;
        }

        if (type == 1)
        {
            ApplyTool(
                JsonUtility.FromJson<ToolApply>(msg),
                player,
                clientId);

            return;
        }

        if (type == 2)
        {
            SkyManager.Instance?.OnlineCollectSun(
                JsonUtility.FromJson<ClickedSun>(msg));

            return;
        }

        if (type == 3)
        {
            ChangeMap(
                JsonUtility.FromJson<PlayerMap>(msg),
                player);

            return;
        }

        if (type == 4)
        {
            SelectCard(
                JsonUtility.FromJson<SelectCard>(msg),
                player);

            return;
        }

        if (type == 5)
        {
            SelectPrepare(
                JsonUtility.FromJson<SelectPrepare>(msg),
                player);

            return;
        }

        if (type == 6)
        {
            var item =
                JsonUtility.FromJson<SynItem>(msg);

            if (item != null)
                SynItem(item);

            return;
        }

        if (type == 7)
        {
            var preview =
                JsonUtility.FromJson<PlantPreview>(msg);

            if (preview != null)
            {
                BattlePlayerList.Instance?.PreviewPlant(preview);
                PlacePreview(preview, clientId);
            }

            return;
        }

        if (type == 8)
        {
            UpdateCD(
                JsonUtility.FromJson<UpdateCardCD>(msg),
                player,
                clientId);

            return;
        }

        if (type == 9)
        {
            var preview =
                JsonUtility.FromJson<ShovelPreview>(msg);

            if (preview != null)
            {
                BattlePlayerList.Instance?
                    .PreviewShovel(
                        preview.PlayerName,
                        preview.GridPos,
                        preview.isShow);

                ShovelPreview(
                    preview,
                    clientId);
            }

            return;
        }

        if (type == 10)
        {
            PlaceZombie(
                JsonUtility.FromJson<ZombieSpawnApply>(msg),
                player,
                clientId);

            return;
        }

        if (type == 11)
        {
            var preview =
                JsonUtility.FromJson<ZombiePreview>(msg);

            if (preview != null)
            {
                BattlePlayerList.Instance?.PreviewZombie(preview);
                ZombiePreview(preview, clientId);
            }

            return;
        }

        if (type == 12)
        {
            var team =
                JsonUtility.FromJson<JoinTeamApply>(msg);

            if (team != null &&
                PvPSelector.Instance != null)
            {
                if (team.isRed)
                    PvPSelector.Instance.JoinRed(player.Name);
                else
                    PvPSelector.Instance.JoinBlue(player.Name);
            }

            return;
        }

        if (type == 13)
        {
            var spectator =
                JsonUtility.FromJson<JoinSpecApply>(msg);

            if (spectator != null &&
                SpectatorList.Instance != null)
            {
                SpectatorList.Instance.ClientJoinSpect(
                    player.Name,
                    spectator.isJoin);
            }

            return;
        }

        if (type == 14)
        {
            var bag =
                JsonUtility.FromJson<SlotMchBag>(msg);

            if (bag != null)
                SlotMachine.Instance?.ClientSyn(bag);
        }
    }

    private void PlacePlant(
        PlantSpawn spawn,
        PlayerInfo player,
        ulong clientId)
    {
        if (spawn == null ||
            player == null ||
            PlantManager.Instance == null ||
            MapManager.Instance == null ||
            SeedBank.Instance == null)
            return;

        var cd =
            new UpdateCardCD
            {
                CardId = spawn.CardId,
                name = player.Name
            };

        ReversePvP(
            ref spawn.GridPos,
            player.Name);

        var plant =
            PlantManager.Instance.GetNewPlant(
                spawn.plantType);

        var grid =
            MapManager.Instance.GetGridByWorldPos(
                spawn.GridPos);

        if (plant == null ||
            grid == null)
            return;

        if (SeedBank.Instance.CheckPlant(
                plant,
                grid,
                -1,
                player.Name))
        {
            int needSun = -1;
            cd.OK = false;

            if (spawn.SPcode == 2)
            {
                plant.InitForCreate(
                    false,
                    null,
                    false);

                needSun =
                    SeedBank.Instance
                        .GetPlantNc(
                            plant.GetPlantType())
                        .NeedNum;
            }

            SeedBank.Instance.PlantConfirm(
                plant,
                grid,
                needSun,
                spawn.SPcode,
                player.Name);

            SendJson(2, 4, cd);

            BattlePlayerList.Instance?
                .UpdateCardCD(
                    cd.name,
                    cd.CardId,
                    cd.OK);
        }
        else
        {
            cd.OK = true;

            SendJsonToClient(
                clientId,
                2,
                4,
                cd);

            Destroy(plant.gameObject);
        }

        SeedBank.Instance.LikeColumnPlace(
            grid,
            spawn.plantType,
            ZombieType.Nope,
            -1,
            player.Name,
            false);
    }

    private void PlaceZombie(
        ZombieSpawnApply spawn,
        PlayerInfo player,
        ulong clientId)
    {
        if (spawn == null ||
            player == null ||
            ZombieManager.Instance == null ||
            MapManager.Instance == null ||
            SeedBank.Instance == null)
            return;

        var cd =
            new UpdateCardCD
            {
                CardId = spawn.CardId,
                name = player.Name
            };

        ReversePvP(
            ref spawn.GridPos,
            player.Name);

        var zombie =
            ZombieManager.Instance.GetNewZombie(
                spawn.Type);

        var grid =
            MapManager.Instance.GetGridByWorldPos(
                spawn.GridPos);

        if (zombie == null ||
            grid == null)
            return;

        if (SeedBank.Instance.CheckZombie(
                spawn.Type,
                grid,
                -1,
                player.Name))
        {
            cd.OK = false;

            SeedBank.Instance.ZombieConfirm(
                spawn.Type,
                zombie,
                grid,
                -1,
                player.Name,
                spawn.isRat);

            SendJson(2, 4, cd);

            BattlePlayerList.Instance?
                .UpdateCardCD(
                    cd.name,
                    cd.CardId,
                    cd.OK);
        }
        else
        {
            cd.OK = true;

            SendJsonToClient(
                clientId,
                2,
                4,
                cd);

            Destroy(zombie.gameObject);
        }

        SeedBank.Instance.LikeColumnPlace(
            grid,
            PlantType.Nope,
            spawn.Type,
            -1,
            player.Name,
            spawn.isRat);
    }

    private void ReversePvP(
        ref Vector2 pos,
        string player)
    {
        if (LV.Instance?.CurrLVType == LVType.PvP &&
            PvPSelector.Instance != null &&
            !PvPSelector.Instance.IsSameTeam(player))
        {
            pos.x = -pos.x;
        }
    }

    private void ApplyTool(
        ToolApply apply,
        PlayerInfo player,
        ulong clientId)
    {
        if (apply == null ||
            player == null ||
            MapManager.Instance == null)
            return;

        apply.User = player.Name;

        var grid =
            MapManager.Instance.GetGridByWorldPos(
                apply.GridPos);

        if (grid == null)
            return;

        if (apply.type == ToolType.Shovel)
        {
            if (Shovel.Instance?.ClearPlant(
                    grid,
                    apply.GridPos,
                    player.Name) == true)
            {
                BattlePlayerList.Instance?
                    .PlayShovelAnimation(
                        grid.Position,
                        apply.Sound,
                        player.Name);
            }

            SendJson(2, 6, apply);
            return;
        }

        if (apply.type == ToolType.Glove)
        {
            Glove.Instance?.SynClient(
                apply.OnlineId,
                apply.GridPos);
        }
    }

    private void ChangeMap(
        PlayerMap map,
        PlayerInfo player)
    {
        if (map == null ||
            player == null)
            return;

        map.PlayerName = player.Name;

        ChangeMap(map);

        BattlePlayerList.Instance?
            .UpdateMapSprite(
                map.PlayerName,
                map.Pos);
    }

    private void SelectCard(
        SelectCard card,
        PlayerInfo player)
    {
        if (card == null ||
            player == null)
            return;

        card.PlayerName = player.Name;

        SelectCard(card);

        if (BattlePlayerList.Instance == null)
            return;

        if (card.isBack)
        {
            BattlePlayerList.Instance.CancelCard(
                card.PlayerName,
                card.cardId);
        }
        else
        {
            BattlePlayerList.Instance.SelectCard(
                card.PlayerName,
                card.plantType,
                card.zombieType,
                card.noAnim,
                card.cardId);
        }
    }

    private void SelectPrepare(
        SelectPrepare prepare,
        PlayerInfo player)
    {
        if (prepare == null ||
            player == null)
            return;

        prepare.PlayerName = player.Name;

        SelectPrepare(prepare);

        BattlePlayerList.Instance?
            .UpdateState(
                prepare.PlayerName,
                prepare.isPrepare);
    }

    private void UpdateCD(
        UpdateCardCD cd,
        PlayerInfo player,
        ulong clientId)
    {
        if (cd == null ||
            player == null)
            return;

        cd.name = player.Name;

        BattlePlayerList.Instance?
            .UpdateCardCD(
                cd.name,
                cd.CardId,
                cd.OK);

        SendJsonExcept(
            clientId,
            2,
            4,
            cd);
    }

    private void HandleChatMessage(
        byte type,
        string msg,
        PlayerInfo player,
        ulong clientId)
    {
        if (type == 0)
        {
            ChatInput.Instance?.AddMessage(msg);

            SendMessageExcept(
                clientId,
                4,
                0,
                msg);

            return;
        }

        if (type != 1)
            return;

        var chat =
            JsonUtility.FromJson<PrivateChatMsg>(msg);

        if (chat == null ||
            player == null)
            return;

        if (GameManager.Instance?.LocalPlayerSave != null &&
            chat.PlayerName ==
            GameManager.Instance.LocalPlayerSave.playerName)
        {
            ChatInput.Instance?.AddMessage(
                "Jugador" +
                player.Name +
                "Susurro:" +
                chat.content,
                new Color32(
                    123,
                    123,
                    123,
                    255));
        }
        else
        {
            SendPrivateChatMsg(
                chat.PlayerName,
                chat.content,
                player.Name);
        }
    }

    private void SendNamedMessageToClient(
        ulong clientId,
        byte type1,
        byte type2,
        string content)
    {
        var manager =
            NetworkManager.Singleton;

        if (!IsServer ||
            manager == null ||
            !manager.IsListening ||
            manager.CustomMessagingManager == null)
        {
            return;
        }

        content ??= "";

        int contentBytes =
            Encoding.UTF8.GetByteCount(content);

        if (contentBytes > MaxPacket)
        {
            Debug.LogError(
                $"[OnlineNetworkServer] Mensaje demasiado grande. Bytes={contentBytes}");

            return;
        }

        int size =
            contentBytes +
            1024;

        using var writer =
            new FastBufferWriter(
                size,
                Allocator.Temp);

        writer.WriteValueSafe(
            type1);

        writer.WriteValueSafe(
            type2);

        writer.WriteValueSafe(
            content);

        manager.CustomMessagingManager.SendNamedMessage(
            MessageName,
            clientId,
            writer);
    }

    private void SendMessage(
        byte type1,
        byte type2,
        string content = "")
    {
        if (!IsServer)
            return;

        var manager =
            NetworkManager.Singleton;

        if (manager == null)
            return;

        foreach (var clientId in
                 manager.ConnectedClientsIds)
        {
            if (clientId ==
                NetworkManager.ServerClientId)
                continue;

            SendNamedMessageToClient(
                clientId,
                type1,
                type2,
                content);
        }
    }

    private void SendMessageToClient(
        ulong clientId,
        byte type1,
        byte type2,
        string content = "")
    {
        if (!IsServer)
            return;

        var manager =
            NetworkManager.Singleton;

        if (manager?.ConnectedClientsIds.Contains(
                clientId) != true)
            return;

        SendNamedMessageToClient(
            clientId,
            type1,
            type2,
            content);
    }

    private void SendMessageExcept(
        ulong excludedClientId,
        byte type1,
        byte type2,
        string content = "")
    {
        if (!IsServer)
            return;

        var manager =
            NetworkManager.Singleton;

        if (manager == null)
            return;

        foreach (var clientId in
                 manager.ConnectedClientsIds)
        {
            if (clientId == NetworkManager.ServerClientId ||
                clientId == excludedClientId)
                continue;

            SendNamedMessageToClient(
                clientId,
                type1,
                type2,
                content);
        }
    }

    private void SendJson(
        byte type1,
        byte type2,
        object value)
    {
        SendMessage(
            type1,
            type2,
            JsonUtility.ToJson(value));
    }

    private void SendJsonToClient(
        ulong clientId,
        byte type1,
        byte type2,
        object value)
    {
        SendMessageToClient(
            clientId,
            type1,
            type2,
            JsonUtility.ToJson(value));
    }

    private void SendJsonExcept(
        ulong excludedClientId,
        byte type1,
        byte type2,
        object value)
    {
        SendMessageExcept(
            excludedClientId,
            type1,
            type2,
            JsonUtility.ToJson(value));
    }

    private void RemovePlayerByClient(
        ulong clientId,
        bool intentional)
    {
        if (!clientPlayers.TryGetValue(
                clientId,
                out var player))
        {
            return;
        }

        if (intentional &&
            !handQuitPlayer.Any(
                p => p?.Name == player.Name))
        {
            handQuitPlayer.Add(player);
        }

        RemovePlayer(
            player,
            clientId);
    }

    private void RemovePlayer(
        PlayerInfo player,
        ulong clientId)
    {
        if (player == null)
            return;

        clientPlayers.Remove(clientId);

        int index =
            players.FindIndex(
                p => p?.Name == player.Name);

        if (index < 0)
            return;

        player =
            players[index];

        players.RemoveAt(index);

        Debug.Log(
            "[OnlineNetworkServer] Jugador desconectado: " +
            player.Name);

        if (LVManager.Instance?.InGame == true)
        {
            if (handQuitPlayer.Remove(
                    player))
            {
                RemoveDone(player);
                return;
            }

            if (!reConnectPlayer.Any(
                    p => p?.Name == player.Name))
            {
                reConnectPlayer.Add(player);
            }

            ReConnect.Instance?.OpenInit(false);
            ReConnectListChange();

            return;
        }

        RemoveDone(player);
    }

    private void RemovePlayerOnDisconnect(
        ulong clientId)
    {
        if (!clientPlayers.TryGetValue(
                clientId,
                out var player))
        {
            return;
        }

        RemovePlayer(
            player,
            clientId);
    }

    public void HandleClientDisconnected(
        ulong clientId)
    {
        if (!IsServer)
            return;

        Debug.Log(
            "[OnlineNetworkServer] NGO desconectó al cliente: " +
            clientId);

        RemovePlayerOnDisconnect(
            clientId);
    }

    public void SynItem(
        SynItem syn)
    {
        if (syn == null)
            return;

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Plant)
        {
            PlantManager.Instance?
                .OnlineGetPlant(syn.OnlineId)?
                .OnlineSynPlant(syn);
        }

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Zombie)
        {
            ZombieManager.Instance?
                .OnlineGetZombie(syn.OnlineId)?
                .OnlineSynZombie(syn);
        }

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Puddle)
        {
            var puddles =
                MapManager.Instance?.puddles;

            if (puddles != null)
            {
                foreach (var item in puddles)
                {
                    if (item?.OnlineId != syn.OnlineId)
                        continue;

                    item.StartDisappear();
                    break;
                }
            }
        }

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Portal)
        {
            var portals =
                MapManager.Instance?.portalCs;

            if (portals != null)
            {
                foreach (var item in portals)
                {
                    if (item?.OnlineId != syn.OnlineId)
                        continue;

                    item.ClientReset(syn);
                    break;
                }
            }
        }

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Vase)
        {
            LvItemManager.Instance?.SynVase(syn);
        }

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Card)
        {
            SeedBank.Instance?.SynDropCard(syn);
        }
    }

    public List<string> GetAllPlayerNameList()
    {
        var names =
            new List<string>();

        if (hostPlayer != null)
            names.Add(hostPlayer.Name);

        names.AddRange(
            players
                .Where(p => p != null)
                .Select(p => p.Name));

        return names;
    }

    public List<PlayerInfo> GetPlayers()
    {
        return new List<PlayerInfo>(players);
    }

    public PlayerInfo GetHostPlayer()
    {
        return hostPlayer;
    }

    private void UpdatePlayerLists()
    {
        battlePlayerList ??= BattlePlayerList.Instance;
        playerList ??= PlayerList.Instance;

        var currentPlayers =
            players
                .Where(p => p != null)
                .ToList();

        battlePlayerList?.UpdatePlayerList(
            hostPlayer,
            currentPlayers);

        playerList?.UpdatePlayerList(
            hostPlayer,
            currentPlayers);
    }

    private void AddNewPlayer(
        PlayerInfo player,
        ulong clientId)
    {
        if (player == null ||
            players.Any(
                p => p?.Name == player.Name))
            return;

        players.Add(player);

        UpdatePlayerLists();

        string content =
            player.Name +
            "Te has unido a la partida";

        ChatInput.Instance?.AddMessage(
            content,
            new Color32(
                255,
                255,
                0,
                255));

        SendMessage(
            4,
            1,
            content);

        SendJsonToClient(
            clientId,
            0,
            2,
            new OnlinePlayerInfo
            {
                HostPlayer = hostPlayer,
                players = players
            });
    }

    private void RemoveDone(
        PlayerInfo player)
    {
        if (player == null)
            return;

        string content =
            player.Name +
            "Saliste del juego";

        ChatInput.Instance?.AddMessage(
            content,
            new Color32(
                255,
                255,
                0,
                255));

        SendMessage(
            4,
            1,
            content);

        UpdatePlayerLists();

        PvPSelector.Instance?
            .ClearQuitPlayer(player.Name);

        SpectatorList.Instance?
            .ClearPlayer(player.Name);

        SendJson(
            0,
            2,
            new OnlinePlayerInfo
            {
                HostPlayer = hostPlayer,
                players =
                    players
                        .Where(p => p != null)
                        .ToList()
            });
    }

    public void SendFailConnectMsg(
        string msg,
        ulong clientId)
    {
        SendJsonToClient(
            clientId,
            0,
            1,
            new ConnectInfo
            {
                msg = msg
            });
    }

    public void SendHostCD(
        int cardID,
        bool isOK)
    {
        var save =
            GameManager.Instance?.LocalPlayerSave;

        if (save == null)
            return;

        SendJson(
            2,
            4,
            new UpdateCardCD
            {
                name = save.playerName,
                CardId = cardID,
                OK = isOK
            });
    }

    public void SendChatMsg(
        string content)
    {
        SendMessage(
            4,
            0,
            content);
    }

    public void SendPrivateChatMsg(
        string name,
        string content,
        string sender)
    {
        var clientId =
            GetClientIdByPlayer(name);

        if (clientId == ulong.MaxValue)
            return;

        SendJsonToClient(
            clientId,
            4,
            2,
            new PrivateChatMsg
            {
                PlayerName = sender,
                content = content
            });
    }

    public void KickPlayer(
        string name)
    {
        var player =
            players.FirstOrDefault(
                p => p?.Name == name);

        if (player == null)
            return;

        if (!handQuitPlayer.Any(
                p => p?.Name == player.Name))
        {
            handQuitPlayer.Add(player);
        }

        var clientId =
            GetClientIdByPlayer(name);

        if (clientId == ulong.MaxValue)
            return;

        SendFailConnectMsg(
            "Has sido expulsado de la partida",
            clientId);

        DisconnectClient(clientId);
    }

    public void LoadLv(
        LoadLVBag loadLV)
    {
        reConnectCode =
            UnityEngine.Random.Range(
                100000,
                999999);

        loadLV.ReCntCode =
            reConnectCode;

        SendJson(
            1,
            0,
            loadLV);
    }

    public void SendAddCard(
        AddCardBag bag,
        string playerName)
    {
        var clientId =
            GetClientIdByPlayer(playerName);

        if (clientId == ulong.MaxValue)
            return;

        SendJsonToClient(
            clientId,
            1,
            12,
            bag);
    }

    public void StartRunLv()
    {
        SendMessage(1, 1);
    }

    public void BigWaveComing(
        WaveComing wave)
    {
        SendJson(1, 2, wave);
    }

    public void UpdateSunNum(
        SunNumBag sun)
    {
        SendJson(1, 3, sun);
    }

    public void SendSynBag(
        SynItem syn)
    {
        SendJson(1, 6, syn);
    }

    public void ChangeMap(
        PlayerMap map)
    {
        SendJson(1, 7, map);
    }

    public void SelectCard(
        SelectCard card)
    {
        SendJson(1, 4, card);
    }

    public void SelectPrepare(
        SelectPrepare prepare)
    {
        SendJson(1, 5, prepare);
    }

    public void GameOver(
        GameOver over)
    {
        SendJson(1, 8, over);
    }

    public void SynTeamList(
        PvPTeamList list)
    {
        SendJson(1, 9, list);
    }

    public void SynPvPMode(
        PvPModeSyn syn,
        ulong clientId = ulong.MaxValue)
    {
        if (clientId == ulong.MaxValue)
            SendJson(1, 10, syn);
        else
            SendJsonToClient(
                clientId,
                1,
                10,
                syn);
    }

    public void SynSpectList(
        SpectList list)
    {
        SendJson(1, 11, list);
    }

    public void SynFlagMeter(
        FlagMeterSyn syn)
    {
        SendJson(1, 13, syn);
    }

    public void SynTimeTable(
        TimetableSyn syn)
    {
        SendJson(1, 14, syn);
    }

    public void SpawnSun(
        SunSpawn spawn)
    {
        SendJson(2, 1, spawn);
    }

    public void ClickedSun(
        ClickedSun sun)
    {
        SendJson(2, 2, sun);
    }

    public void SpawnPlant(
        PlantSpawn spawn)
    {
        SendJson(2, 0, spawn);
    }

    public void PlacePreview(
        PlantPreview preview,
        ulong? clientId)
    {
        ulong excludedClientId =
            clientId ??
            NetworkManager.Singleton?.LocalClientId ??
            ulong.MaxValue;

        SendJsonExcept(
            excludedClientId,
            2,
            3,
            preview);
    }

    public void ZombiePreview(
        ZombiePreview preview,
        ulong? clientId)
    {
        ulong excludedClientId =
            clientId ??
            NetworkManager.Singleton?.LocalClientId ??
            ulong.MaxValue;

        SendJsonExcept(
            excludedClientId,
            3,
            5,
            preview);
    }

    public void ShovelPreview(
        ShovelPreview preview,
        ulong? clientId)
    {
        ulong excludedClientId =
            clientId ??
            NetworkManager.Singleton?.LocalClientId ??
            ulong.MaxValue;

        SendJsonExcept(
            excludedClientId,
            2,
            5,
            preview);
    }

    public void SpawnZombie(
        ZombieSpawn spawn)
    {
        SendJson(3, 0, spawn);
    }

    public void SpawnGraveStone(
        GraveStoneSpawn spawn)
    {
        SendJson(3, 1, spawn);
    }

    public void SpawnPuddle(
        PuddleSpawn spawn)
    {
        SendJson(3, 2, spawn);
    }

    public void SpawnPortal(
        PortalSpawn spawn)
    {
        SendJson(3, 6, spawn);
    }

    public void SpawnVase(
        VaseSpawn spawn)
    {
        SendJson(3, 9, spawn);
    }

    public void SpawnDropCard(
        CardSpawn spawn)
    {
        SendJson(3, 10, spawn);
    }

    public void SpawnMelt(
        MeltSpawn spawn)
    {
        SendJson(3, 11, spawn);
    }

    public void SpawnFallHail(
        FallHailSpawn spawn)
    {
        SendJson(3, 12, spawn);
    }

    public void SpawnLightning(
        LightingSpawn spawn)
    {
        SendJson(3, 3, spawn);
    }

    public void SendShovelAnim(
        ToolApply apply)
    {
        SendJson(2, 6, apply);
    }

    public void SendGridState(
        SynGrid grid)
    {
        SendJson(3, 7, grid);
    }

    public void SendWeatherCmd(
        WeatherChange cmd)
    {
        SendJson(4, 4, cmd);
    }

    public void SendTimeCmd(
        TimeCmd cmd)
    {
        SendJson(4, 5, cmd);
    }

    public void SendMapSyn(
        SynMap map)
    {
        SendJson(3, 4, map);
    }

    public void SendSynBooty(
        SynBooty booty)
    {
        SendJson(3, 8, booty);
    }

    public void SendAcvmentGet(
        Acvname acv,
        string playerName)
    {
        var clientId =
            GetClientIdByPlayer(playerName);

        if (clientId == ulong.MaxValue)
            return;

        SendJsonToClient(
            clientId,
            4,
            6,
            new GetAcvment
            {
                acv = acv
            });
    }

    public void SendCommandBag(
        ulong clientId = ulong.MaxValue)
    {
        if (PlantManager.Instance == null ||
            ZombieManager.Instance == null ||
            SkyManager.Instance == null ||
            PlayerManager.Instance == null ||
            SeedBank.Instance == null ||
            LvItemManager.Instance == null)
            return;

        var bag =
            new CommandBag
            {
                Pinv = PlantManager.Instance.PlantInvincible,
                Zinv = ZombieManager.Instance.ZombieInvincible,
                DLiCy = SkyManager.Instance.DayLightCycle,
                SnInf = PlayerManager.Instance.SunInfinite,
                CdCle = SeedBank.Instance.isNoCD,
                ZomStop = ZombieManager.Instance.ZombieDontMove,
                VaseXray = LvItemManager.Instance.VaseAlwaysLight
            };

        if (clientId == ulong.MaxValue)
            SendJson(4, 3, bag);
        else
            SendJsonToClient(
                clientId,
                4,
                3,
                bag);
    }

    private void SendWaitReConnect(
        bool wait)
    {
        var names =
            reConnectPlayer
                .Where(p => p != null)
                .Select(p => p.Name)
                .ToList();

        SendJson(
            0,
            3,
            new ReConnectInfo
            {
                isWait = wait,
                names = names
            });
    }

    private void ReConnectListChange()
    {
        if (reConnectPlayer.Count > 0)
        {
            var names =
                reConnectPlayer
                    .Where(p => p != null)
                    .Select(p => p.Name)
                    .ToList();

            ReConnect.Instance?
                .LoadPlayerList(names);

            SendWaitReConnect(true);
        }
        else
        {
            SendWaitReConnect(false);

            ReConnect.Instance?
                .OverClose();
        }
    }

    private bool CanReConnect(
        PlayerInfo info)
    {
        return info != null &&
               reConnectPlayer.Any(
                   p => p?.Name == info.Name);
    }

    public void GiveUpReConnect()
    {
        foreach (var player in reConnectPlayer)
            RemoveDone(player);

        reConnectPlayer.Clear();

        ReConnectListChange();
    }

    private ulong GetClientIdByPlayer(
        string name)
    {
        if (string.IsNullOrEmpty(name))
            return ulong.MaxValue;

        foreach (var pair in clientPlayers)
        {
            if (pair.Value?.Name == name)
                return pair.Key;
        }

        return ulong.MaxValue;
    }

    private IEnumerator SendHeartbeat()
    {
        float timer =
            Time.realtimeSinceStartup;

        while (
            isServerOpen &&
            GameManager.Instance?.isOnline == true)
        {
            yield return null;

            if (Time.realtimeSinceStartup - timer <= .5f)
                continue;

            SendMessage(
                0,
                byte.MaxValue);

            timer =
                Time.realtimeSinceStartup;
        }
    }

    private IEnumerator CheckConnect()
    {
        float timer =
            Time.realtimeSinceStartup;

        while (
            isServerOpen &&
            GameManager.Instance?.isOnline == true)
        {
            yield return null;

            if (Time.realtimeSinceStartup - timer <= 3f)
                continue;

            var disconnected =
                new List<ulong>();

            foreach (var pair in clientPlayers)
            {
                if (pair.Value == null)
                {
                    disconnected.Add(pair.Key);
                    continue;
                }

                if (!pair.Value.Heartbeat)
                {
                    disconnected.Add(pair.Key);

                    Debug.LogError(
                        pair.Value.Name +
                        "Desconexión por tiempo de espera");
                }
                else
                {
                    pair.Value.Heartbeat = false;
                }
            }

            foreach (var clientId in disconnected)
            {
                RemovePlayerOnDisconnect(clientId);
                DisconnectClient(clientId);
            }

            timer =
                Time.realtimeSinceStartup;
        }
    }
}