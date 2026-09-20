using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using SocketSave;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class OnlineNetworkClient : MonoBehaviour
{
    public static OnlineNetworkClient Instance;

    const string MessageName = "PVZ_ONLINE_MESSAGE";
    const int MaxPacket = 1048576;

    bool needLog;
    bool needLog2;
    bool OnlineCheck;
    bool IsHandOver;

    public TextMesh text;

    int ReConnectCode;

    bool connectOverCalled;
    bool connectionResponseReceived;

    Coroutine WaitConnect;

    string pendingPassword;

    OnlinePlayerInfo pendingOnlinePlayerInfo;
    bool hasPendingOnlinePlayerInfo;
    bool pendingPlayerListDone;
    bool pendingBattlePlayerListDone;

    void Awake()
    {
        Instance = this;
        Application.runInBackground = true;
    }

    void Update()
    {
        ProcessPendingOnlinePlayerInfo();
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            try
            {
                NetworkManager.Singleton.CustomMessagingManager
                    ?.UnregisterNamedMessageHandler(MessageName);
            }
            catch
            {
            }

            NetworkManager.Singleton.OnClientConnectedCallback -=
                OnClientConnected;

            NetworkManager.Singleton.OnClientDisconnectCallback -=
                OnClientDisconnect;
        }

        if (Instance == this)
            Instance = null;
    }

    void RegisterMessageHandler()
    {
        var manager = NetworkManager.Singleton;

        if (manager == null ||
            manager.CustomMessagingManager == null)
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

    void RegisterNetworkCallbacks()
    {
        var manager = NetworkManager.Singleton;

        if (manager == null)
            return;

        manager.OnClientConnectedCallback -= OnClientConnected;
        manager.OnClientDisconnectCallback -= OnClientDisconnect;

        manager.OnClientConnectedCallback += OnClientConnected;
        manager.OnClientDisconnectCallback += OnClientDisconnect;
    }

    void ConfigureTransport(IPAddress ip, int port)
    {
        var manager = NetworkManager.Singleton;

        if (manager == null)
            return;

        var transport = manager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError("No se encontró UnityTransport.");
            return;
        }

        transport.SetConnectionData(
            ip.ToString(),
            (ushort)port,
            "0.0.0.0");
    }

    public void JoinGame(
        IPAddress ip,
        int port,
        string passWord)
    {
        var gm = GameManager.Instance;

        if (gm == null ||
            WaitConnect != null ||
            gm.isOnline)
        {
            return;
        }

        var manager = NetworkManager.Singleton;

        if (manager == null)
        {
            Debug.LogError("No existe NetworkManager.");
            return;
        }

        if (manager.IsListening)
            manager.Shutdown();

        if (gm.LocalPlayerSave == null)
            return;

        pendingPassword = passWord ?? "";

        needLog = true;
        needLog2 = false;
        IsHandOver = false;

        connectOverCalled = false;
        connectionResponseReceived = false;
        OnlineCheck = false;

        pendingOnlinePlayerInfo = null;
        hasPendingOnlinePlayerInfo = false;
        pendingPlayerListDone = false;
        pendingBattlePlayerListDone = false;

        ConfigureTransport(ip, port);
        RegisterNetworkCallbacks();

        WaitConnect = StartCoroutine(WaitLog());

        if (!manager.StartClient())
        {
            WaitConnect = null;
            Debug.LogError("No se pudo iniciar el cliente NGO.");
            return;
        }

        RegisterMessageHandler();
    }

    void OnClientConnected(ulong clientId)
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

        Debug.Log(
            "[OnlineNetworkClient] Enviando PlayerInfo...");

        SendConnectionInfo();
    }

    void SendConnectionInfo()
    {
        var gm = GameManager.Instance;

        if (gm == null ||
            gm.LocalPlayerSave == null)
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
            ReCntCode = ReConnectCode,
            Password = pendingPassword
        };

        Debug.Log(
            $"[OnlineNetworkClient] PlayerInfo preparado. Name={info.Name} | Version={info.VersionCode} | ReCntCode={info.ReCntCode}");

        SendMsg(
            JsonUtility.ToJson(info),
            0,
            1);
    }

    void OnClientDisconnect(ulong clientId)
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

        if (!connectionResponseReceived &&
            !IsHandOver)
        {
            connectionResponseReceived = true;
        }

        if (!IsHandOver)
            ConnectOver();
    }

    public void ReceiveMessage(
        ulong senderClientId,
        FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte type1);
        reader.ReadValueSafe(out byte type2);
        reader.ReadValueSafe(out string content);

        ProcessMessage(
            type1,
            type2,
            content ?? "");
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

    void SendMsg(
        string content,
        byte type1,
        byte type2)
    {
        var manager = NetworkManager.Singleton;

        if (manager == null ||
            !manager.IsListening)
        {
            Debug.LogWarning(
                $"[OnlineNetworkClient] SendMsg cancelado: NetworkManager inexistente o no está escuchando. Type1={type1} Type2={type2}");
            return;
        }

        content ??= "";

        int stringBytes =
            Encoding.UTF8.GetByteCount(content);

        int size =
            stringBytes + 256;

        if (size > MaxPacket)
        {
            Debug.LogWarning(
                $"[OnlineNetworkClient] Mensaje demasiado grande. Bytes={stringBytes}");
            return;
        }

        using var writer =
            new FastBufferWriter(
                size,
                Allocator.Temp);

        writer.WriteValueSafe(type1);
        writer.WriteValueSafe(type2);
        writer.WriteValueSafe(content);

        Debug.Log(
            $"[OnlineNetworkClient] SendMsg. Type1={type1} | Type2={type2} | IsServer={manager.IsServer} | IsClient={manager.IsClient} | CustomMessagingManager={manager.CustomMessagingManager != null}");

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
        {
            Debug.LogWarning(
                "[OnlineNetworkClient] CustomMessagingManager inexistente.");
            return;
        }

        manager.CustomMessagingManager.SendNamedMessage(
            MessageName,
            NetworkManager.ServerClientId,
            writer);
    }

    void ProcessMessage(
        byte type1,
        byte type2,
        string s)
    {
        if (type1 == 0)
            ProcessConnection(type2, s);
        else if (type1 == 1)
            ProcessGame(type2, s);
        else if (type1 == 2)
            ProcessSpawn(type2, s);
        else if (type1 == 3)
            ProcessWorld(type2, s);
        else if (type1 == 4)
            ProcessCommand(type2, s);
    }

    void ProcessConnection(
        byte type,
        string s)
    {
        connectionResponseReceived = true;

        if (type == byte.MaxValue)
        {
            OnlineCheck = true;
            return;
        }

        if (type == 1)
        {
            IsHandOver = true;
            needLog = false;

            var info =
                JsonUtility.FromJson<ConnectInfo>(s);

            if (info == null)
                return;

            if (UIManager.Instance?.LogPanel == null)
            {
                CloseClient();
                return;
            }

            if (LVManager.Instance?.InGame == true)
            {
                UIManager.Instance.LogPanel.DisplayLog(
                    "Por favor, inténtelo de nuevo.",
                    null);
            }
            else
            {
                UIManager.Instance.LogPanel.DisplayLog(
                    info.msg ?? "",
                    () =>
                    {
                        if (GameManager.Instance?.isOnline == false &&
                            UIManager.Instance?.JoinGame != null)
                        {
                            UIManager.Instance.JoinGame
                                .gameObject
                                .SetActive(true);
                        }
                    });
            }

            CloseClient();
            return;
        }

        if (type == 2)
        {
            var info =
                JsonUtility.FromJson<OnlinePlayerInfo>(s);

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
                JsonUtility.FromJson<ReConnectInfo>(s);

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

    void ProcessPendingOnlinePlayerInfo()
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
            pendingOnlinePlayerInfo = null;
            hasPendingOnlinePlayerInfo = false;
        }
    }

    void ProcessGame(byte type, string s)
    {
        if (type == 0)
        {
            var load =
                JsonUtility.FromJson<LoadLVBag>(s);

            if (load == null ||
                LVManager.Instance == null)
                return;

            ReConnectCode = load.ReCntCode;

            if (load.LoadType == 0)
                LVManager.Instance.StartGame(load, -1);
            else if (load.LoadType == 1)
                LVManager.Instance.ReStartGame();
            else if (load.LoadType == 2)
                LVManager.Instance.QuitBattleGame();

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
                JsonUtility.FromJson<WaveComing>(s));
            return;
        }

        if (type == 3)
        {
            PlayerManager.Instance?.ClientUpdateSunNum(
                JsonUtility.FromJson<SunNumBag>(s));
            return;
        }

        if (type == 4)
        {
            SynItem(
                JsonUtility.FromJson<SynItem>(s));
            return;
        }

        if (type == 5)
        {
            var map =
                JsonUtility.FromJson<PlayerMap>(s);

            if (map != null)
            {
                BattlePlayerList.Instance?.UpdateMapSprite(
                    map.PlayerName,
                    map.Pos);
            }

            return;
        }

        if (type == 6)
        {
            var card =
                JsonUtility.FromJson<SelectCard>(s);

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

        if (type == 7)
        {
            var prepare =
                JsonUtility.FromJson<SelectPrepare>(s);

            if (prepare != null)
            {
                BattlePlayerList.Instance?.UpdateState(
                    prepare.PlayerName,
                    prepare.isPrepare);
            }

            return;
        }

        if (type == 8)
        {
            var over =
                JsonUtility.FromJson<GameOver>(s);

            if (over != null &&
                LV.Instance != null &&
                LVManager.Instance != null)
            {
                if (LV.Instance.CurrLVType == LVType.PvP)
                    LVManager.Instance.PvPGameOver(
                        over.pos,
                        over.isRedFail);
                else
                    LVManager.Instance.ZombieGameOver(
                        over.pos);
            }

            return;
        }

        if (type == 9)
        {
            PvPSelector.Instance?.ClientSynTeam(
                JsonUtility.FromJson<PvPTeamList>(s));
            return;
        }

        if (type == 10)
        {
            PvPSelector.Instance?.ClientSynMode(
                JsonUtility.FromJson<PvPModeSyn>(s));
            return;
        }

        if (type == 11)
        {
            var list =
                JsonUtility.FromJson<SpectList>(s);

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
                JsonUtility.FromJson<AddCardBag>(s);

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
                JsonUtility.FromJson<FlagMeterSyn>(s));
            return;
        }

        if (type == 14)
        {
            Timetable.Instance?.ClientSyn(
                JsonUtility.FromJson<TimetableSyn>(s));
        }
    }

    void ProcessSpawn(byte type, string s)
    {
        if (type == 0)
        {
            var plant =
                JsonUtility.FromJson<PlantSpawn>(s);

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

            obj.OnlineId = plant.OnlineId;

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
                JsonUtility.FromJson<SunSpawn>(s));
            return;
        }

        if (type == 2)
        {
            SkyManager.Instance?.OnlineCollectSun(
                JsonUtility.FromJson<ClickedSun>(s));
            return;
        }

        if (type == 3)
        {
            BattlePlayerList.Instance?.PreviewPlant(
                JsonUtility.FromJson<PlantPreview>(s));
            return;
        }

        if (type == 4)
        {
            var cd =
                JsonUtility.FromJson<UpdateCardCD>(s);

            if (cd == null)
                return;

            var save =
                GameManager.Instance?.LocalPlayerSave;

            if (save != null &&
                cd.name == save.playerName)
            {
                if (cd.OK)
                    SeedBank.Instance?.PlantFailClearCD(
                        cd.CardId);
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
                JsonUtility.FromJson<ShovelPreview>(s);

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
                JsonUtility.FromJson<ToolApply>(s);

            if (tool != null)
            {
                BattlePlayerList.Instance?.PlayShovelAnimation(
                    tool.GridPos,
                    tool.Sound,
                    tool.User);
            }
        }
    }

    void ProcessWorld(byte type, string s)
    {
        if (type == 0)
        {
            ZombieManager.Instance?.UpdateZombie(
                JsonUtility.FromJson<ZombieSpawn>(s));
            return;
        }

        if (type == 1)
        {
            var grave =
                JsonUtility.FromJson<GraveStoneSpawn>(s);

            var ggrid =
                grave != null
                    ? MapManager.Instance?
                        .GetGridByWorldPos(grave.MapPos)
                    : null;

            if (ggrid != null)
            {
                ggrid.ClientSynGrave(
                    grave.Type,
                    grave.isHave);
            }

            return;
        }

        if (type == 2)
        {
            CreatePuddle(
                JsonUtility.FromJson<PuddleSpawn>(s));
            return;
        }

        if (type == 3)
        {
            var light =
                JsonUtility.FromJson<LightingSpawn>(s);

            var lgrid =
                light != null
                    ? MapManager.Instance?
                        .GetGridByWorldPos(light.Pos)
                    : null;

            if (lgrid != null)
            {
                SkyManager.Instance?.ClientLightningThis(
                    lgrid);
            }

            return;
        }

        if (type == 4)
        {
            var map =
                JsonUtility.FromJson<SynMap>(s);

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
                JsonUtility.FromJson<ZombiePreview>(s));
            return;
        }

        if (type == 6)
        {
            MapManager.Instance?.ClientCreatePortal(
                JsonUtility.FromJson<PortalSpawn>(s));
            return;
        }

        if (type == 7)
        {
            SynGrid(
                JsonUtility.FromJson<SynGrid>(s));
            return;
        }

        if (type == 8)
        {
            var booty =
                JsonUtility.FromJson<SynBooty>(s);

            if (booty != null &&
                LVManager.Instance?.InGame == true)
            {
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
            }

            return;
        }

        if (type == 9)
        {
            LvItemManager.Instance?.ClientCreateVase(
                JsonUtility.FromJson<VaseSpawn>(s));
            return;
        }

        if (type == 10)
        {
            SeedBank.Instance?.ClientSpawnCard(
                JsonUtility.FromJson<CardSpawn>(s));
            return;
        }

        if (type == 11)
        {
            LvItemManager.Instance?.SpawnMelt(
                JsonUtility.FromJson<MeltSpawn>(s));
            return;
        }

        if (type == 12)
        {
            LvItemManager.Instance?.SpawnFallHail(
                JsonUtility.FromJson<FallHailSpawn>(s));
        }
    }

    void CreatePuddle(PuddleSpawn spawn)
    {
        if (spawn == null ||
            MapManager.Instance == null ||
            GameManager.Instance?.GameConf?.Puddle == null)
            return;

        var grids =
            new List<Grid>();

        foreach (var pos in
                 spawn.MapPos ??
                 new List<Vector2>())
        {
            var grid =
                MapManager.Instance.GetGridByWorldPos(pos);

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

        MapManager.Instance.puddles.Add(puddle);
    }

    void SynGrid(SynGrid data)
    {
        if (data == null ||
            MapManager.Instance == null ||
            LV.Instance == null)
            return;

        if (LV.Instance.CurrLVType == LVType.PvP &&
            PvPSelector.Instance != null &&
            GameManager.Instance != null &&
            !PvPSelector.Instance.IsSameTeam(
                GameManager.Instance.HostName))
        {
            data.GridPos =
                MyTool.ReverseX(data.GridPos);
        }

        MapManager.Instance
            .GetGridByWorldPos(data.GridPos)?
            .ClientSynState(data);
    }

    void ProcessCommand(byte type, string s)
    {
        if (type == 0)
        {
            ChatInput.Instance?.AddMessage(s);
            return;
        }

        if (type == 1)
        {
            ChatInput.Instance?.AddMessage(
                s,
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
                JsonUtility.FromJson<PrivateChatMsg>(s);

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
                JsonUtility.FromJson<CommandBag>(s);

            if (cmd == null)
                return;

            if (PlantManager.Instance != null)
                PlantManager.Instance.PlantInvincible = cmd.Pinv;

            if (ZombieManager.Instance != null)
            {
                ZombieManager.Instance.ZombieInvincible =
                    cmd.Zinv;

                ZombieManager.Instance.ZombieDontMove =
                    cmd.ZomStop;
            }

            if (SkyManager.Instance != null)
                SkyManager.Instance.DayLightCycle = cmd.DLiCy;

            if (PlayerManager.Instance != null)
                PlayerManager.Instance.SunInfinite = cmd.SnInf;

            if (SeedBank.Instance != null)
                SeedBank.Instance.isNoCD = cmd.CdCle;

            if (LvItemManager.Instance != null)
                LvItemManager.Instance.VaseAlwaysLight =
                    cmd.VaseXray;

            return;
        }

        if (type == 4)
        {
            SkyManager.Instance?.ClientSynWeather(
                JsonUtility.FromJson<WeatherChange>(s));
            return;
        }

        if (type == 5)
        {
            var time =
                JsonUtility.FromJson<TimeCmd>(s);

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
                JsonUtility.FromJson<GetAcvment>(s);

            if (achievement != null)
            {
                AcvmentManager.Instance?.GetAchievement(
                    achievement.acv);
            }

            return;
        }

        if (type == 99)
            Debug.Log(s);
    }

    void ReversePvP(ref Vector2 pos, string player)
    {
        if (LV.Instance?.CurrLVType == LVType.PvP &&
            PvPSelector.Instance != null &&
            !PvPSelector.Instance.IsSameTeam(player))
        {
            pos.x = -pos.x;
        }
    }

    public void SynItem(SynItem syn)
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
                    if (plant?.OnlineId == syn.OnlineId)
                    {
                        plant.OnlineSynPlant(syn);
                        break;
                    }
                }
            }
        }

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Zombie)
        {
            if (ZombieManager.Instance != null)
            {
                var zombies =
                    new List<ZombieBase>(
                        ZombieManager.Instance.GetAllZombies());

                var hyp =
                    ZombieManager.Instance.GetAllHypZombies();

                if (hyp != null)
                    zombies.AddRange(hyp);

                foreach (var zombie in zombies)
                {
                    if (zombie?.OnlineId == syn.OnlineId)
                    {
                        zombie.OnlineSynZombie(syn);
                        break;
                    }
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
                    if (puddle?.OnlineId == syn.OnlineId)
                    {
                        puddle.StartDisappear();
                        break;
                    }
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
                    if (portal?.OnlineId == syn.OnlineId)
                    {
                        portal.ClientReset(syn);
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

    IEnumerator SendHeartbeat()
    {
        float t =
            Time.realtimeSinceStartup;

        while (GameManager.Instance?.isOnline == true)
        {
            yield return null;

            if (Time.realtimeSinceStartup - t > .5f)
            {
                SendMsg(
                    "",
                    0,
                    byte.MaxValue);

                t =
                    Time.realtimeSinceStartup;
            }
        }
    }

    IEnumerator CheckConnect()
    {
        float t =
            Time.realtimeSinceStartup;

        while (GameManager.Instance?.isOnline == true)
        {
            yield return null;

            if (Time.realtimeSinceStartup - t > 3f)
            {
                if (!OnlineCheck)
                {
                    CloseClient();
                    yield break;
                }

                OnlineCheck = false;

                t =
                    Time.realtimeSinceStartup;
            }
        }
    }

    IEnumerator WaitLog()
    {
        float t =
            Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup - t <= 15f &&
               GameManager.Instance?.isOnline == false &&
               WaitConnect != null &&
               !connectionResponseReceived)
        {
            yield return null;
        }

        if (GameManager.Instance?.isOnline == true ||
            connectionResponseReceived)
        {
            WaitConnect = null;
            yield break;
        }

        WaitConnect = null;

        CloseClient();

        UIManager.Instance?.LogPanel?.Confirm();
    }

    void ConnectSuccess()
    {
        if (GameManager.Instance == null ||
            GameManager.Instance.isOnline)
            return;

        if (WaitConnect != null)
        {
            StopCoroutine(WaitConnect);
            WaitConnect = null;
        }

        needLog2 = true;

        Application.runInBackground = true;

        GameManager.Instance.isOnline = true;

        OnlineCheck = true;

        StartCoroutine(CheckConnect());
        StartCoroutine(SendHeartbeat());

        UIManager.Instance?.ConnectSuccess();
    }

    public void CloseClient()
    {
        var manager = NetworkManager.Singleton;

        if (manager != null &&
            manager.IsListening)
        {
            SendMsg(
                "",
                0,
                2);

            IsHandOver = true;

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

    void ConnectOver()
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
                if (WaitConnect != null)
                {
                    StopCoroutine(WaitConnect);
                    WaitConnect = null;
                }

                return;
            }

            gm.isOnline = false;

            if (WaitConnect != null)
            {
                StopCoroutine(WaitConnect);
                WaitConnect = null;
            }

            if (LVManager.Instance?.InGame == true)
            {
                if (IsHandOver)
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

    void ConnectOverDone()
    {
        ReConnectCode = 0;
        connectionResponseReceived = false;

        pendingOnlinePlayerInfo = null;
        hasPendingOnlinePlayerInfo = false;

        pendingPlayerListDone = false;
        pendingBattlePlayerListDone = false;

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

        if (UIManager.Instance?.LogPanel != null)
        {
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
                        UIManager.Instance?.JoinGame?
                            .gameObject
                            .SetActive(true));
            }
        }
    }

    public void SendChatMsg(string msg)
    {
        SendMsg(
            msg,
            2,
            0);
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

    public void ChangeMap(PlayerMap map)
    {
        SendMsg(
            JsonUtility.ToJson(map),
            1,
            3);
    }

    public void SelectCard(SelectCard card)
    {
        SendMsg(
            JsonUtility.ToJson(card),
            1,
            4);
    }

    public void SelectPrepare(SelectPrepare prepare)
    {
        SendMsg(
            JsonUtility.ToJson(prepare),
            1,
            5);
    }

    public void ApplyTool(ToolApply apply)
    {
        SendMsg(
            JsonUtility.ToJson(apply),
            1,
            1);
    }

    public void UpdateCD(int cardID, bool Ok)
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

    public void ClickedSun(ClickedSun sun)
    {
        SendMsg(
            JsonUtility.ToJson(sun),
            1,
            2);
    }

    public void ApplyPlacePlant(PlantSpawn spawn)
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            0);
    }

    public void ApplyPlaceZombie(ZombieSpawnApply spawn)
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            10);
    }

    public void ApplyPlacePreview(PlantPreview spawn)
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            7);
    }

    public void ApplyShovelPreview(ShovelPreview spawn)
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            9);
    }

    public void ApplyZombiePreview(ZombiePreview spawn)
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            11);
    }

    public void ApplyJoinTeam(JoinTeamApply spawn)
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            12);
    }

    public void ApplyJoinSpect(JoinSpecApply spawn)
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            13);
    }

    public void SendSynBag(SynItem syn)
    {
        SendMsg(
            JsonUtility.ToJson(syn),
            1,
            6);
    }

    public void SendSlotMBag(SlotMchBag bag)
    {
        SendMsg(
            JsonUtility.ToJson(bag),
            1,
            14);
    }
}