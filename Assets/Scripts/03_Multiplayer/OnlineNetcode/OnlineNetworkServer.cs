using SocketSave;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class OnlineNetworkServer : NetworkBehaviour
{
    public static OnlineNetworkServer Instance;

    [Header("UI")]
    [SerializeField] BattlePlayerList battlePlayerList;
    [SerializeField] PlayerList playerList;

    public bool isServerOpen;

    readonly List<PlayerInfo> players = new();
    readonly Dictionary<ulong, PlayerInfo> clientPlayers = new();

    PlayerInfo HostPlayer;

    int ReConnectCode;
    int itemId;

    readonly List<PlayerInfo> ReConnectPlayer = new();
    readonly List<PlayerInfo> HandQuitPlayer = new();

    public int noHostPlayerNum => players.Count;
    public int ItemId => ++itemId;

    void Awake()
    {
        Instance = this;

        battlePlayerList ??= BattlePlayerList.Instance;
        playerList ??= PlayerList.Instance;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            isServerOpen = true;

            InitializeHostPlayer();

            StartCoroutine(SendHeartbeat());
            StartCoroutine(CheckConnect());

            UpdatePlayerLists();

            Debug.Log(
                $"Online Server iniciado. Host: {HostPlayer?.Name} | " +
                $"Versión: v{GameManager.Instance?.VersionCode}");
        }
    }

    public override void OnNetworkDespawn()
    {
        StopAllCoroutines();

        if (IsServer)
            ClearServerState(true);

        base.OnNetworkDespawn();
    }

    void InitializeHostPlayer()
    {
        var gm = GameManager.Instance;

        if (gm == null)
            return;

        string name =
            gm.LocalPlayerSave?.playerName ??
            "Host";

        HostPlayer = new PlayerInfo
        {
            Name = name,
            VersionCode = gm.VersionCode
        };

        gm.HostName = name;
        gm.isOnline = true;
    }

    void ClearPlayerUI()
    {
        battlePlayerList?.UpdatePlayerList(
            null,
            new List<PlayerInfo>());

        playerList?.UpdatePlayerList(
            null,
            new List<PlayerInfo>());
    }

    void ClearServerState(bool clearUI)
    {
        isServerOpen = false;

        if (GameManager.Instance != null)
            GameManager.Instance.isOnline = false;

        players.Clear();
        clientPlayers.Clear();
        ReConnectPlayer.Clear();
        HandQuitPlayer.Clear();

        HostPlayer = null;

        ReConnectCode = 0;
        itemId = 0;

        if (clearUI)
            ClearPlayerUI();
    }

    public void StartServer(IPAddress ip, int port)
    {
        var gm = GameManager.Instance;

        if (gm == null)
        {
            Debug.LogError(
                "No se pudo iniciar el servidor: GameManager.Instance es null.");
            return;
        }

        if (gm.isOnline)
            return;

        var networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError(
                "No existe NetworkManager.");
            return;
        }

        var transport =
            networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError(
                "No existe UnityTransport en NetworkManager.");
            return;
        }

        StopAllCoroutines();
        ClearServerState(true);

        string address =
            ip != null
                ? ip.ToString()
                : "0.0.0.0";

        transport.SetConnectionData(
            address,
            (ushort)port,
            "0.0.0.0");

        if (!networkManager.StartHost())
        {
            Debug.LogError(
                "No se pudo iniciar NetworkManager como Host.");

            ClearServerState(true);
            return;
        }

        isServerOpen = true;

        InitializeHostPlayer();

        UpdatePlayerLists();

        StartCoroutine(SendHeartbeat());
        StartCoroutine(CheckConnect());

        Debug.Log(
            $"Servidor online iniciado. " +
            $"Host: {HostPlayer?.Name} | " +
            $"Address: {address} | " +
            $"Port: {port}");
    }

    public void CloseServer()
    {
        StopAllCoroutines();

        var networkManager = NetworkManager.Singleton;

        if (networkManager != null &&
            networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        ClearServerState(true);

        Debug.Log(
            "Servidor online cerrado y estado limpiado.");
    }

    void OnApplicationQuit()
    {
        try
        {
            StopAllCoroutines();
            ClearServerState(false);
        }
        catch
        {
        }
    }

    void OnDestroy()
    {
        if (Instance != this)
            return;

        try
        {
            StopAllCoroutines();
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
        if (!IsServer || !isServerOpen)
            return;

        ulong clientId =
            rpcParams.Receive.SenderClientId;

        HandleClientMessage(
            clientId,
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
        if (!IsServer || !isServerOpen)
            return;

        HandleClientMessage(
            clientId,
            type1,
            type2,
            content ?? "");
    }

    void HandleClientMessage(
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

        if (type1 == 1)
        {
            if (!clientPlayers.TryGetValue(
                    clientId,
                    out var player))
                return;

            ProcessGameMessage(
                type2,
                content,
                player,
                clientId);

            return;
        }

        if (type1 == 2)
        {
            if (!clientPlayers.TryGetValue(
                    clientId,
                    out var player))
                return;

            HandleChatMessage(
                type2,
                content,
                player,
                clientId);
        }
    }

    void HandleConnectionMessage(
        ulong clientId,
        byte type,
        string msg)
    {
        if (type == 1)
        {
            PlayerInfo info;

            try
            {
                info = JsonUtility.FromJson<PlayerInfo>(msg);
            }
            catch
            {
                info = null;
            }

            if (!ValidatePlayer(info))
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
            if (clientPlayers.TryGetValue(
                    clientId,
                    out var player))
            {
                HandQuitPlayer.Add(player);
            }

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
                byte.MaxValue,
                "");

            return;
        }
    }

    bool ValidatePlayer(PlayerInfo info)
    {
        if (info == null)
        {
            SendFailConnectMsg(
                "Información del jugador no válida.",
                0);

            return false;
        }

        var gm = GameManager.Instance;

        if (gm == null)
        {
            SendFailConnectMsg(
                "GameManager no está disponible.",
                FindClientId(info.Name));

            return false;
        }

        if (info.VersionCode != gm.VersionCode)
        {
            SendFailConnectMsg(
                "Diferente a la versión del juego del servidor. Versión del juego del servidor :v" +
                gm.VersionCode,
                FindClientId(info.Name));

            return false;
        }

        if (CanReConnect(info))
        {
            if (info.ReCntCode != ReConnectCode)
            {
                return false;
            }

            return true;
        }

        var ui = UIManager.Instance;

        if (ui?.HostPasswordInput == null)
        {
            SendFailConnectMsg(
                "La interfaz del servidor no está disponible.",
                FindClientId(info.Name));

            return false;
        }

        if (!string.IsNullOrEmpty(
                ui.HostPasswordInput.text) &&
            info.Password !=
            ui.HostPasswordInput.text)
        {
            SendFailConnectMsg(
                "Contraseña incorrecta; no se pudo unir.",
                FindClientId(info.Name));

            return false;
        }

        if (players.Count >= 3)
        {
            SendFailConnectMsg(
                "Server lleno; error al unirse.",
                FindClientId(info.Name));

            return false;
        }

        if (HostPlayer?.Name == info.Name)
        {
            SendFailConnectMsg(
                "El nombre entra en conflicto con el de un jugador en línea; no se pudo unir a la partida.",
                FindClientId(info.Name));

            return false;
        }

        if (LVManager.Instance?.InGame == true)
        {
            SendFailConnectMsg(
                "La partida ya ha comenzado; no se pudo unir.",
                FindClientId(info.Name));

            return false;
        }

        if (gm.LocalPlayerSave == null)
        {
            SendFailConnectMsg(
                "No se pudo cargar la información del jugador.",
                FindClientId(info.Name));

            return false;
        }

        if (info.CmdEnable !=
            gm.LocalPlayerSave.CmdEnable)
        {
            SendFailConnectMsg(
                info.CmdEnable
                    ? "Has habilitado los comandos, pero el servidor no; no puedes unirte"
                    : "No has habilitado los comandos, pero el servidor sí; no puedes unirte",
                FindClientId(info.Name));

            return false;
        }

        if (players.Any(
                p => p?.Name == info.Name))
        {
            SendFailConnectMsg(
                "El nombre entra en conflicto con el de un jugador en línea; no se pudo unir a la partida.",
                FindClientId(info.Name));

            return false;
        }

        return true;
    }

    void AddPlayerFromClient(
        ulong clientId,
        PlayerInfo player)
    {
        if (player == null)
            return;

        if (CanReConnect(player))
        {
            players.Add(player);

            ReConnectPlayer.RemoveAll(
                p => p?.Name == player.Name);

            ReConnectListChange();

            return;
        }

        AddNewPlayer(player);

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

        SpectatorList.Instance?.ServerSynSpectList();
    }

    void DisconnectClient(ulong clientId)
    {
        var networkManager =
            NetworkManager.Singleton;

        if (networkManager == null)
            return;

        if (networkManager.ConnectedClientsIds.Contains(
                clientId))
        {
            networkManager.DisconnectClient(
                clientId);
        }
    }

    ulong FindClientId(string playerName)
    {
        foreach (var pair in clientPlayers)
        {
            if (pair.Value?.Name == playerName)
                return pair.Key;
        }

        return ulong.MaxValue;
    }

    void ProcessGameMessage(
        byte type,
        string msg,
        PlayerInfo player,
        ulong clientId)
    {
        switch (type)
        {
            case 0:
                PlacePlant(
                    JsonUtility.FromJson<PlantSpawn>(msg),
                    player,
                    clientId);
                break;

            case 1:
                ApplyTool(
                    JsonUtility.FromJson<ToolApply>(msg),
                    player,
                    clientId);
                break;

            case 2:
                if (SkyManager.Instance != null)
                {
                    SkyManager.Instance.OnlineCollectSun(
                        JsonUtility.FromJson<ClickedSun>(msg));
                }
                break;

            case 3:
                ChangeMap(
                    JsonUtility.FromJson<PlayerMap>(msg),
                    player);
                break;

            case 4:
                SelectCard(
                    JsonUtility.FromJson<SelectCard>(msg),
                    player);
                break;

            case 5:
                SelectPrepare(
                    JsonUtility.FromJson<SelectPrepare>(msg),
                    player);
                break;

            case 6:
                {
                    var item =
                        JsonUtility.FromJson<SynItem>(msg);

                    if (item != null)
                        SynItem(item);

                    break;
                }

            case 7:
                {
                    var pp =
                        JsonUtility.FromJson<PlantPreview>(msg);

                    if (pp != null)
                    {
                        BattlePlayerList.Instance?.PreviewPlant(pp);
                        PlacePreview(pp, clientId);
                    }

                    break;
                }

            case 8:
                UpdateCD(
                    JsonUtility.FromJson<UpdateCardCD>(msg),
                    player,
                    clientId);
                break;

            case 9:
                {
                    var sp =
                        JsonUtility.FromJson<ShovelPreview>(msg);

                    if (sp != null)
                    {
                        BattlePlayerList.Instance?.PreviewShovel(
                            sp.PlayerName,
                            sp.GridPos,
                            sp.isShow);

                        ShovelPreview(
                            sp,
                            clientId);
                    }

                    break;
                }

            case 10:
                PlaceZombie(
                    JsonUtility.FromJson<ZombieSpawnApply>(msg),
                    player,
                    clientId);
                break;

            case 11:
                {
                    var zp =
                        JsonUtility.FromJson<ZombiePreview>(msg);

                    if (zp != null)
                    {
                        BattlePlayerList.Instance?.PreviewZombie(zp);
                        ZombiePreview(zp, clientId);
                    }

                    break;
                }

            case 12:
                {
                    var team =
                        JsonUtility.FromJson<JoinTeamApply>(msg);

                    if (team != null &&
                        player != null &&
                        PvPSelector.Instance != null)
                    {
                        if (team.isRed)
                            PvPSelector.Instance.JoinRed(
                                player.Name);
                        else
                            PvPSelector.Instance.JoinBlue(
                                player.Name);
                    }

                    break;
                }

            case 13:
                {
                    var spect =
                        JsonUtility.FromJson<JoinSpecApply>(msg);

                    if (spect != null &&
                        player != null &&
                        SpectatorList.Instance != null)
                    {
                        SpectatorList.Instance.ClientJoinSpect(
                            player.Name,
                            spect.isJoin);
                    }

                    break;
                }

            case 14:
                {
                    var bag =
                        JsonUtility.FromJson<SlotMchBag>(msg);

                    if (bag != null)
                        SlotMachine.Instance?.ClientSyn(bag);

                    break;
                }
        }
    }

    void PlacePlant(
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

        var cd = new UpdateCardCD
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

            SendJson(
                2,
                4,
                cd);

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

    void PlaceZombie(
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

        var cd = new UpdateCardCD
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

            SendJson(
                2,
                4,
                cd);

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

    void ReversePvP(
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

    void ApplyTool(
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

            SendJson(
                2,
                6,
                apply);
        }
        else if (apply.type == ToolType.Glove)
        {
            Glove.Instance?.SynClient(
                apply.OnlineId,
                apply.GridPos);
        }
    }

    void ChangeMap(
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

    void SelectCard(
        SelectCard card,
        PlayerInfo player)
    {
        if (card == null ||
            player == null)
            return;

        card.PlayerName = player.Name;

        SelectCard(card);

        if (BattlePlayerList.Instance != null)
        {
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
    }

    void SelectPrepare(
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

    void UpdateCD(
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

    void HandleChatMessage(
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

        if (type == 1)
        {
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
    }

    void SendMessage(
        byte type1,
        byte type2,
        string content = "")
    {
        if (!IsServer)
            return;

        SendMessageClientRpc(
            type1,
            type2,
            content ?? "");
    }

    void SendMessageToClient(
        ulong clientId,
        byte type1,
        byte type2,
        string content = "")
    {
        if (!IsServer)
            return;

        var rpcParams =
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds =
                        new[]
                        {
                            clientId
                        }
                }
            };

        SendMessageClientRpc(
            type1,
            type2,
            content ?? "",
            rpcParams);
    }

    void SendMessageExcept(
        ulong excludedClientId,
        byte type1,
        byte type2,
        string content = "")
    {
        if (!IsServer)
            return;

        var targets =
            NetworkManager.Singleton
                .ConnectedClientsIds
                .Where(id => id != excludedClientId)
                .ToArray();

        if (targets.Length == 0)
            return;

        var rpcParams =
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = targets
                }
            };

        SendMessageClientRpc(
            type1,
            type2,
            content ?? "",
            rpcParams);
    }

    void SendJson(
        byte type1,
        byte type2,
        object value)
    {
        SendMessage(
            type1,
            type2,
            JsonUtility.ToJson(value));
    }

    void SendJsonToClient(
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

    void SendJsonExcept(
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

    [ClientRpc]
    void SendMessageClientRpc(
        byte type1,
        byte type2,
        string content,
        ClientRpcParams rpcParams = default)
    {
        if (IsServer)
            return;

        OnlineNetworkClient.Instance?.ReceiveMessage(
            type1,
            type2,
            content);
    }

    void DisconnectPlayer(
        ulong clientId)
    {
        if (!clientPlayers.TryGetValue(
                clientId,
                out var player))
            return;

        RemovePlayer(
            player,
            clientId);
    }

    void RemovePlayerByClient(
        ulong clientId,
        bool intentional)
    {
        if (!clientPlayers.TryGetValue(
                clientId,
                out var player))
            return;

        if (intentional)
            HandQuitPlayer.Add(player);

        RemovePlayer(
            player,
            clientId);
    }

    void RemovePlayer(
        PlayerInfo player,
        ulong clientId)
    {
        if (player == null)
            return;

        clientPlayers.Remove(clientId);

        int index =
            players.IndexOf(player);

        if (index < 0)
        {
            index =
                players.FindIndex(
                    p => p?.Name == player.Name);
        }

        if (index < 0)
            return;

        player = players[index];

        players.RemoveAt(index);

        if (LVManager.Instance?.InGame == true)
        {
            if (HandQuitPlayer.Remove(player))
            {
                RemoveDone(player);
                return;
            }

            if (!ReConnectPlayer.Contains(player))
                ReConnectPlayer.Add(player);

            ReConnect.Instance?.OpenInit(false);

            ReConnectListChange();
        }
        else
        {
            RemoveDone(player);
        }
    }

    void RemovePlayerOnDisconnect(
        ulong clientId)
    {
        if (!clientPlayers.TryGetValue(
                clientId,
                out var player))
            return;

        RemovePlayer(
            player,
            clientId);
    }

    public void HandleClientDisconnected(
        ulong clientId)
    {
        if (!IsServer)
            return;

        RemovePlayerOnDisconnect(clientId);
    }

    public void SynItem(SynItem syn)
    {
        if (syn == null)
            return;

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Plant)
        {
            PlantManager.Instance?
                .OnlineGetPlant(
                    syn.OnlineId)?
                .OnlineSynPlant(syn);
        }

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Zombie)
        {
            ZombieManager.Instance?
                .OnlineGetZombie(
                    syn.OnlineId)?
                .OnlineSynZombie(syn);
        }

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Puddle)
        {
            var list =
                MapManager.Instance?.puddles;

            if (list != null)
            {
                foreach (var item in list)
                {
                    if (item?.OnlineId == syn.OnlineId)
                    {
                        item.StartDisappear();
                        break;
                    }
                }
            }
        }

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Portal)
        {
            var list =
                MapManager.Instance?.portalCs;

            if (list != null)
            {
                foreach (var item in list)
                {
                    if (item?.OnlineId == syn.OnlineId)
                    {
                        item.ClientReset(syn);
                        break;
                    }
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
        var names = new List<string>();

        if (HostPlayer != null)
            names.Add(HostPlayer.Name);

        foreach (var player in players)
        {
            if (player != null)
                names.Add(player.Name);
        }

        return names;
    }

    public List<PlayerInfo> GetPlayers()
    {
        return new List<PlayerInfo>(players);
    }

    public PlayerInfo GetHostPlayer()
    {
        return HostPlayer;
    }

    void UpdatePlayerLists()
    {
        battlePlayerList ??=
            BattlePlayerList.Instance;

        playerList ??=
            PlayerList.Instance;

        battlePlayerList?.UpdatePlayerList(
            HostPlayer,
            players);

        playerList?.UpdatePlayerList(
            HostPlayer,
            players);
    }

    void AddNewPlayer(
        PlayerInfo player)
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

        SendJson(
            0,
            2,
            new OnlinePlayerInfo
            {
                HostPlayer = HostPlayer,
                players = players
            });
    }

    void RemoveDone(
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
            .ClearQuitPlayer(
                player.Name);

        SpectatorList.Instance?
            .ClearPlayer(
                player.Name);

        SendJson(
            0,
            2,
            new OnlinePlayerInfo
            {
                HostPlayer = HostPlayer,
                players = players
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

        HandQuitPlayer.Add(player);

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
        ReConnectCode =
            UnityEngine.Random.Range(
                100000,
                999999);

        loadLV.ReCntCode =
            ReConnectCode;

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
        SendMessage(
            1,
            1);
    }

    public void BigWaveComing(
        WaveComing wave)
    {
        SendJson(
            1,
            2,
            wave);
    }

    public void UpdateSunNum(
        SunNumBag sun)
    {
        SendJson(
            1,
            3,
            sun);
    }

    public void SendSynBag(
        SynItem syn)
    {
        SendJson(
            1,
            4,
            syn);
    }

    public void ChangeMap(
        PlayerMap map)
    {
        SendJson(
            1,
            5,
            map);
    }

    public void SelectCard(
        SelectCard card)
    {
        SendJson(
            1,
            6,
            card);
    }

    public void SelectPrepare(
        SelectPrepare prepare)
    {
        SendJson(
            1,
            7,
            prepare);
    }

    public void GameOver(
        GameOver over)
    {
        SendJson(
            1,
            8,
            over);
    }

    public void SynTeamList(
        PvPTeamList list)
    {
        SendJson(
            1,
            9,
            list);
    }

    public void SynPvPMode(
        PvPModeSyn syn,
        ulong clientId = ulong.MaxValue)
    {
        if (clientId == ulong.MaxValue)
        {
            SendJson(
                1,
                10,
                syn);
        }
        else
        {
            SendJsonToClient(
                clientId,
                1,
                10,
                syn);
        }
    }

    public void SynSpectList(
        SpectList list)
    {
        SendJson(
            1,
            11,
            list);
    }

    public void SynFlagMeter(
        FlagMeterSyn syn)
    {
        SendJson(
            1,
            13,
            syn);
    }

    public void SynTimeTable(
        TimetableSyn syn)
    {
        SendJson(
            1,
            14,
            syn);
    }

    public void SpawnSun(
        SunSpawn spawn)
    {
        SendJson(
            2,
            1,
            spawn);
    }

    public void ClickedSun(
        ClickedSun sun)
    {
        SendJson(
            2,
            2,
            sun);
    }

    public void SpawnPlant(
        PlantSpawn spawn)
    {
        SendJson(
            2,
            0,
            spawn);
    }

    public void PlacePreview(
        PlantPreview preview,
        ulong clientId)
    {
        SendJsonExcept(
            clientId,
            2,
            3,
            preview);
    }

    public void ZombiePreview(
        ZombiePreview preview,
        ulong clientId)
    {
        SendJsonExcept(
            clientId,
            3,
            5,
            preview);
    }

    public void ShovelPreview(
        ShovelPreview preview,
        ulong clientId)
    {
        SendJsonExcept(
            clientId,
            2,
            5,
            preview);
    }

    public void SpawnZombie(
        ZombieSpawn spawn)
    {
        SendJson(
            3,
            0,
            spawn);
    }

    public void SpawnGraveStone(
        GraveStoneSpawn spawn)
    {
        SendJson(
            3,
            1,
            spawn);
    }

    public void SpawnPuddle(
        PuddleSpawn spawn)
    {
        SendJson(
            3,
            2,
            spawn);
    }

    public void SpawnPortal(
        PortalSpawn spawn)
    {
        SendJson(
            3,
            6,
            spawn);
    }

    public void SpawnVase(
        VaseSpawn spawn)
    {
        SendJson(
            3,
            9,
            spawn);
    }

    public void SpawnDropCard(
        CardSpawn spawn)
    {
        SendJson(
            3,
            10,
            spawn);
    }

    public void SpawnMelt(
        MeltSpawn spawn)
    {
        SendJson(
            3,
            11,
            spawn);
    }

    public void SpawnFallHail(
        FallHailSpawn spawn)
    {
        SendJson(
            3,
            12,
            spawn);
    }

    public void SpawnLightning(
        LightingSpawn spawn)
    {
        SendJson(
            3,
            3,
            spawn);
    }

    public void SendShovelAnim(
        ToolApply apply)
    {
        SendJson(
            2,
            6,
            apply);
    }

    public void SendGridState(
        SynGrid grid)
    {
        SendJson(
            3,
            7,
            grid);
    }

    public void SendWeatherCmd(
        WeatherChange cmd)
    {
        SendJson(
            4,
            4,
            cmd);
    }

    public void SendTimeCmd(
        TimeCmd cmd)
    {
        SendJson(
            4,
            5,
            cmd);
    }

    public void SendMapSyn(
        SynMap map)
    {
        SendJson(
            3,
            4,
            map);
    }

    public void SendSynBooty(
        SynBooty booty)
    {
        SendJson(
            3,
            8,
            booty);
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

        var bag = new CommandBag
        {
            Pinv =
                PlantManager.Instance.PlantInvincible,

            Zinv =
                ZombieManager.Instance.ZombieInvincible,

            DLiCy =
                SkyManager.Instance.DayLightCycle,

            SnInf =
                PlayerManager.Instance.SunInfinite,

            CdCle =
                SeedBank.Instance.isNoCD,

            ZomStop =
                ZombieManager.Instance.ZombieDontMove,

            VaseXray =
                LvItemManager.Instance.VaseAlwaysLight
        };

        if (clientId == ulong.MaxValue)
        {
            SendJson(
                4,
                3,
                bag);
        }
        else
        {
            SendJsonToClient(
                clientId,
                4,
                3,
                bag);
        }
    }

    void SendWaitReConnect(
        bool wait)
    {
        var names =
            ReConnectPlayer
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

    void ReConnectListChange()
    {
        if (ReConnectPlayer.Count > 0)
        {
            var names =
                ReConnectPlayer
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

    bool CanReConnect(
        PlayerInfo info)
    {
        return info != null &&
               ReConnectPlayer.Any(
                   p => p?.Name == info.Name);
    }

    public void GiveUpReConnect()
    {
        foreach (var player in ReConnectPlayer)
            RemoveDone(player);

        ReConnectPlayer.Clear();

        ReConnectListChange();
    }

    ulong GetClientIdByPlayer(
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

    IEnumerator SendHeartbeat()
    {
        float timer =
            Time.realtimeSinceStartup;

        while (
            isServerOpen &&
            GameManager.Instance?.isOnline == true)
        {
            yield return null;

            if (Time.realtimeSinceStartup - timer > .5f)
            {
                SendMessage(
                    0,
                    byte.MaxValue);

                timer =
                    Time.realtimeSinceStartup;
            }
        }
    }

    IEnumerator CheckConnect()
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
                var player = pair.Value;

                if (player == null)
                {
                    disconnected.Add(pair.Key);
                    continue;
                }

                if (!player.Heartbeat)
                {
                    disconnected.Add(pair.Key);

                    Debug.LogError(
                        player.Name +
                        "Desconexión por tiempo de espera");
                }
                else
                {
                    player.Heartbeat = false;
                }
            }

            foreach (var clientId in disconnected)
            {
                RemovePlayerOnDisconnect(
                    clientId);

                DisconnectClient(
                    clientId);
            }

            timer =
                Time.realtimeSinceStartup;
        }
    }
}