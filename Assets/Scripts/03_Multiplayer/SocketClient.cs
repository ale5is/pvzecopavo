using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using SocketSave;
using UnityEngine;

public class SocketClient : MonoBehaviour
{
    public static SocketClient Instance;

    const int MaxPacket = 1048576;
    const int BufferSize = 65536;

    Socket clientSocket;
    Coroutine WaitConnect;

    bool needLog;
    bool needLog2;
    bool OnlineCheck;
    bool IsHandOver;

    public TextMesh text;

    int ReConnectCode;

    volatile bool isClosing;
    volatile bool connectOverCalled;
    volatile bool connectionResponseReceived;

    OnlinePlayerInfo pendingOnlinePlayerInfo;
    bool hasPendingOnlinePlayerInfo;
    bool pendingPlayerListDone;
    bool pendingBattlePlayerListDone;

    readonly ConcurrentQueue<byte[]> messageQueue = new();
    readonly object socketLock = new();
    static readonly ConcurrentQueue<Action> UnityMainQueue = new();

    void Awake()
    {
        Instance = this;
        Application.runInBackground = true;
    }

    void Update()
    {
        while (messageQueue.TryDequeue(out var msg))
        {
            try
            {
                ProcessMessage(msg);
            }
            catch (Exception e)
            {
                Debug.LogError("Message processing error: " + e);
            }
        }

        ProcessPendingOnlinePlayerInfo();
    }

    void LateUpdate()
    {
        while (UnityMainQueue.TryDequeue(out var action))
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }

    Socket GetSocket()
    {
        lock (socketLock)
            return clientSocket;
    }

    void SetSocket(Socket socket)
    {
        lock (socketLock)
            clientSocket = socket;
    }

    void SafeClose(Socket socket)
    {
        if (socket == null)
            return;

        try
        {
            socket.Shutdown(SocketShutdown.Both);
        }
        catch
        {
        }

        try
        {
            socket.Close();
        }
        catch
        {
        }

        try
        {
            socket.Dispose();
        }
        catch
        {
        }
    }

    void UnityMain(Action action)
    {
        if (action != null)
            UnityMainQueue.Enqueue(action);
    }

    void CloseSocket()
    {
        lock (socketLock)
        {
            if (isClosing)
                return;

            isClosing = true;
        }

        var socket = GetSocket();
        SetSocket(null);
        SafeClose(socket);
        ConnectOver();
        isClosing = false;
    }

    public void JoinGame(IPAddress ip, int port, string passWord)
    {
        var gm = GameManager.Instance;

        if (gm == null || WaitConnect != null || gm.isOnline)
            return;

        SafeClose(GetSocket());
        SetSocket(null);

        isClosing = false;
        connectOverCalled = false;
        connectionResponseReceived = false;

        pendingOnlinePlayerInfo = null;
        hasPendingOnlinePlayerInfo = false;
        pendingPlayerListDone = false;
        pendingBattlePlayerListDone = false;

        WaitConnect = StartCoroutine(WaitLog());

        var socket = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp)
        {
            NoDelay = true,
            SendBufferSize = BufferSize,
            ReceiveBufferSize = BufferSize
        };

        SetSocket(socket);

        if (gm.LocalPlayerSave == null)
        {
            SetSocket(null);
            SafeClose(socket);
            WaitConnect = null;
            return;
        }

        var info = new PlayerInfo
        {
            Name = gm.LocalPlayerSave.playerName,
            VersionCode = gm.VersionCode,
            CmdEnable = gm.LocalPlayerSave.CmdEnable,
            ReCntCode = ReConnectCode,
            Password = passWord
        };

        needLog = true;
        needLog2 = false;
        IsHandOver = false;

        ThreadPool.QueueUserWorkItem(
            _ => ConnectAndReceive(socket, ip, port, info));
    }

    void ConnectAndReceive(
        Socket socket,
        IPAddress ip,
        int port,
        PlayerInfo info)
    {
        try
        {
            socket.Connect(new IPEndPoint(ip, port));

            if (isClosing || GetSocket() != socket)
                return;

            SendMsg(JsonUtility.ToJson(info), 0, 1, socket);
            ReceiveLoop(socket);
        }
        catch (SocketException e)
        {
            if (!isClosing)
                UnityMain(() => Debug.Log("Error de conexión: " + e));
        }
        catch (ObjectDisposedException)
        {
            if (!isClosing)
                UnityMain(() => Debug.Log("El socket se ha cerrado."));
        }
        catch (Exception e)
        {
            if (!isClosing)
                UnityMain(() => Debug.LogError(e));
        }
    }

    void ReceiveLoop(Socket socket)
    {
        var buffer = new byte[BufferSize];
        var pending = new byte[MaxPacket + BufferSize + 4];
        int pendingCount = 0;

        try
        {
            while (!isClosing && GetSocket() == socket)
            {
                int received;

                try
                {
                    received = socket.Receive(buffer);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException e)
                {
                    if (!isClosing)
                        UnityMain(() =>
                            Debug.LogError(
                                new Exception("Conexión perdida", e)));

                    break;
                }

                if (received <= 0)
                    break;

                if (pendingCount + received > pending.Length)
                {
                    UnityMain(() =>
                        Debug.Log("Los datos recibidos son demasiado grandes."));

                    break;
                }

                Buffer.BlockCopy(
                    buffer,
                    0,
                    pending,
                    pendingCount,
                    received);

                pendingCount += received;

                int offset = 0;

                while (pendingCount - offset >= 4)
                {
                    int len = BitConverter.ToInt32(pending, offset);

                    if (len < 2 || len > MaxPacket)
                    {
                        UnityMain(() =>
                            Debug.Log(
                                "Se recibió un paquete de datos no válido."));

                        return;
                    }

                    int total = len + 4;

                    if (pendingCount - offset < total)
                        break;

                    var packet = new byte[len];

                    Buffer.BlockCopy(
                        pending,
                        offset + 4,
                        packet,
                        0,
                        len);

                    if (packet[0] == 0 &&
                        (packet[1] == 1 ||
                         packet[1] == 2 ||
                         packet[1] == 3 ||
                         packet[1] == byte.MaxValue))
                    {
                        connectionResponseReceived = true;
                    }

                    messageQueue.Enqueue(packet);
                    offset += total;
                }

                if (offset > 0)
                {
                    int left = pendingCount - offset;

                    if (left > 0)
                    {
                        Buffer.BlockCopy(
                            pending,
                            offset,
                            pending,
                            0,
                            left);
                    }

                    pendingCount = left;
                }
            }
        }
        catch (Exception e)
        {
            if (!isClosing)
            {
                UnityMain(() =>
                    Debug.LogError(
                        new Exception("Conexión perdida", e)));
            }
        }
        finally
        {
            if (!isClosing && GetSocket() == socket)
                UnityMain(CloseSocket);
        }
    }

    void ProcessMessage(byte[] data)
    {
        if (data == null || data.Length < 2)
            return;

        string s = data.Length > 2
            ? Encoding.UTF8.GetString(data, 2, data.Length - 2)
            : "";

        if (data[0] == 0)
        {
            ProcessConnection(data[1], s);
        }
        else if (data[0] == 1)
        {
            ProcessGame(data[1], s);
        }
        else if (data[0] == 2)
        {
            ProcessSpawn(data[1], s);
        }
        else if (data[0] == 3)
        {
            ProcessWorld(data[1], s);
        }
        else if (data[0] == 4)
        {
            ProcessCommand(data[1], s);
        }
    }

    void ProcessConnection(byte type, string s)
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

            var info = JsonUtility.FromJson<ConnectInfo>(s);

            if (info == null)
                return;

            if (UIManager.Instance?.LogPanel == null)
            {
                CloseSocket();
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
                            UIManager.Instance.JoinGame.gameObject.SetActive(true);
                        }
                    });
            }

            CloseSocket();
            return;
        }

        if (type == 2)
        {
            var info = JsonUtility.FromJson<OnlinePlayerInfo>(s);

            if (info == null || GameManager.Instance == null)
                return;

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
            var info = JsonUtility.FromJson<ReConnectInfo>(s);

            if (info == null || GameManager.Instance == null)
                return;

            ConnectSuccess();

            if (ReConnect.Instance == null)
            {
                CloseSocket();
                return;
            }

            if (info.isWait)
            {
                ReConnect.Instance.OpenInit(false);
            }
            else
            {
                ReConnect.Instance.OverClose();
            }

            ReConnect.Instance.LoadPlayerList(
                info.names ?? new List<string>());
        }
    }

    void ProcessPendingOnlinePlayerInfo()
    {
        if (!hasPendingOnlinePlayerInfo ||
            pendingOnlinePlayerInfo == null)
            return;

        var info = pendingOnlinePlayerInfo;
        var players = info.players ?? new List<PlayerInfo>();

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
            var load = JsonUtility.FromJson<LoadLVBag>(s);

            if (load == null || LVManager.Instance == null)
                return;

            ReConnectCode = load.ReCntCode;

            if (load.LoadType == 0)
            {
                LVManager.Instance.StartGame(load, -1);
            }
            else if (load.LoadType == 1)
            {
                LVManager.Instance.ReStartGame();
            }
            else if (load.LoadType == 2)
            {
                LVManager.Instance.QuitBattleGame();
            }
        }
        else if (type == 1)
        {
            SeedChooser.Instance?.StartRunLv(true);
            ZombieChooser.Instance?.StartRunLv(true);
        }
        else if (type == 2)
        {
            if (LVManager.Instance != null)
            {
                LVManager.Instance.ClientShowBigWave(
                    JsonUtility.FromJson<WaveComing>(s));
            }
        }
        else if (type == 3)
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.ClientUpdateSunNum(
                    JsonUtility.FromJson<SunNumBag>(s));
            }
        }
        else if (type == 4)
        {
            SynItem(JsonUtility.FromJson<SynItem>(s));
        }
        else if (type == 5)
        {
            var map = JsonUtility.FromJson<PlayerMap>(s);

            if (map != null)
            {
                BattlePlayerList.Instance?.UpdateMapSprite(
                    map.PlayerName,
                    map.Pos);
            }
        }
        else if (type == 6)
        {
            var card = JsonUtility.FromJson<SelectCard>(s);
            var save = GameManager.Instance?.LocalPlayerSave;

            if (card != null &&
                save != null &&
                card.PlayerName != save.playerName)
            {
                if (card.isBack)
                {
                    BattlePlayerList.Instance?.CancelCard(
                        card.PlayerName,
                        card.cardId
                    );
                }
                else
                {
                    BattlePlayerList.Instance?.SelectCard(
                        card.PlayerName,
                        card.plantType,
                        card.zombieType,
                        card.noAnim,
                        card.cardId
                    );
                }
            }
        }
        else if (type == 7)
        {
            var prepare = JsonUtility.FromJson<SelectPrepare>(s);

            if (prepare != null)
            {
                BattlePlayerList.Instance?.UpdateState(
                    prepare.PlayerName,
                    prepare.isPrepare);
            }
        }
        else if (type == 8)
        {
            var over = JsonUtility.FromJson<GameOver>(s);

            if (over != null &&
                LV.Instance != null &&
                LVManager.Instance != null)
            {
                if (LV.Instance.CurrLVType == LVType.PvP)
                {
                    LVManager.Instance.PvPGameOver(
                        over.pos,
                        over.isRedFail);
                }
                else
                {
                    LVManager.Instance.ZombieGameOver(over.pos);
                }
            }
        }
        else if (type == 9)
        {
            PvPSelector.Instance?.ClientSynTeam(
                JsonUtility.FromJson<PvPTeamList>(s));
        }
        else if (type == 10)
        {
            PvPSelector.Instance?.ClientSynMode(
                JsonUtility.FromJson<PvPModeSyn>(s));
        }
        else if (type == 11)
        {
            var list = JsonUtility.FromJson<SpectList>(s);

            if (list != null)
            {
                SpectatorList.Instance?.ClientSynList(
                    list.names ?? new List<string>());
            }
        }
        else if (type == 12)
        {
            var add = JsonUtility.FromJson<AddCardBag>(s);

            if (add != null)
            {
                SeedBank.Instance?.AddCards(
                    add.CardTypes,
                    true);
            }
        }
        else if (type == 13)
        {
            FlagMeter.Instance?.ClientSyn(
                JsonUtility.FromJson<FlagMeterSyn>(s));
        }
        else if (type == 14)
        {
            Timetable.Instance?.ClientSyn(
                JsonUtility.FromJson<TimetableSyn>(s));
        }
    }

    void ProcessSpawn(byte type, string s)
    {
        if (type == 0)
        {
            var plant = JsonUtility.FromJson<PlantSpawn>(s);

            if (plant == null ||
                PlantManager.Instance == null ||
                SeedBank.Instance == null ||
                MapManager.Instance == null)
            {
                return;
            }

            var obj = PlantManager.Instance.GetNewPlant(
                plant.plantType);

            if (obj == null)
                return;

            if (plant.SPcode == 2)
            {
                obj.InitForCreate(false, null, false);
            }

            ReversePvP(
                ref plant.GridPos,
                plant.PlacePlayer);

            obj.OnlineId = plant.OnlineId;

            var grid = MapManager.Instance.GetGridByWorldPos(
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
        }
        else if (type == 1)
        {
            SkyManager.Instance?.ClientSpawnSun(
                JsonUtility.FromJson<SunSpawn>(s));
        }
        else if (type == 2)
        {
            SkyManager.Instance?.OnlineCollectSun(
                JsonUtility.FromJson<ClickedSun>(s));
        }
        else if (type == 3)
        {
            BattlePlayerList.Instance?.PreviewPlant(
                JsonUtility.FromJson<PlantPreview>(s));
        }
        else if (type == 4)
        {
            var cd = JsonUtility.FromJson<UpdateCardCD>(s);

            if (cd == null)
                return;

            var save = GameManager.Instance?.LocalPlayerSave;

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
        }
        else if (type == 5)
        {
            var shovel = JsonUtility.FromJson<ShovelPreview>(s);

            if (shovel != null)
            {
                BattlePlayerList.Instance?.PreviewShovel(
                    shovel.PlayerName,
                    shovel.GridPos,
                    shovel.isShow);
            }
        }
        else if (type == 6)
        {
            var tool = JsonUtility.FromJson<ToolApply>(s);

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
        }
        else if (type == 1)
        {
            var grave = JsonUtility.FromJson<GraveStoneSpawn>(s);

            var ggrid = grave != null
                ? MapManager.Instance?.GetGridByWorldPos(grave.MapPos)
                : null;

            if (ggrid != null)
            {
                ggrid.ClientSynGrave(
                    grave.Type,
                    grave.isHave);
            }
        }
        else if (type == 2)
        {
            CreatePuddle(
                JsonUtility.FromJson<PuddleSpawn>(s));
        }
        else if (type == 3)
        {
            var light = JsonUtility.FromJson<LightingSpawn>(s);

            var lgrid = light != null
                ? MapManager.Instance?.GetGridByWorldPos(light.Pos)
                : null;

            if (lgrid != null)
            {
                SkyManager.Instance?.ClientLightningThis(lgrid);
            }
        }
        else if (type == 4)
        {
            var map = JsonUtility.FromJson<SynMap>(s);

            if (map != null)
            {
                MapManager.Instance?
                    .GetCurrMap(map.mapPos)?
                    .SynMap(map);
            }
        }
        else if (type == 5)
        {
            BattlePlayerList.Instance?.PreviewZombie(
                JsonUtility.FromJson<ZombiePreview>(s));
        }
        else if (type == 6)
        {
            MapManager.Instance?.ClientCreatePortal(
                JsonUtility.FromJson<PortalSpawn>(s));
        }
        else if (type == 7)
        {
            SynGrid(
                JsonUtility.FromJson<SynGrid>(s));
        }
        else if (type == 8)
        {
            var booty = JsonUtility.FromJson<SynBooty>(s);

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
                    LVManager.Instance.OnlyBooty?.CollectBooty();
                }
            }
        }
        else if (type == 9)
        {
            LvItemManager.Instance?.ClientCreateVase(
                JsonUtility.FromJson<VaseSpawn>(s));
        }
        else if (type == 10)
        {
            SeedBank.Instance?.ClientSpawnCard(
                JsonUtility.FromJson<CardSpawn>(s));
        }
        else if (type == 11)
        {
            LvItemManager.Instance?.SpawnMelt(
                JsonUtility.FromJson<MeltSpawn>(s));
        }
        else if (type == 12)
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
        {
            return;
        }

        var grids = new List<Grid>();

        foreach (var pos in spawn.MapPos ?? new List<Vector2>())
        {
            var grid = MapManager.Instance.GetGridByWorldPos(pos);

            if (grid != null)
                grids.Add(grid);
        }

        var puddle = Instantiate(
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
        {
            return;
        }

        if (LV.Instance.CurrLVType == LVType.PvP &&
            PvPSelector.Instance != null &&
            GameManager.Instance != null &&
            !PvPSelector.Instance.IsSameTeam(
                GameManager.Instance.HostName))
        {
            data.GridPos = MyTool.ReverseX(data.GridPos);
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
        }
        else if (type == 1)
        {
            ChatInput.Instance?.AddMessage(
                s,
                new Color32(255, 255, 0, 255));
        }
        else if (type == 2)
        {
            var chat = JsonUtility.FromJson<PrivateChatMsg>(s);

            if (chat != null)
            {
                ChatInput.Instance?.AddMessage(
                    "Jugador" +
                    chat.PlayerName +
                    "Susurro:" +
                    chat.content,
                    new Color32(123, 123, 123, 255));
            }
        }
        else if (type == 3)
        {
            var cmd = JsonUtility.FromJson<CommandBag>(s);

            if (cmd == null)
                return;

            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.PlantInvincible = cmd.Pinv;
            }

            if (ZombieManager.Instance != null)
            {
                ZombieManager.Instance.ZombieInvincible = cmd.Zinv;
                ZombieManager.Instance.ZombieDontMove = cmd.ZomStop;
            }

            if (SkyManager.Instance != null)
            {
                SkyManager.Instance.DayLightCycle = cmd.DLiCy;
            }

            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.SunInfinite = cmd.SnInf;
            }

            if (SeedBank.Instance != null)
            {
                SeedBank.Instance.isNoCD = cmd.CdCle;
            }

            if (LvItemManager.Instance != null)
            {
                LvItemManager.Instance.VaseAlwaysLight = cmd.VaseXray;
            }
        }
        else if (type == 4)
        {
            SkyManager.Instance?.ClientSynWeather(
                JsonUtility.FromJson<WeatherChange>(s));
        }
        else if (type == 5)
        {
            var time = JsonUtility.FromJson<TimeCmd>(s);

            if (time == null ||
                SkyManager.Instance == null)
            {
                return;
            }

            if (time.time >= 10000)
            {
                SkyManager.Instance.DirectSetTime(
                    time.time - 10000,
                    true);
            }
            else
            {
                SkyManager.Instance.Time = time.time;
            }
        }
        else if (type == 6)
        {
            var achievement =
                JsonUtility.FromJson<GetAcvment>(s);

            if (achievement != null)
            {
                AcvmentManager.Instance?.GetAchievement(
                    achievement.acv);
            }
        }
        else if (type == 99)
        {
            Debug.Log(s);
        }
    }

    void SendMsg(
        string content,
        byte type1,
        byte type2,
        Socket socket = null)
    {
        var target = socket ?? GetSocket();

        if (target == null || isClosing)
            return;

        content ??= "";

        int count = Encoding.UTF8.GetByteCount(content);
        int len = count + 2;

        if (len > MaxPacket)
            return;

        var buffer = new byte[count + 6];

        Buffer.BlockCopy(
            BitConverter.GetBytes(len),
            0,
            buffer,
            0,
            4);

        buffer[4] = type1;
        buffer[5] = type2;

        if (count > 0)
        {
            Encoding.UTF8.GetBytes(
                content,
                0,
                content.Length,
                buffer,
                6);
        }

        try
        {
            int sent = 0;

            while (sent < buffer.Length)
            {
                int n = target.Send(
                    buffer,
                    sent,
                    buffer.Length - sent,
                    SocketFlags.None);

                if (n <= 0)
                    throw new SocketException();

                sent += n;
            }
        }
        catch
        {
            if (!isClosing)
                CloseSocket();
        }
    }

    IEnumerator SendHeartbeat()
    {
        float t = Time.realtimeSinceStartup;

        while (GameManager.Instance?.isOnline == true)
        {
            yield return null;

            if (Time.realtimeSinceStartup - t > .5f)
            {
                SendMsg(
                    "",
                    0,
                    byte.MaxValue);

                t = Time.realtimeSinceStartup;
            }
        }
    }

    IEnumerator CheckConnect()
    {
        float t = Time.realtimeSinceStartup;

        while (GameManager.Instance?.isOnline == true)
        {
            yield return null;

            if (Time.realtimeSinceStartup - t > 3f)
            {
                if (!OnlineCheck)
                {
                    CloseSocket();
                    yield break;
                }

                OnlineCheck = false;
                t = Time.realtimeSinceStartup;
            }
        }
    }

    IEnumerator WaitLog()
    {
        float t = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup - t <= 3f &&
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
        CloseSocket();
        UIManager.Instance?.LogPanel?.Confirm();
    }

    void ConnectSuccess()
    {
        if (GameManager.Instance == null ||
            GameManager.Instance.isOnline)
        {
            return;
        }

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
        SendMsg("", 0, 2);

        IsHandOver = true;
        needLog = false;
        needLog2 = false;

        CloseSocket();
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
            var gm = GameManager.Instance;

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
            var plants = PlantManager.Instance?.plants;

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
                var zombies = new List<ZombieBase>(
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
            var puddles = MapManager.Instance?.puddles;

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
            var portals = MapManager.Instance?.portalCs;

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
}