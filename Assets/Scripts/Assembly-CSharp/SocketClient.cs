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

    const int MaxPacket = 1048576, BufferSize = 65536;
    Socket clientSocket;
    Coroutine WaitConnect;
    bool needLog, needLog2, OnlineCheck, IsHandOver;
    public TextMesh text;
    int ReConnectCode;
    volatile bool isClosing, connectOverCalled;
    readonly ConcurrentQueue<byte[]> messageQueue = new ConcurrentQueue<byte[]>();
    readonly object socketLock = new object();

    void Awake()
    {
        Instance = this;
        Application.runInBackground = true;
    }

    void Update()
    {
        while (messageQueue.TryDequeue(out byte[] msg))
            try { ProcessMessage(msg); }
            catch (Exception e) { Debug.LogError("Message processing error: " + e); }
    }

    Socket GetSocket()
    {
        lock (socketLock) return clientSocket;
    }

    void SetSocket(Socket socket)
    {
        lock (socketLock) clientSocket = socket;
    }

    void SafeClose(Socket socket)
    {
        if (socket == null) return;
        try { socket.Shutdown(SocketShutdown.Both); } catch { }
        try { socket.Close(); } catch { }
        try { socket.Dispose(); } catch { }
    }

    void CloseSocket()
    {
        lock (socketLock)
        {
            if (isClosing) return;
            isClosing = true;
        }

        Socket socket = GetSocket();
        SetSocket(null);
        SafeClose(socket);
        ConnectOver();
        isClosing = false;
    }

    public void JoinGame(IPAddress ip, int port, string passWord)
    {
        if (WaitConnect != null || GameManager.Instance.isOnline) return;

        Socket old = GetSocket();
        SetSocket(null);
        SafeClose(old);

        isClosing = false;
        connectOverCalled = false;
        WaitConnect = StartCoroutine(WaitLog());

        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
            SendBufferSize = BufferSize,
            ReceiveBufferSize = BufferSize
        };

        SetSocket(socket);

        PlayerInfo info = new PlayerInfo
        {
            Name = GameManager.Instance.LocalPlayerSave.playerName,
            VersionCode = GameManager.Instance.VersionCode,
            CmdEnable = GameManager.Instance.LocalPlayerSave.CmdEnable,
            ReCntCode = ReConnectCode,
            Password = passWord
        };

        needLog = true;
        needLog2 = false;
        IsHandOver = false;

        ThreadPool.QueueUserWorkItem(_ => ConnectAndReceive(socket, ip, port, info));
    }

    void ConnectAndReceive(Socket socket, IPAddress ip, int port, PlayerInfo info)
    {
        try
        {
            socket.Connect(new IPEndPoint(ip, port));
            if (isClosing || GetSocket() != socket) return;

            SendMsg(JsonUtility.ToJson(info), 0, 1, socket);
            ReceiveLoop(socket);
        }
        catch (SocketException e)
        {
            if (!isClosing) EnqueueLog("Error de conexión: " + e);
        }
        catch (ObjectDisposedException)
        {
            if (!isClosing) EnqueueLog("El socket se ha cerrado.");
        }
        catch (Exception e)
        {
            if (!isClosing) EnqueueError(e);
        }
    }

    void EnqueueLog(string msg)
    {
        UnityMain(() => Debug.Log(msg));
    }

    void EnqueueError(Exception e)
    {
        UnityMain(() => Debug.LogError(e));
    }

    void UnityMain(Action action)
    {
        if (action == null) return;
        UnityMainQueue.Enqueue(action);
    }

    static readonly ConcurrentQueue<Action> UnityMainQueue = new ConcurrentQueue<Action>();

    void LateUpdate()
    {
        while (UnityMainQueue.TryDequeue(out Action action))
            try { action(); } catch (Exception e) { Debug.LogError(e); }
    }

    void ReceiveLoop(Socket socket)
    {
        byte[] buffer = new byte[BufferSize];
        byte[] pending = new byte[MaxPacket + BufferSize + 4];
        int pendingCount = 0;

        try
        {
            while (!isClosing && GetSocket() == socket)
            {
                int received;

                try
                {
                    received = socket.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException e)
                {
                    if (!isClosing) EnqueueError(new Exception("Conexión perdida", e));
                    break;
                }

                if (received <= 0) break;
                if (pendingCount + received > pending.Length)
                {
                    EnqueueLog("Los datos recibidos son demasiado grandes.");
                    break;
                }

                Buffer.BlockCopy(buffer, 0, pending, pendingCount, received);
                pendingCount += received;

                int offset = 0;

                while (pendingCount - offset >= 4)
                {
                    int len =
                        pending[offset] |
                        pending[offset + 1] << 8 |
                        pending[offset + 2] << 16 |
                        pending[offset + 3] << 24;

                    if (len < 2 || len > MaxPacket)
                    {
                        EnqueueLog("Se recibió un paquete de datos no válido.");
                        return;
                    }

                    int total = len + 4;
                    if (pendingCount - offset < total) break;

                    byte[] packet = new byte[len];
                    Buffer.BlockCopy(pending, offset + 4, packet, 0, len);
                    messageQueue.Enqueue(packet);
                    offset += total;
                }

                if (offset > 0)
                {
                    int left = pendingCount - offset;
                    if (left > 0) Buffer.BlockCopy(pending, offset, pending, 0, left);
                    pendingCount = left;
                }
            }
        }
        catch (ObjectDisposedException) { }
        catch (SocketException e)
        {
            if (!isClosing) EnqueueError(new Exception("Conexión perdida", e));
        }
        catch (Exception e)
        {
            if (!isClosing) EnqueueError(new Exception("Conexión perdida", e));
        }
        finally
        {
            if (!isClosing && GetSocket() == socket)
                UnityMain(CloseSocket);
        }
    }

    void ProcessMessage(byte[] data)
    {
        if (data == null || data.Length < 2) return;

        byte a = data[0], b = data[1];
        string s = data.Length > 2 ? Encoding.UTF8.GetString(data, 2, data.Length - 2) : "";

        if (a == 0) ProcessConnection(b, s);
        else if (a == 1) ProcessGame(b, s);
        else if (a == 2) ProcessSpawn(b, s);
        else if (a == 3) ProcessWorld(b, s);
        else if (a == 4) ProcessCommand(b, s);
    }

    void ProcessConnection(byte b, string s)
    {
        if (b == 0) { Debug.Log("0-0" + s); return; }

        if (b == 1)
        {
            IsHandOver = true;
            needLog = false;
            ConnectInfo info = JsonUtility.FromJson<ConnectInfo>(s);

            if (LVManager.Instance.InGame)
                UIManager.Instance.LogPanel.DisplayLog("Por favor, inténtelo de nuevo.", null);
            else
                UIManager.Instance.LogPanel.DisplayLog(info.msg, () =>
                {
                    if (!GameManager.Instance.isOnline)
                        UIManager.Instance.JoinGame.gameObject.SetActive(true);
                });

            CloseSocket();
        }
        else if (b == 2)
        {
            OnlinePlayerInfo info = JsonUtility.FromJson<OnlinePlayerInfo>(s);
            ConnectSuccess();
            PlayerList.Instance.UpdatePlayerList(info.HostPlayer, info.players);
            BattlePlayerList.Instance.UpdatePlayerList(info.HostPlayer, info.players);
        }
        else if (b == 3)
        {
            ConnectSuccess();
            ReConnectInfo info = JsonUtility.FromJson<ReConnectInfo>(s);

            if (info.isWait) ReConnect.Instance.OpenInit(false);
            else ReConnect.Instance.OverClose();

            ReConnect.Instance.LoadPlayerList(info.names);
        }
        else if (b == byte.MaxValue)
            OnlineCheck = true;
    }

    void ProcessGame(byte b, string s)
    {
        if (b == 0)
        {
            LoadLVBag bag = JsonUtility.FromJson<LoadLVBag>(s);
            ReConnectCode = bag.ReCntCode;

            if (bag.LoadType == 0) LVManager.Instance.StartGame(bag, -1);
            else if (bag.LoadType == 1) LVManager.Instance.ReStartGame();
            else if (bag.LoadType == 2) LVManager.Instance.QuitBattleGame();
        }
        else if (b == 1)
        {
            SeedChooser.Instance.StartRunLv(true);
            ZombieChooser.Instance.StartRunLv(true);
        }
        else if (b == 2)
            LVManager.Instance.ClientShowBigWave(JsonUtility.FromJson<WaveComing>(s));
        else if (b == 3)
            PlayerManager.Instance.ClientUpdateSunNum(JsonUtility.FromJson<SunNumBag>(s));
        else if (b == 4)
            SynItem(JsonUtility.FromJson<SynItem>(s));
        else if (b == 5)
        {
            PlayerMap m = JsonUtility.FromJson<PlayerMap>(s);
            BattlePlayerList.Instance.UpdateMapSprite(m.PlayerName, m.Pos);
        }
        else if (b == 6)
        {
            SelectCard c = JsonUtility.FromJson<SelectCard>(s);
            if (c.PlayerName != GameManager.Instance.LocalPlayerSave.playerName)
            {
                if (c.isBack) BattlePlayerList.Instance.CancelCard(c.PlayerName, c.cardId);
                else BattlePlayerList.Instance.SelectCard(c.PlayerName, c.plantType, c.zombieType, c.noAnim);
            }
        }
        else if (b == 7)
        {
            SelectPrepare p = JsonUtility.FromJson<SelectPrepare>(s);
            BattlePlayerList.Instance.UpdateState(p.PlayerName, p.isPrepare);
        }
        else if (b == 8)
        {
            GameOver g = JsonUtility.FromJson<GameOver>(s);
            if (LV.Instance.CurrLVType == LVType.PvP) LVManager.Instance.PvPGameOver(g.pos, g.isRedFail);
            else LVManager.Instance.ZombieGameOver(g.pos);
        }
        else if (b == 9)
            PvPSelector.Instance.ClientSynTeam(JsonUtility.FromJson<PvPTeamList>(s));
        else if (b == 10)
            PvPSelector.Instance.ClientSynMode(JsonUtility.FromJson<PvPModeSyn>(s));
        else if (b == 11)
            SpectatorList.Instance.ClientSynList(JsonUtility.FromJson<SpectList>(s).names);
        else if (b == 12)
        {
            AddCardBag bag = JsonUtility.FromJson<AddCardBag>(s);
            SeedBank.Instance.AddCards(bag.CardTypes, true);
        }
        else if (b == 13)
            FlagMeter.Instance.ClientSyn(JsonUtility.FromJson<FlagMeterSyn>(s));
        else if (b == 14)
            Timetable.Instance.ClientSyn(JsonUtility.FromJson<TimetableSyn>(s));
    }

    void ProcessSpawn(byte b, string s)
    {
        if (b == 0)
        {
            PlantSpawn p = JsonUtility.FromJson<PlantSpawn>(s);
            PlantBase plant = PlantManager.Instance.GetNewPlant(p.plantType);

            if (p.SPcode == 2) plant.InitForCreate(false, null, false);

            if (LV.Instance.CurrLVType == LVType.PvP &&
                !PvPSelector.Instance.IsSameTeam(p.PlacePlayer))
                p.GridPos = new Vector2(-p.GridPos.x, p.GridPos.y);

            plant.OnlineId = p.OnlineId;

            SeedBank.Instance.PlantConfirm(
                plant,
                MapManager.Instance.GetGridByWorldPos(p.GridPos),
                -1,
                p.SPcode,
                p.PlacePlayer
            );
        }
        else if (b == 1)
            SkyManager.Instance.ClientSpawnSun(JsonUtility.FromJson<SunSpawn>(s));
        else if (b == 2)
            SkyManager.Instance.OnlineCollectSun(JsonUtility.FromJson<ClickedSun>(s));
        else if (b == 3)
            BattlePlayerList.Instance.PreviewPlant(JsonUtility.FromJson<PlantPreview>(s));
        else if (b == 4)
        {
            UpdateCardCD c = JsonUtility.FromJson<UpdateCardCD>(s);

            if (c.name == GameManager.Instance.LocalPlayerSave.playerName)
            {
                if (c.OK) SeedBank.Instance.PlantFailClearCD(c.CardId);
            }
            else
                BattlePlayerList.Instance.UpdateCardCD(c.name, c.CardId, c.OK);
        }
        else if (b == 5)
        {
            ShovelPreview p = JsonUtility.FromJson<ShovelPreview>(s);
            BattlePlayerList.Instance.PreviewShovel(p.PlayerName, p.GridPos, p.isShow);
        }
        else if (b == 6)
        {
            ToolApply t = JsonUtility.FromJson<ToolApply>(s);
            BattlePlayerList.Instance.PlayShovelAnimation(t.GridPos, t.Sound, t.User);
        }
    }

    void ProcessWorld(byte b, string s)
    {
        if (b == 0)
            ZombieManager.Instance.UpdateZombie(JsonUtility.FromJson<ZombieSpawn>(s));
        else if (b == 1)
        {
            GraveStoneSpawn g = JsonUtility.FromJson<GraveStoneSpawn>(s);
            MapManager.Instance.GetGridByWorldPos(g.MapPos).ClientSynGrave(g.Type, g.isHave);
        }
        else if (b == 2)
        {
            PuddleSpawn p = JsonUtility.FromJson<PuddleSpawn>(s);
            List<Grid> grids = new List<Grid>(p.MapPos.Count);

            for (int i = 0; i < p.MapPos.Count; i++)
                grids.Add(MapManager.Instance.GetGridByWorldPos(p.MapPos[i]));

            Puddle puddle = UnityEngine.Object.Instantiate(
                GameManager.Instance.GameConf.Puddle
            ).GetComponent<Puddle>();

            puddle.CreateInit(grids, p.InitPos, p.OnlineId);
            MapManager.Instance.puddles.Add(puddle);
        }
        else if (b == 3)
        {
            LightingSpawn l = JsonUtility.FromJson<LightingSpawn>(s);
            SkyManager.Instance.ClientLightningThis(
                MapManager.Instance.GetGridByWorldPos(l.Pos)
            );
        }
        else if (b == 4)
        {
            SynMap m = JsonUtility.FromJson<SynMap>(s);
            MapManager.Instance.GetCurrMap(m.mapPos).SynMap(m);
        }
        else if (b == 5)
            BattlePlayerList.Instance.PreviewZombie(JsonUtility.FromJson<ZombiePreview>(s));
        else if (b == 6)
            MapManager.Instance.ClientCreatePortal(JsonUtility.FromJson<PortalSpawn>(s));
        else if (b == 7)
        {
            SynGrid g = JsonUtility.FromJson<SynGrid>(s);

            if (LV.Instance.CurrLVType == LVType.PvP &&
                !PvPSelector.Instance.IsSameTeam(GameManager.Instance.HostName))
                g.GridPos = MyTool.ReverseX(g.GridPos);

            MapManager.Instance.GetGridByWorldPos(g.GridPos).ClientSynState(g);
        }
        else if (b == 8)
        {
            SynBooty booty = JsonUtility.FromJson<SynBooty>(s);

            if (LVManager.Instance.InGame)
            {
                if (booty.isSpawn)
                    LVManager.Instance.SpawnBooty(booty.pos, booty);
                else if (LVManager.Instance.OnlyBooty != null)
                    LVManager.Instance.OnlyBooty.CollectBooty();
            }
        }
        else if (b == 9)
            LvItemManager.Instance.ClientCreateVase(JsonUtility.FromJson<VaseSpawn>(s));
        else if (b == 10)
            SeedBank.Instance.ClientSpawnCard(JsonUtility.FromJson<CardSpawn>(s));
        else if (b == 11)
            LvItemManager.Instance.SpawnMelt(JsonUtility.FromJson<MeltSpawn>(s));
        else if (b == 12)
            LvItemManager.Instance.SpawnFallHail(JsonUtility.FromJson<FallHailSpawn>(s));
    }

    void ProcessCommand(byte b, string s)
    {
        if (b == 0)
            ChatInput.Instance.AddMessage(s);
        else if (b == 1)
            ChatInput.Instance.AddMessage(s, new Color32(255, 255, 0, 255));
        else if (b == 2)
        {
            PrivateChatMsg m = JsonUtility.FromJson<PrivateChatMsg>(s);
            ChatInput.Instance.AddMessage(
                "Jugador" + m.PlayerName + "Susurro:" + m.content,
                new Color32(123, 123, 123, 255)
            );
        }
        else if (b == 3)
        {
            CommandBag c = JsonUtility.FromJson<CommandBag>(s);
            PlantManager.Instance.PlantInvincible = c.Pinv;
            ZombieManager.Instance.ZombieInvincible = c.Zinv;
            SkyManager.Instance.DayLightCycle = c.DLiCy;
            PlayerManager.Instance.SunInfinite = c.SnInf;
            SeedBank.Instance.isNoCD = c.CdCle;
            ZombieManager.Instance.ZombieDontMove = c.ZomStop;
            LvItemManager.Instance.VaseAlwaysLight = c.VaseXray;
        }
        else if (b == 4)
            SkyManager.Instance.ClientSynWeather(JsonUtility.FromJson<WeatherChange>(s));
        else if (b == 5)
        {
            TimeCmd t = JsonUtility.FromJson<TimeCmd>(s);

            if (t.time >= 10000)
            {
                t.time -= 10000;
                SkyManager.Instance.DirectSetTime(t.time, true);
            }
            else
                SkyManager.Instance.Time = t.time;
        }
        else if (b == 6)
            AcvmentManager.Instance.GetAchievement(
                JsonUtility.FromJson<GetAcvment>(s).acv
            );
        else if (b == 99)
            Debug.Log(s);
    }

    void SendMsg(string content, byte type1, byte type2, Socket socket = null)
    {
        Socket target = socket ?? GetSocket();

        if (target == null)
        {
            Debug.LogError(type1 + "/" + type2 + "Enviar desconexión");
            return;
        }

        if (isClosing) return;

        content ??= "";

        try
        {
            int count = Encoding.UTF8.GetByteCount(content);
            int len = count + 2;

            if (len > MaxPacket)
            {
                Debug.LogError("Los datos enviados son demasiado grandes.");
                return;
            }

            byte[] buffer = new byte[count + 6];

            buffer[0] = (byte)len;
            buffer[1] = (byte)(len >> 8);
            buffer[2] = (byte)(len >> 16);
            buffer[3] = (byte)(len >> 24);
            buffer[4] = type1;
            buffer[5] = type2;

            Encoding.UTF8.GetBytes(content, 0, content.Length, buffer, 6);

            int sent = 0;
            while (sent < buffer.Length)
            {
                int n = target.Send(
                    buffer,
                    sent,
                    buffer.Length - sent,
                    SocketFlags.None
                );

                if (n <= 0) throw new SocketException();
                sent += n;
            }
        }
        catch (ObjectDisposedException)
        {
            if (!isClosing) Debug.LogError(type1 + "/" + type2 + "Enviar desconexión");
        }
        catch (SocketException e)
        {
            if (!isClosing)
            {
                Debug.LogError(type1 + "/" + type2 + "Enviar desconexión");
                Debug.LogError(e);
                CloseSocket();
            }
        }
        catch (Exception e)
        {
            if (!isClosing)
            {
                Debug.LogError(type1 + "/" + type2 + "Enviar desconexión");
                Debug.LogError(e);
                CloseSocket();
            }
        }
    }

    IEnumerator SendHeartbeat()
    {
        float t = Time.realtimeSinceStartup;

        while (GameManager.Instance.isOnline)
        {
            yield return null;

            if (Time.realtimeSinceStartup - t > 0.5f)
            {
                SendMsg("", 0, byte.MaxValue);
                t = Time.realtimeSinceStartup;
            }
        }
    }

    IEnumerator CheckConnect()
    {
        float t = Time.realtimeSinceStartup;

        while (GameManager.Instance.isOnline)
        {
            yield return null;

            if (Time.realtimeSinceStartup - t > 3f)
            {
                if (!OnlineCheck)
                {
                    Debug.LogError("Desconexión por falta de señal");
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

        while (
            Time.realtimeSinceStartup - t <= 3f &&
            !GameManager.Instance.isOnline &&
            WaitConnect != null
        )
            yield return null;

        if (GameManager.Instance.isOnline)
        {
            WaitConnect = null;
            yield break;
        }

        Debug.Log("e999");
        WaitConnect = null;
        CloseSocket();

        if (UIManager.Instance != null &&
            UIManager.Instance.LogPanel != null)
            UIManager.Instance.LogPanel.Confirm();
    }

    void ConnectSuccess()
    {
        if (GameManager.Instance.isOnline) return;

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
        UIManager.Instance.ConnectSuccess();
    }

    public void CloseClient()
    {
        SendMsg("", 0, 2);
        IsHandOver = true;
        needLog = needLog2 = false;
        CloseSocket();
    }

    public void ReConnectGiveUp()
    {
        CloseClient();
        ConnectOverDone();
    }

    void ConnectOver()
    {
        if (connectOverCalled) return;
        connectOverCalled = true;

        if (!GameManager.Instance.isOnline)
        {
            if (WaitConnect != null)
            {
                StopCoroutine(WaitConnect);
                WaitConnect = null;
            }

            connectOverCalled = false;
            return;
        }

        Debug.LogError("6666");
        GameManager.Instance.isOnline = false;

        if (WaitConnect != null)
        {
            StopCoroutine(WaitConnect);
            WaitConnect = null;
        }

        if (LVManager.Instance.InGame)
        {
            if (IsHandOver)
            {
                LVManager.Instance.QuitBattleGame();
                ConnectOverDone();
            }
            else
            {
                StopAllCoroutines();
                ReConnect.Instance.OpenInit(true);
            }
        }
        else
            ConnectOverDone();

        connectOverCalled = false;
    }

    void ConnectOverDone()
    {
        ReConnectCode = 0;
        SpectatorList.Instance.ClientSynList(new List<string>());
        PvPSelector.Instance.ResetPvPInfo();
        PlayerList.Instance.UpdatePlayerList(null, new List<PlayerInfo>());
        BattlePlayerList.Instance.UpdatePlayerList(null, new List<PlayerInfo>());
        StopAllCoroutines();

        if (needLog && needLog2)
            UIManager.Instance.LogPanel.DisplayLog("Desconectado del servidor", null);
        else if (needLog)
            UIManager.Instance.LogPanel.DisplayLog(
                "La conexión ha caducado.",
                () => UIManager.Instance.JoinGame.gameObject.SetActive(true)
            );
    }

    public void SendChatMsg(string msg) => SendMsg(msg, 2, 0);

    public void SendPrivateChatMsg(string name, string content) =>
        SendMsg(JsonUtility.ToJson(new PrivateChatMsg
        {
            PlayerName = name,
            content = content
        }), 2, 1);

    public void ChangeMap(PlayerMap map) =>
        SendMsg(JsonUtility.ToJson(map), 1, 3);

    public void SelectCard(SelectCard card) =>
        SendMsg(JsonUtility.ToJson(card), 1, 4);

    public void SelectPrepare(SelectPrepare prepare) =>
        SendMsg(JsonUtility.ToJson(prepare), 1, 5);

    public void ApplyTool(ToolApply apply) =>
        SendMsg(JsonUtility.ToJson(apply), 1, 1);

    public void UpdateCD(int cardID, bool Ok) =>
        SendMsg(JsonUtility.ToJson(new UpdateCardCD
        {
            CardId = cardID,
            OK = Ok
        }), 1, 8);

    public void ClickedSun(ClickedSun sun) =>
        SendMsg(JsonUtility.ToJson(sun), 1, 2);

    public void ApplyPlacePlant(PlantSpawn spawn) =>
        SendMsg(JsonUtility.ToJson(spawn), 1, 0);

    public void ApplyPlaceZombie(ZombieSpawnApply spawn) =>
        SendMsg(JsonUtility.ToJson(spawn), 1, 10);

    public void ApplyPlacePreview(PlantPreview spawn) =>
        SendMsg(JsonUtility.ToJson(spawn), 1, 7);

    public void ApplyShovelPreview(ShovelPreview spawn) =>
        SendMsg(JsonUtility.ToJson(spawn), 1, 9);

    public void ApplyZombiePreview(ZombiePreview spawn) =>
        SendMsg(JsonUtility.ToJson(spawn), 1, 11);

    public void ApplyJoinTeam(JoinTeamApply spawn) =>
        SendMsg(JsonUtility.ToJson(spawn), 1, 12);

    public void ApplyJoinSpect(JoinSpecApply spawn) =>
        SendMsg(JsonUtility.ToJson(spawn), 1, 13);

    public void SendSynBag(SynItem syn) =>
        SendMsg(JsonUtility.ToJson(syn), 1, 6);

    public void SendSlotMBag(SlotMchBag bag) =>
        SendMsg(JsonUtility.ToJson(bag), 1, 14);

    public void SynItem(SynItem syn)
    {
        if (syn.Type == SynItemType.AllItem || syn.Type == SynItemType.Plant)
        {
            List<PlantBase> plants = PlantManager.Instance.plants;

            for (int i = 0; i < plants.Count; i++)
                if (plants[i].OnlineId == syn.OnlineId)
                {
                    plants[i].OnlineSynPlant(syn);
                    break;
                }
        }

        if (syn.Type == SynItemType.AllItem || syn.Type == SynItemType.Zombie)
        {
            List<ZombieBase> zombies =
                new List<ZombieBase>(ZombieManager.Instance.GetAllZombies());

            zombies.AddRange(ZombieManager.Instance.GetAllHypZombies());

            for (int i = 0; i < zombies.Count; i++)
                if (zombies[i].OnlineId == syn.OnlineId)
                {
                    zombies[i].OnlineSynZombie(syn);
                    break;
                }
        }

        if (syn.Type == SynItemType.AllItem || syn.Type == SynItemType.Puddle)
        {
            List<Puddle> puddles = MapManager.Instance.puddles;

            for (int i = 0; i < puddles.Count; i++)
                if (puddles[i].OnlineId == syn.OnlineId)
                {
                    puddles[i].StartDisappear();
                    break;
                }
        }

        if (syn.Type == SynItemType.AllItem || syn.Type == SynItemType.Portal)
        {
            List<PortalController> portals = MapManager.Instance.portalCs;

            for (int i = 0; i < portals.Count; i++)
                if (portals[i].OnlineId == syn.OnlineId)
                {
                    portals[i].ClientReset(syn);
                    break;
                }
        }

        if (syn.Type == SynItemType.AllItem || syn.Type == SynItemType.Vase)
            LvItemManager.Instance.SynVase(syn);

        if (syn.Type == SynItemType.AllItem || syn.Type == SynItemType.Card)
            SeedBank.Instance.SynDropCard(syn);
    }
}