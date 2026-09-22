using SocketSave;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class OnlineNetworkClient : MonoBehaviour
{
    public static OnlineNetworkClient Instance;

    private const string MessageName = "PVZ_ONLINE_MESSAGE";
    private const int MaxPacket = 1048576;
    private const string RelayConnectionType = "udp";

    private bool needLog;
    private bool needLog2;
    private bool onlineCheck;
    private bool isHandOver;

    private int reconnectCode;

    private bool connectOverCalled;
    private bool connectionResponseReceived;

    private Coroutine waitConnect;

    private string pendingPassword = "";

    private OnlinePlayerInfo pendingOnlinePlayerInfo;
    private bool hasPendingOnlinePlayerInfo;
    private bool pendingPlayerListDone;
    private bool pendingBattlePlayerListDone;

    private bool relayConnecting;

    private void Awake()
    {
        Instance = this;
        Application.runInBackground = true;
    }

    private void Update()
    {
        ProcessPendingOnlinePlayerInfo();
    }

    private void OnDestroy()
    {
        UnregisterNetworkCallbacks();
        UnregisterMessageHandler();

        if (Instance == this)
            Instance = null;
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
            ReceiveMessage);
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

    private void RegisterNetworkCallbacks()
    {
        var manager = NetworkManager.Singleton;

        if (manager == null)
            return;

        manager.OnClientConnectedCallback -= OnClientConnected;
        manager.OnClientDisconnectCallback -= OnClientDisconnect;

        manager.OnClientConnectedCallback += OnClientConnected;
        manager.OnClientDisconnectCallback += OnClientDisconnect;
    }

    private void UnregisterNetworkCallbacks()
    {
        var manager = NetworkManager.Singleton;

        if (manager == null)
            return;

        manager.OnClientConnectedCallback -= OnClientConnected;
        manager.OnClientDisconnectCallback -= OnClientDisconnect;
    }

    private bool ConfigureTransport(
        IPAddress ip,
        int port)
    {
        var manager = NetworkManager.Singleton;

        if (manager == null)
            return false;

        var transport =
            manager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError(
                "[OnlineNetworkClient] No se encontró UnityTransport.");

            return false;
        }

        if (ip == null)
        {
            Debug.LogError(
                "[OnlineNetworkClient] IP del servidor inválida.");

            return false;
        }

        if (port < 1 ||
            port > ushort.MaxValue)
        {
            Debug.LogError(
                $"[OnlineNetworkClient] Puerto inválido: {port}");

            return false;
        }

        transport.SetConnectionData(
            ip.ToString(),
            (ushort)port,
            "0.0.0.0");

        return true;
    }

    public void JoinGame(
        IPAddress ip,
        int port,
        string passWord)
    {
        var gm = GameManager.Instance;

        if (gm == null ||
            gm.isOnline ||
            waitConnect != null ||
            relayConnecting)
        {
            return;
        }

        var manager = NetworkManager.Singleton;

        if (manager == null)
        {
            Debug.LogError(
                "[OnlineNetworkClient] No existe NetworkManager.");

            return;
        }

        if (gm.LocalPlayerSave == null)
            return;

        if (manager.IsListening)
            manager.Shutdown();

        if (!ConfigureTransport(
                ip,
                port))
        {
            return;
        }

        pendingPassword =
            (passWord ?? "").Trim();

        PrepareConnection();

        RegisterNetworkCallbacks();

        waitConnect =
            StartCoroutine(
                WaitLog());

        if (!manager.StartClient())
        {
            waitConnect = null;

            Debug.LogError(
                "[OnlineNetworkClient] No se pudo iniciar el cliente NGO.");

            return;
        }

        RegisterMessageHandler();

        Debug.Log(
            $"[OnlineNetworkClient] Conectando a {ip}:{port}");
    }

    public void JoinRelay(
        string joinCode,
        string passWord)
    {
        _ = JoinRelayGame(
            joinCode,
            passWord);
    }

    public async System.Threading.Tasks.Task<bool> JoinRelayGame(
        string joinCode,
        string passWord)
    {
        var gm = GameManager.Instance;

        if (gm == null ||
            gm.isOnline ||
            waitConnect != null ||
            relayConnecting)
        {
            return false;
        }

        var manager = NetworkManager.Singleton;

        if (manager == null)
        {
            Debug.LogError(
                "[OnlineNetworkClient] No existe NetworkManager.");

            return false;
        }

        if (gm.LocalPlayerSave == null)
            return false;

        joinCode =
            (joinCode ?? "")
                .Trim()
                .ToUpperInvariant();

        if (string.IsNullOrEmpty(joinCode))
        {
            Debug.LogError(
                "[OnlineNetworkClient] Join Code vacío.");

            return false;
        }

        var services =
            UnityServicesInitializer.Instance;

        if (services == null)
        {
            Debug.LogError(
                "[OnlineNetworkClient] No existe UnityServicesInitializer.");

            return false;
        }

        relayConnecting = true;

        try
        {
            if (manager.IsListening)
                manager.Shutdown();

            if (!await services.InitializeServices())
            {
                Debug.LogError(
                    "[OnlineNetworkClient] No se pudieron inicializar Unity Services.");

                return false;
            }

            JoinAllocation allocation =
                await RelayService.Instance
                    .JoinAllocationAsync(
                        joinCode);

            var transport =
                manager.GetComponent<UnityTransport>();

            if (transport == null)
            {
                Debug.LogError(
                    "[OnlineNetworkClient] No se encontró UnityTransport.");

                return false;
            }

            transport.SetRelayServerData(
                new RelayServerData(
                    allocation,
                    RelayConnectionType));

            pendingPassword =
                (passWord ?? "").Trim();

            PrepareConnection();

            RegisterNetworkCallbacks();

            waitConnect =
                StartCoroutine(
                    WaitLog());

            if (!manager.StartClient())
            {
                waitConnect = null;

                Debug.LogError(
                    "[OnlineNetworkClient] No se pudo iniciar el cliente Relay.");

                return false;
            }

            RegisterMessageHandler();

            Debug.Log(
                $"[OnlineNetworkClient] Conectando mediante Relay. JoinCode={joinCode}");

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "[OnlineNetworkClient] Error uniéndose a Relay: " +
                ex);

            if (manager.IsListening)
                manager.Shutdown();

            return false;
        }
        finally
        {
            relayConnecting = false;
        }
    }

    private void PrepareConnection()
    {
        needLog = true;
        needLog2 = false;
        isHandOver = false;

        connectOverCalled = false;
        connectionResponseReceived = false;
        onlineCheck = false;

        ClearPendingPlayerInfo();
    }

    private void OnClientConnected(
        ulong clientId)
    {
        var manager = NetworkManager.Singleton;

        if (manager == null ||
            manager.IsServer ||
            clientId != manager.LocalClientId)
        {
            return;
        }

        Debug.Log(
            $"[OnlineNetworkClient] NGO conectado. ClientId: {clientId}");

        RegisterMessageHandler();
        SendConnectionInfo();
    }

    private void SendConnectionInfo()
    {
        var gm = GameManager.Instance;

        if (gm?.LocalPlayerSave == null)
        {
            Debug.LogWarning(
                "[OnlineNetworkClient] No se pudo preparar PlayerInfo.");

            return;
        }

        var info = new PlayerInfo
        {
            Name = gm.LocalPlayerSave.playerName,
            VersionCode = gm.VersionCode,
            CmdEnable = gm.LocalPlayerSave.CmdEnable,
            ReCntCode = reconnectCode,
            Password = pendingPassword
        };

        SendMsg(
            JsonUtility.ToJson(info),
            0,
            1);
    }

    private void OnClientDisconnect(
        ulong clientId)
    {
        var manager = NetworkManager.Singleton;

        if (manager == null ||
            manager.IsServer ||
            clientId != manager.LocalClientId)
        {
            return;
        }

        Debug.Log(
            $"[OnlineNetworkClient] NGO desconectado. ClientId: {clientId}");

        connectionResponseReceived = true;

        if (!isHandOver)
            ConnectOver();
    }

    public void ReceiveMessage(
        ulong senderClientId,
        FastBufferReader reader)
    {
        try
        {
            reader.ReadValueSafe(
                out byte type1);

            reader.ReadValueSafe(
                out byte type2);

            reader.ReadValueSafe(
                out string content);

            ProcessMessage(
                type1,
                type2,
                content ?? "");
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "[OnlineNetworkClient] Error leyendo mensaje: " +
                ex);
        }
    }

    public void ReceiveMessage(
        byte type1,
        byte type2,
        string content)
    {
        ProcessMessage(
            type1,
            type2,
            content ?? "");
    }

    private void SendMsg(
        string content,
        byte type1,
        byte type2)
    {
        var manager = NetworkManager.Singleton;

        if (manager == null ||
            !manager.IsListening)
        {
            return;
        }

        content ??= "";

        int size =
            Encoding.UTF8.GetByteCount(content) +
            256;

        if (size > MaxPacket)
            return;

        using var writer =
            new FastBufferWriter(
                size,
                Allocator.Temp);

        writer.WriteValueSafe(type1);
        writer.WriteValueSafe(type2);
        writer.WriteValueSafe(content);

        if (manager.IsServer)
        {
            OnlineNetworkServer.Instance?
                .ReceiveClientMessage(
                    NetworkManager.ServerClientId,
                    type1,
                    type2,
                    content);

            return;
        }

        if (manager.CustomMessagingManager == null)
            return;

        manager.CustomMessagingManager.SendNamedMessage(
            MessageName,
            NetworkManager.ServerClientId,
            writer);
    }

    private void ProcessMessage(
        byte type1,
        byte type2,
        string content)
    {
        if (type1 == 0)
        {
            ProcessConnection(
                type2,
                content);

            return;
        }

        if (type1 == 1)
        {
            ProcessGame(
                type2,
                content);

            return;
        }

        if (type1 == 2)
        {
            ProcessSpawn(
                type2,
                content);

            return;
        }

        if (type1 == 3)
        {
            ProcessWorld(
                type2,
                content);

            return;
        }

        if (type1 == 4)
        {
            ProcessCommand(
                type2,
                content);
        }
    }

    private void ProcessConnection(
        byte type,
        string content)
    {
        connectionResponseReceived = true;

        if (type == byte.MaxValue)
        {
            onlineCheck = true;
            return;
        }

        if (type == 1)
        {
            isHandOver = true;
            needLog = false;

            var info =
                JsonUtility.FromJson<ConnectInfo>(
                    content);

            if (info == null)
                return;

            var logPanel =
                UIManager.Instance?.LogPanel;

            if (logPanel == null)
            {
                CloseClient();
                return;
            }

            if (LVManager.Instance?.InGame == true)
            {
                logPanel.DisplayLog(
                    "Por favor, inténtelo de nuevo.",
                    null);
            }
            else
            {
                logPanel.DisplayLog(
                    info.msg ?? "",
                    () =>
                    {
                        if (GameManager.Instance?.isOnline == false)
                            MultiplayerUI.Instance?.OpenJoinCanvas();
                    });
            }

            CloseClient();
            return;
        }

        if (type == 2)
        {
            var info =
                JsonUtility.FromJson<OnlinePlayerInfo>(
                    content);

            if (info == null ||
                GameManager.Instance == null)
            {
                return;
            }

            ConnectSuccess();

            pendingOnlinePlayerInfo = info;
            hasPendingOnlinePlayerInfo = true;
            pendingPlayerListDone = false;
            pendingBattlePlayerListDone = false;

            ProcessPendingOnlinePlayerInfo();
            return;
        }

        if (type == 3)
        {
            var info =
                JsonUtility.FromJson<ReConnectInfo>(
                    content);

            if (info == null ||
                GameManager.Instance == null)
            {
                return;
            }

            ConnectSuccess();

            if (ReConnect.Instance == null)
            {
                CloseClient();
                return;
            }

            if (info.isWait)
                ReConnect.Instance.OpenInit(false);
            else
                ReConnect.Instance.OverClose();

            ReConnect.Instance.LoadPlayerList(
                info.names ??
                new List<string>());
        }
    }

    private void ProcessPendingOnlinePlayerInfo()
    {
        if (!hasPendingOnlinePlayerInfo ||
            pendingOnlinePlayerInfo == null)
        {
            return;
        }

        var info = pendingOnlinePlayerInfo;

        var players =
            info.players ??
            new List<PlayerInfo>();

        if (!pendingPlayerListDone &&
            PlayerList.Instance != null)
        {
            PlayerList.Instance.UpdatePlayerList(
                info.HostPlayer,
                players);

            pendingPlayerListDone = true;
        }

        if (!pendingBattlePlayerListDone &&
            BattlePlayerList.Instance != null)
        {
            BattlePlayerList.Instance.UpdatePlayerList(
                info.HostPlayer,
                players);

            pendingBattlePlayerListDone = true;
        }

        if (pendingPlayerListDone &&
            pendingBattlePlayerListDone)
        {
            ClearPendingPlayerInfo();
        }
    }

    private void ClearPendingPlayerInfo()
    {
        pendingOnlinePlayerInfo = null;
        hasPendingOnlinePlayerInfo = false;
        pendingPlayerListDone = false;
        pendingBattlePlayerListDone = false;
    }

    private void ProcessGame(
        byte type,
        string content)
    {
        if (type == 0)
        {
            var load =
                JsonUtility.FromJson<LoadLVBag>(
                    content);

            if (load == null ||
                LVManager.Instance == null)
                return;

            reconnectCode =
                load.ReCntCode;

            if (load.LoadType == 0)
            {
                LVManager.Instance.StartGame(
                    load,
                    -1);

                return;
            }

            if (load.LoadType == 1)
            {
                LVManager.Instance.ReStartGame();
                return;
            }

            if (load.LoadType == 2)
            {
                LVManager.Instance.QuitBattleGame();
                return;
            }

            return;
        }

        if (type == 1)
        {
            SeedChooser.Instance?.StartRunLv(true);
            ZombieChooser.Instance?.StartRunLv(true);
            return;
        }

        if (type == 2)
        {
            LVManager.Instance?.ClientShowBigWave(
                JsonUtility.FromJson<WaveComing>(
                    content));

            return;
        }

        if (type == 3)
        {
            PlayerManager.Instance?.ClientUpdateSunNum(
                JsonUtility.FromJson<SunNumBag>(
                    content));

            return;
        }

        if (type == 4)
        {
            var card =
                JsonUtility.FromJson<SelectCard>(
                    content);

            var save =
                GameManager.Instance?.LocalPlayerSave;

            if (card != null &&
                save != null &&
                card.PlayerName != save.playerName)
            {
                if (card.isBack)
                {
                    BattlePlayerList.Instance?.CancelCard(
                        card.PlayerName,
                        card.cardId);
                }
                else
                {
                    BattlePlayerList.Instance?.SelectCard(
                        card.PlayerName,
                        card.plantType,
                        card.zombieType,
                        card.noAnim,
                        card.cardId);
                }
            }

            return;
        }

        if (type == 5)
        {
            var prepare =
                JsonUtility.FromJson<SelectPrepare>(
                    content);

            if (prepare != null)
            {
                BattlePlayerList.Instance?.UpdateState(
                    prepare.PlayerName,
                    prepare.isPrepare);
            }

            return;
        }

        if (type == 6)
        {
            SynItem(
                JsonUtility.FromJson<SynItem>(
                    content));

            return;
        }

        if (type == 7)
        {
            var map =
                JsonUtility.FromJson<PlayerMap>(
                    content);

            if (map != null)
            {
                BattlePlayerList.Instance?.UpdateMapSprite(
                    map.PlayerName,
                    map.Pos);
            }

            return;
        }

        if (type == 8)
        {
            var over =
                JsonUtility.FromJson<GameOver>(
                    content);

            if (over == null ||
                LV.Instance == null ||
                LVManager.Instance == null)
                return;

            if (LV.Instance.CurrLVType == LVType.PvP)
            {
                LVManager.Instance.PvPGameOver(
                    over.pos,
                    over.isRedFail);
            }
            else
            {
                LVManager.Instance.ZombieGameOver(
                    over.pos);
            }

            return;
        }

        if (type == 9)
        {
            PvPSelector.Instance?.ClientSynTeam(
                JsonUtility.FromJson<PvPTeamList>(
                    content));

            return;
        }

        if (type == 10)
        {
            PvPSelector.Instance?.ClientSynMode(
                JsonUtility.FromJson<PvPModeSyn>(
                    content));

            return;
        }

        if (type == 11)
        {
            var list =
                JsonUtility.FromJson<SpectList>(
                    content);

            if (list != null)
            {
                SpectatorList.Instance?.ClientSynList(
                    list.names ??
                    new List<string>());
            }

            return;
        }

        if (type == 12)
        {
            var add =
                JsonUtility.FromJson<AddCardBag>(
                    content);

            if (add != null)
            {
                SeedBank.Instance?.AddCards(
                    add.CardTypes,
                    true);
            }

            return;
        }

        if (type == 13)
        {
            FlagMeter.Instance?.ClientSyn(
                JsonUtility.FromJson<FlagMeterSyn>(
                    content));

            return;
        }

        if (type == 14)
        {
            Timetable.Instance?.ClientSyn(
                JsonUtility.FromJson<TimetableSyn>(
                    content));
        }
    }

    private void ProcessSpawn(
        byte type,
        string content)
    {
        if (type == 0)
        {
            var plant =
                JsonUtility.FromJson<PlantSpawn>(
                    content);

            if (plant == null ||
                PlantManager.Instance == null ||
                SeedBank.Instance == null ||
                MapManager.Instance == null)
                return;

            var obj =
                PlantManager.Instance.GetNewPlant(
                    plant.plantType);

            if (obj == null)
                return;

            if (plant.SPcode == 2)
            {
                obj.InitForCreate(
                    false,
                    null,
                    false);
            }

            ReversePvP(
                ref plant.GridPos,
                plant.PlacePlayer);

            obj.OnlineId =
                plant.OnlineId;

            var grid =
                MapManager.Instance.GetGridByWorldPos(
                    plant.GridPos);

            if (grid != null)
            {
                SeedBank.Instance.PlantConfirm(
                    obj,
                    grid,
                    -1,
                    plant.SPcode,
                    plant.PlacePlayer);
            }

            return;
        }

        if (type == 1)
        {
            SkyManager.Instance?.ClientSpawnSun(
                JsonUtility.FromJson<SunSpawn>(
                    content));

            return;
        }

        if (type == 2)
        {
            SkyManager.Instance?.OnlineCollectSun(
                JsonUtility.FromJson<ClickedSun>(
                    content));

            return;
        }

        if (type == 3)
        {
            BattlePlayerList.Instance?.PreviewPlant(
                JsonUtility.FromJson<PlantPreview>(
                    content));

            return;
        }

        if (type == 4)
        {
            var cd =
                JsonUtility.FromJson<UpdateCardCD>(
                    content);

            if (cd == null)
                return;

            var save =
                GameManager.Instance?.LocalPlayerSave;

            if (save != null &&
                cd.name == save.playerName)
            {
                if (cd.OK)
                {
                    SeedBank.Instance?.PlantFailClearCD(
                        cd.CardId);
                }
            }
            else
            {
                BattlePlayerList.Instance?.UpdateCardCD(
                    cd.name,
                    cd.CardId,
                    cd.OK);
            }

            return;
        }

        if (type == 5)
        {
            var shovel =
                JsonUtility.FromJson<ShovelPreview>(
                    content);

            if (shovel != null)
            {
                BattlePlayerList.Instance?.PreviewShovel(
                    shovel.PlayerName,
                    shovel.GridPos,
                    shovel.isShow);
            }

            return;
        }

        if (type == 6)
        {
            var tool =
                JsonUtility.FromJson<ToolApply>(
                    content);

            if (tool != null)
            {
                BattlePlayerList.Instance?
                    .PlayShovelAnimation(
                        tool.GridPos,
                        tool.Sound,
                        tool.User);
            }
        }
    }

    private void ProcessWorld(
        byte type,
        string content)
    {
        if (type == 0)
        {
            ZombieManager.Instance?.UpdateZombie(
                JsonUtility.FromJson<ZombieSpawn>(
                    content));

            return;
        }

        if (type == 1)
        {
            var grave =
                JsonUtility.FromJson<GraveStoneSpawn>(
                    content);

            var grid =
                grave != null
                    ? MapManager.Instance?
                        .GetGridByWorldPos(
                            grave.MapPos)
                    : null;

            if (grid != null)
            {
                grid.ClientSynGrave(
                    grave.Type,
                    grave.isHave);
            }

            return;
        }

        if (type == 2)
        {
            CreatePuddle(
                JsonUtility.FromJson<PuddleSpawn>(
                    content));

            return;
        }

        if (type == 3)
        {
            var light =
                JsonUtility.FromJson<LightingSpawn>(
                    content);

            var grid =
                light != null
                    ? MapManager.Instance?
                        .GetGridByWorldPos(
                            light.Pos)
                    : null;

            if (grid != null)
            {
                SkyManager.Instance?.ClientLightningThis(
                    grid);
            }

            return;
        }

        if (type == 4)
        {
            var map =
                JsonUtility.FromJson<SynMap>(
                    content);

            if (map != null)
            {
                MapManager.Instance?
                    .GetCurrMap(map.mapPos)?
                    .SynMap(map);
            }

            return;
        }

        if (type == 5)
        {
            BattlePlayerList.Instance?.PreviewZombie(
                JsonUtility.FromJson<ZombiePreview>(
                    content));

            return;
        }

        if (type == 6)
        {
            MapManager.Instance?.ClientCreatePortal(
                JsonUtility.FromJson<PortalSpawn>(
                    content));

            return;
        }

        if (type == 7)
        {
            SynGrid(
                JsonUtility.FromJson<SynGrid>(
                    content));

            return;
        }

        if (type == 8)
        {
            var booty =
                JsonUtility.FromJson<SynBooty>(
                    content);

            if (booty == null ||
                LVManager.Instance?.InGame != true)
                return;

            if (booty.isSpawn)
            {
                LVManager.Instance.SpawnBooty(
                    booty.pos,
                    booty);
            }
            else
            {
                LVManager.Instance.OnlyBooty?
                    .CollectBooty();
            }

            return;
        }

        if (type == 9)
        {
            LvItemManager.Instance?.ClientCreateVase(
                JsonUtility.FromJson<VaseSpawn>(
                    content));

            return;
        }

        if (type == 10)
        {
            SeedBank.Instance?.ClientSpawnCard(
                JsonUtility.FromJson<CardSpawn>(
                    content));

            return;
        }

        if (type == 11)
        {
            LvItemManager.Instance?.SpawnMelt(
                JsonUtility.FromJson<MeltSpawn>(
                    content));

            return;
        }

        if (type == 12)
        {
            LvItemManager.Instance?.SpawnFallHail(
                JsonUtility.FromJson<FallHailSpawn>(
                    content));
        }
    }

    private void CreatePuddle(
        PuddleSpawn spawn)
    {
        if (spawn == null ||
            MapManager.Instance == null ||
            GameManager.Instance?.GameConf?.Puddle == null)
        {
            return;
        }

        var grids = new List<Grid>();

        foreach (var pos in
                 spawn.MapPos ??
                 new List<Vector2>())
        {
            var grid =
                MapManager.Instance.GetGridByWorldPos(
                    pos);

            if (grid != null)
                grids.Add(grid);
        }

        var puddle =
            Instantiate(
                GameManager.Instance.GameConf.Puddle)
            .GetComponent<Puddle>();

        if (puddle == null)
            return;

        puddle.CreateInit(
            grids,
            spawn.InitPos,
            spawn.OnlineId);

        MapManager.Instance.puddles.Add(
            puddle);
    }

    private void SynGrid(
        SynGrid data)
    {
        if (data == null ||
            MapManager.Instance == null ||
            LV.Instance == null)
        {
            return;
        }

        var gm = GameManager.Instance;

        if (LV.Instance.CurrLVType == LVType.PvP &&
            PvPSelector.Instance != null &&
            gm != null &&
            !PvPSelector.Instance.IsSameTeam(
                gm.HostName))
        {
            data.GridPos =
                MyTool.ReverseX(
                    data.GridPos);
        }

        MapManager.Instance
            .GetGridByWorldPos(
                data.GridPos)?
            .ClientSynState(data);
    }

    private void ProcessCommand(
        byte type,
        string content)
    {
        if (type == 0)
        {
            ChatInput.Instance?.AddMessage(
                content);

            return;
        }

        if (type == 1)
        {
            ChatInput.Instance?.AddMessage(
                content,
                new Color32(
                    255,
                    255,
                    0,
                    255));

            return;
        }

        if (type == 2)
        {
            var chat =
                JsonUtility.FromJson<PrivateChatMsg>(
                    content);

            if (chat != null)
            {
                ChatInput.Instance?.AddMessage(
                    "Jugador" +
                    chat.PlayerName +
                    "Susurro:" +
                    chat.content,
                    new Color32(
                        123,
                        123,
                        123,
                        255));
            }

            return;
        }

        if (type == 3)
        {
            var cmd =
                JsonUtility.FromJson<CommandBag>(
                    content);

            if (cmd == null)
                return;

            if (PlantManager.Instance != null)
                PlantManager.Instance.PlantInvincible =
                    cmd.Pinv;

            if (ZombieManager.Instance != null)
            {
                ZombieManager.Instance.ZombieInvincible =
                    cmd.Zinv;

                ZombieManager.Instance.ZombieDontMove =
                    cmd.ZomStop;
            }

            if (SkyManager.Instance != null)
                SkyManager.Instance.DayLightCycle =
                    cmd.DLiCy;

            if (PlayerManager.Instance != null)
                PlayerManager.Instance.SunInfinite =
                    cmd.SnInf;

            if (SeedBank.Instance != null)
                SeedBank.Instance.isNoCD =
                    cmd.CdCle;

            if (LvItemManager.Instance != null)
                LvItemManager.Instance.VaseAlwaysLight =
                    cmd.VaseXray;

            return;
        }

        if (type == 4)
        {
            SkyManager.Instance?.ClientSynWeather(
                JsonUtility.FromJson<WeatherChange>(
                    content));

            return;
        }

        if (type == 5)
        {
            var time =
                JsonUtility.FromJson<TimeCmd>(
                    content);

            if (time == null ||
                SkyManager.Instance == null)
                return;

            if (time.time >= 10000)
            {
                SkyManager.Instance.DirectSetTime(
                    time.time - 10000,
                    true);
            }
            else
            {
                SkyManager.Instance.Time =
                    time.time;
            }

            return;
        }

        if (type == 6)
        {
            var achievement =
                JsonUtility.FromJson<GetAcvment>(
                    content);

            if (achievement != null)
            {
                AcvmentManager.Instance?.GetAchievement(
                    achievement.acv);
            }

            return;
        }

        if (type == 99)
            Debug.Log(content);
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

    public void SynItem(
        SynItem syn)
    {
        if (syn == null)
            return;

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Plant)
        {
            var plants =
                PlantManager.Instance?.plants;

            if (plants != null)
            {
                foreach (var plant in plants)
                {
                    if (plant?.OnlineId != syn.OnlineId)
                        continue;

                    plant.OnlineSynPlant(syn);
                    break;
                }
            }
        }

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Zombie)
        {
            var manager =
                ZombieManager.Instance;

            if (manager != null)
            {
                var zombies =
                    new List<ZombieBase>(
                        manager.GetAllZombies());

                var hyp =
                    manager.GetAllHypZombies();

                if (hyp != null)
                    zombies.AddRange(hyp);

                foreach (var zombie in zombies)
                {
                    if (zombie?.OnlineId != syn.OnlineId)
                        continue;

                    zombie.OnlineSynZombie(syn);
                    break;
                }
            }
        }

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Puddle)
        {
            var puddles =
                MapManager.Instance?.puddles;

            if (puddles != null)
            {
                foreach (var puddle in puddles)
                {
                    if (puddle?.OnlineId != syn.OnlineId)
                        continue;

                    puddle.StartDisappear();
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
                foreach (var portal in portals)
                {
                    if (portal?.OnlineId != syn.OnlineId)
                        continue;

                    portal.ClientReset(syn);
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

    private IEnumerator SendHeartbeat()
    {
        float timer =
            Time.realtimeSinceStartup;

        while (
            GameManager.Instance?.isOnline == true)
        {
            yield return null;

            if (Time.realtimeSinceStartup - timer <= .5f)
                continue;

            SendMsg(
                "",
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
            GameManager.Instance?.isOnline == true)
        {
            yield return null;

            if (Time.realtimeSinceStartup - timer <= 3f)
                continue;

            if (!onlineCheck)
            {
                CloseClient();
                yield break;
            }

            onlineCheck = false;
            timer =
                Time.realtimeSinceStartup;
        }
    }

    private IEnumerator WaitLog()
    {
        float timer =
            Time.realtimeSinceStartup;

        while (
            Time.realtimeSinceStartup - timer <= 15f &&
            GameManager.Instance?.isOnline == false &&
            waitConnect != null &&
            !connectionResponseReceived)
        {
            yield return null;
        }

        if (GameManager.Instance?.isOnline == true ||
            connectionResponseReceived)
        {
            waitConnect = null;
            yield break;
        }

        waitConnect = null;

        CloseClient();

        UIManager.Instance?.LogPanel?.Confirm();
    }

    private void ConnectSuccess()
    {
        var gm =
            GameManager.Instance;

        if (gm == null ||
            gm.isOnline)
        {
            return;
        }

        if (waitConnect != null)
        {
            StopCoroutine(waitConnect);
            waitConnect = null;
        }

        needLog2 = true;

        Application.runInBackground = true;

        gm.isOnline = true;
        onlineCheck = true;

        StartCoroutine(CheckConnect());
        StartCoroutine(SendHeartbeat());

        MultiplayerUI.Instance?.ConnectSuccess();
    }

    public void CloseClient()
    {
        var manager =
            NetworkManager.Singleton;

        if (manager != null &&
            manager.IsListening)
        {
            SendMsg(
                "",
                0,
                2);

            isHandOver = true;

            manager.Shutdown();
        }

        needLog = false;
        needLog2 = false;

        ConnectOver();
    }

    public void ReConnectGiveUp()
    {
        CloseClient();
        ConnectOverDone();
    }

    private void ConnectOver()
    {
        if (connectOverCalled)
            return;

        connectOverCalled = true;

        try
        {
            var gm =
                GameManager.Instance;

            if (gm == null)
                return;

            if (!gm.isOnline)
            {
                StopWaitConnect();
                return;
            }

            gm.isOnline = false;

            StopWaitConnect();

            if (LVManager.Instance?.InGame == true)
            {
                if (isHandOver)
                {
                    LVManager.Instance.QuitBattleGame();
                    ConnectOverDone();
                }
                else if (ReConnect.Instance != null)
                {
                    StopAllCoroutines();
                    ReConnect.Instance.OpenInit(true);
                }
                else
                {
                    ConnectOverDone();
                }
            }
            else
            {
                ConnectOverDone();
            }
        }
        finally
        {
            connectOverCalled = false;
        }
    }

    private void StopWaitConnect()
    {
        if (waitConnect == null)
            return;

        StopCoroutine(waitConnect);
        waitConnect = null;
    }

    private void ConnectOverDone()
    {
        reconnectCode = 0;
        connectionResponseReceived = false;

        ClearPendingPlayerInfo();

        SpectatorList.Instance?.ClientSynList(
            new List<string>());

        PvPSelector.Instance?.ResetPvPInfo();

        PlayerList.Instance?.UpdatePlayerList(
            null,
            new List<PlayerInfo>());

        BattlePlayerList.Instance?.UpdatePlayerList(
            null,
            new List<PlayerInfo>());

        StopAllCoroutines();

        if (UIManager.Instance?.LogPanel == null)
            return;

        if (needLog && needLog2)
        {
            UIManager.Instance.LogPanel.DisplayLog(
                "Desconectado del servidor",
                null);
        }
        else if (needLog)
        {
            UIManager.Instance.LogPanel.DisplayLog(
                "La conexión ha caducado.",
                () =>
                    MultiplayerUI.Instance?.OpenJoinCanvas());
        }
    }

    public void SendChatMsg(
        string msg)
    {
        SendMsg(msg, 2, 0);
    }

    public void SendPrivateChatMsg(
        string name,
        string content)
    {
        SendMsg(
            JsonUtility.ToJson(
                new PrivateChatMsg
                {
                    PlayerName = name,
                    content = content
                }),
            2,
            1);
    }

    public void ChangeMap(
        PlayerMap map)
    {
        SendMsg(
            JsonUtility.ToJson(map),
            1,
            3);
    }

    public void SelectCard(
        SelectCard card)
    {
        SendMsg(
            JsonUtility.ToJson(card),
            1,
            4);
    }

    public void SelectPrepare(
        SelectPrepare prepare)
    {
        SendMsg(
            JsonUtility.ToJson(prepare),
            1,
            5);
    }

    public void ApplyTool(
        ToolApply apply)
    {
        SendMsg(
            JsonUtility.ToJson(apply),
            1,
            1);
    }

    public void UpdateCD(
        int cardID,
        bool Ok)
    {
        SendMsg(
            JsonUtility.ToJson(
                new UpdateCardCD
                {
                    CardId = cardID,
                    OK = Ok
                }),
            1,
            8);
    }

    public void ClickedSun(
        ClickedSun sun)
    {
        SendMsg(
            JsonUtility.ToJson(sun),
            1,
            2);
    }

    public void ApplyPlacePlant(
        PlantSpawn spawn)
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            0);
    }

    public void ApplyPlaceZombie(
        ZombieSpawnApply spawn)
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            10);
    }

    public void ApplyPlacePreview(
        PlantPreview spawn)
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            7);
    }

    public void ApplyShovelPreview(
        ShovelPreview spawn)
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            9);
    }

    public void ApplyZombiePreview(
        ZombiePreview spawn)
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            11);
    }

    public void ApplyJoinTeam(
        JoinTeamApply spawn)
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            12);
    }

    public void ApplyJoinSpect(
        JoinSpecApply spawn)
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            13);
    }

    public void SendSynBag(
        SynItem syn)
    {
        SendMsg(
            JsonUtility.ToJson(syn),
            1,
            6);
    }

    public void SendSlotMBag(
        SlotMchBag bag)
    {
        SendMsg(
            JsonUtility.ToJson(bag),
            1,
            14);
    }
}