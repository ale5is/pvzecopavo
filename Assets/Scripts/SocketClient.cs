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

    readonly ConcurrentQueue<byte[]> messageQueue =
        new ConcurrentQueue<byte[]>();

    readonly object socketLock = new object();

    static readonly ConcurrentQueue<Action> UnityMainQueue =
        new ConcurrentQueue<Action>();

    void Awake()
    {
        Instance = this;
        Application.runInBackground = true;

        Debug.Log(
            "[SocketClient] Awake ejecutado. Instance asignada."
        );
    }

    void Update()
    {
        while (messageQueue.TryDequeue(out byte[] msg))
        {
            try
            {
                ProcessMessage(msg);
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "Message processing error: " + e
                );
            }
        }

        ProcessPendingOnlinePlayerInfo();
    }

    void LateUpdate()
    {
        while (UnityMainQueue.TryDequeue(out Action action))
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
        {
            return clientSocket;
        }
    }

    void SetSocket(Socket socket)
    {
        lock (socketLock)
        {
            clientSocket = socket;
        }
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

    void CloseSocket()
    {
        lock (socketLock)
        {
            if (isClosing)
                return;

            isClosing = true;
        }

        Debug.Log(
            "[SocketClient] CloseSocket ejecutado."
        );

        Socket socket = GetSocket();

        SetSocket(null);

        SafeClose(socket);

        ConnectOver();

        isClosing = false;
    }

    public void JoinGame(
        IPAddress ip,
        int port,
        string passWord
    )
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError(
                "JoinGame: GameManager.Instance es null."
            );
            return;
        }

        if (
            WaitConnect != null ||
            GameManager.Instance.isOnline
        )
        {
            return;
        }

        Socket old = GetSocket();

        SetSocket(null);
        SafeClose(old);

        isClosing = false;
        connectOverCalled = false;
        connectionResponseReceived = false;

        pendingOnlinePlayerInfo = null;
        hasPendingOnlinePlayerInfo = false;
        pendingPlayerListDone = false;
        pendingBattlePlayerListDone = false;

        WaitConnect = StartCoroutine(
            WaitLog()
        );

        Socket socket = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp
        )
        {
            NoDelay = true,
            SendBufferSize = BufferSize,
            ReceiveBufferSize = BufferSize
        };

        SetSocket(socket);

        if (GameManager.Instance.LocalPlayerSave == null)
        {
            Debug.LogError(
                "JoinGame: LocalPlayerSave es null."
            );

            SetSocket(null);
            SafeClose(socket);

            WaitConnect = null;

            return;
        }

        PlayerInfo info = new PlayerInfo
        {
            Name =
                GameManager.Instance
                    .LocalPlayerSave.playerName,

            VersionCode =
                GameManager.Instance.VersionCode,

            CmdEnable =
                GameManager.Instance
                    .LocalPlayerSave.CmdEnable,

            ReCntCode = ReConnectCode,

            Password = passWord
        };

        needLog = true;
        needLog2 = false;
        IsHandOver = false;

        Debug.Log(
            "[SocketClient] JoinGame -> conectando a " +
            ip +
            ":" +
            port +
            " como jugador '" +
            info.Name +
            "'"
        );

        ThreadPool.QueueUserWorkItem(
            _ => ConnectAndReceive(
                socket,
                ip,
                port,
                info
            )
        );
    }

    void ConnectAndReceive(
        Socket socket,
        IPAddress ip,
        int port,
        PlayerInfo info
    )
    {
        try
        {
            socket.Connect(
                new IPEndPoint(ip, port)
            );

            if (
                isClosing ||
                GetSocket() != socket
            )
            {
                return;
            }

            Debug.Log(
                "[SocketClient] Conexión TCP establecida con " +
                ip +
                ":" +
                port
            );

            SendMsg(
                JsonUtility.ToJson(info),
                0,
                1,
                socket
            );

            Debug.Log(
                "[SocketClient] PlayerInfo enviado al servidor."
            );

            ReceiveLoop(socket);
        }
        catch (SocketException e)
        {
            if (!isClosing)
            {
                EnqueueLog(
                    "Error de conexión: " + e
                );
            }
        }
        catch (ObjectDisposedException)
        {
            if (!isClosing)
            {
                EnqueueLog(
                    "El socket se ha cerrado."
                );
            }
        }
        catch (Exception e)
        {
            if (!isClosing)
            {
                EnqueueError(e);
            }
        }
    }

    void EnqueueLog(string msg)
    {
        UnityMain(
            () => Debug.Log(msg)
        );
    }

    void EnqueueError(Exception e)
    {
        UnityMain(
            () => Debug.LogError(e)
        );
    }

    void UnityMain(Action action)
    {
        if (action == null)
            return;

        UnityMainQueue.Enqueue(action);
    }

    void ReceiveLoop(Socket socket)
    {
        byte[] buffer =
            new byte[BufferSize];

        byte[] pending =
            new byte[
                MaxPacket +
                BufferSize +
                4
            ];

        int pendingCount = 0;

        try
        {
            while (
                !isClosing &&
                GetSocket() == socket
            )
            {
                int received;

                try
                {
                    received = socket.Receive(
                        buffer,
                        0,
                        buffer.Length,
                        SocketFlags.None
                    );
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException e)
                {
                    if (!isClosing)
                    {
                        EnqueueError(
                            new Exception(
                                "Conexión perdida",
                                e
                            )
                        );
                    }

                    break;
                }

                if (received <= 0)
                    break;

                if (
                    pendingCount + received >
                    pending.Length
                )
                {
                    EnqueueLog(
                        "Los datos recibidos son demasiado grandes."
                    );

                    break;
                }

                Buffer.BlockCopy(
                    buffer,
                    0,
                    pending,
                    pendingCount,
                    received
                );

                pendingCount += received;

                int offset = 0;

                while (
                    pendingCount - offset >= 4
                )
                {
                    int len =
                        pending[offset] |
                        pending[offset + 1] << 8 |
                        pending[offset + 2] << 16 |
                        pending[offset + 3] << 24;

                    if (
                        len < 2 ||
                        len > MaxPacket
                    )
                    {
                        EnqueueLog(
                            "Se recibió un paquete de datos no válido."
                        );

                        return;
                    }

                    int total = len + 4;

                    if (
                        pendingCount - offset <
                        total
                    )
                    {
                        break;
                    }

                    byte[] packet =
                        new byte[len];

                    Buffer.BlockCopy(
                        pending,
                        offset + 4,
                        packet,
                        0,
                        len
                    );

                    if (
                        packet.Length >= 2 &&
                        packet[0] == 0 &&
                        (
                            packet[1] == 1 ||
                            packet[1] == 2 ||
                            packet[1] == 3 ||
                            packet[1] == byte.MaxValue
                        )
                    )
                    {
                        connectionResponseReceived = true;
                    }

                    Debug.Log(
                        "[SocketClient] Paquete recibido -> " +
                        "type1=" +
                        packet[0] +
                        " | type2=" +
                        packet[1] +
                        " | bytes=" +
                        packet.Length
                    );

                    if (
                        packet.Length >= 2 &&
                        packet[0] == 1 &&
                        packet[1] == 1
                    )
                    {
                        Debug.Log(
                            "[SocketClient] >>> PAQUETE START DE BATALLA 1/1 RECIBIDO EN ReceiveLoop <<<"
                        );
                    }

                    messageQueue.Enqueue(packet);

                    offset += total;
                }

                if (offset > 0)
                {
                    int left =
                        pendingCount - offset;

                    if (left > 0)
                    {
                        Buffer.BlockCopy(
                            pending,
                            offset,
                            pending,
                            0,
                            left
                        );
                    }

                    pendingCount = left;
                }
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException e)
        {
            if (!isClosing)
            {
                EnqueueError(
                    new Exception(
                        "Conexión perdida",
                        e
                    )
                );
            }
        }
        catch (Exception e)
        {
            if (!isClosing)
            {
                EnqueueError(
                    new Exception(
                        "Conexión perdida",
                        e
                    )
                );
            }
        }
        finally
        {
            if (
                !isClosing &&
                GetSocket() == socket
            )
            {
                UnityMain(
                    CloseSocket
                );
            }
        }
    }

    void ProcessMessage(byte[] data)
    {
        if (
            data == null ||
            data.Length < 2
        )
        {
            Debug.LogWarning(
                "[SocketClient] ProcessMessage recibió datos inválidos."
            );

            return;
        }

        byte a = data[0];
        byte b = data[1];

        string s =
            data.Length > 2
                ? Encoding.UTF8.GetString(
                    data,
                    2,
                    data.Length - 2
                )
                : "";

        Debug.Log(
            "[SocketClient] ProcessMessage -> " +
            "type1=" +
            a +
            " | type2=" +
            b +
            " | data=" +
            s
        );

        switch (a)
        {
            case 0:
                ProcessConnection(b, s);
                break;

            case 1:
                ProcessGame(b, s);
                break;

            case 2:
                ProcessSpawn(b, s);
                break;

            case 3:
                ProcessWorld(b, s);
                break;

            case 4:
                ProcessCommand(b, s);
                break;

            default:
                Debug.LogWarning(
                    "[SocketClient] type1 desconocido: " +
                    a
                );
                break;
        }
    }

    void ProcessConnection(
        byte b,
        string s
    )
    {
        if (b == 0)
        {
            Debug.Log(
                "0-0" + s
            );

            return;
        }

        if (b == 1)
        {
            connectionResponseReceived = true;

            IsHandOver = true;
            needLog = false;

            ConnectInfo info = null;

            try
            {
                info =
                    JsonUtility.FromJson<ConnectInfo>(
                        s
                    );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "Error al leer ConnectInfo: " + e
                );

                return;
            }

            if (info == null)
            {
                Debug.LogError(
                    "ConnectInfo es null. Datos: " + s
                );

                return;
            }

            if (UIManager.Instance == null)
            {
                Debug.LogError(
                    "ProcessConnection b=1: " +
                    "UIManager.Instance es null."
                );

                CloseSocket();
                return;
            }

            if (UIManager.Instance.LogPanel == null)
            {
                Debug.LogError(
                    "ProcessConnection b=1: " +
                    "LogPanel es null."
                );

                CloseSocket();
                return;
            }

            if (
                LVManager.Instance != null &&
                LVManager.Instance.InGame
            )
            {
                UIManager.Instance.LogPanel
                    .DisplayLog(
                        "Por favor, inténtelo de nuevo.",
                        null
                    );
            }
            else
            {
                UIManager.Instance.LogPanel
                    .DisplayLog(
                        info.msg ?? "",
                        () =>
                        {
                            if (
                                GameManager.Instance != null &&
                                !GameManager.Instance.isOnline &&
                                UIManager.Instance != null &&
                                UIManager.Instance.JoinGame != null
                            )
                            {
                                UIManager.Instance.JoinGame
                                    .gameObject
                                    .SetActive(true);
                            }
                        }
                    );
            }

            CloseSocket();

            return;
        }

        if (b == 2)
        {
            connectionResponseReceived = true;

            OnlinePlayerInfo info = null;

            try
            {
                info =
                    JsonUtility.FromJson<OnlinePlayerInfo>(
                        s
                    );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "Error al leer OnlinePlayerInfo: " +
                    e
                );

                return;
            }

            if (info == null)
            {
                Debug.LogError(
                    "OnlinePlayerInfo es null. Datos: " +
                    s
                );

                return;
            }

            if (GameManager.Instance == null)
            {
                Debug.LogError(
                    "ProcessConnection b=2: " +
                    "GameManager.Instance es null."
                );

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

        if (b == 3)
        {
            connectionResponseReceived = true;

            ReConnectInfo info = null;

            try
            {
                info =
                    JsonUtility.FromJson<ReConnectInfo>(
                        s
                    );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "Error al leer ReConnectInfo: " +
                    e
                );

                return;
            }

            if (info == null)
            {
                Debug.LogError(
                    "ReConnectInfo es null. Datos: " +
                    s
                );

                return;
            }

            if (GameManager.Instance == null)
            {
                Debug.LogError(
                    "ProcessConnection b=3: " +
                    "GameManager.Instance es null."
                );

                return;
            }

            ConnectSuccess();

            if (ReConnect.Instance == null)
            {
                Debug.LogError(
                    "ProcessConnection b=3: " +
                    "ReConnect.Instance es null."
                );

                CloseSocket();
                return;
            }

            if (info.isWait)
            {
                ReConnect.Instance
                    .OpenInit(false);
            }
            else
            {
                ReConnect.Instance
                    .OverClose();
            }

            ReConnect.Instance
                .LoadPlayerList(
                    info.names ??
                    new List<string>()
                );

            return;
        }

        if (b == byte.MaxValue)
        {
            OnlineCheck = true;
        }
    }

    void ProcessPendingOnlinePlayerInfo()
    {
        if (
            !hasPendingOnlinePlayerInfo ||
            pendingOnlinePlayerInfo == null
        )
        {
            return;
        }

        OnlinePlayerInfo info =
            pendingOnlinePlayerInfo;

        List<PlayerInfo> players =
            info.players ??
            new List<PlayerInfo>();

        if (
            !pendingPlayerListDone &&
            PlayerList.Instance != null
        )
        {
            PlayerList.Instance
                .UpdatePlayerList(
                    info.HostPlayer,
                    players
                );

            pendingPlayerListDone = true;
        }

        if (
            !pendingBattlePlayerListDone &&
            BattlePlayerList.Instance != null
        )
        {
            BattlePlayerList.Instance
                .UpdatePlayerList(
                    info.HostPlayer,
                    players
                );

            pendingBattlePlayerListDone = true;
        }

        if (
            pendingPlayerListDone &&
            pendingBattlePlayerListDone
        )
        {
            pendingOnlinePlayerInfo = null;
            hasPendingOnlinePlayerInfo = false;
        }
    }

    void ProcessGame(
        byte b,
        string s
    )
    {
        Debug.Log(
            "[SocketClient] ProcessGame RECIBIDO | b=" +
            b +
            " | data=" +
            s
        );

        if (b == 0)
        {
            LoadLVBag bag =
                JsonUtility.FromJson<LoadLVBag>(s);

            if (bag == null)
            {
                Debug.LogError(
                    "[SocketClient] ProcessGame b=0: LoadLVBag es null."
                );

                return;
            }

            Debug.Log(
                "[SocketClient] ProcessGame b=0 -> " +
                "LoadType=" +
                bag.LoadType +
                " | LvName=" +
                bag.LvName +
                " | LvId=" +
                bag.LvId
            );

            ReConnectCode =
                bag.ReCntCode;

            if (LVManager.Instance == null)
            {
                Debug.LogError(
                    "[SocketClient] ProcessGame b=0: LVManager.Instance es null."
                );

                return;
            }

            if (bag.LoadType == 0)
            {
                Debug.Log(
                    "[SocketClient] Llamando LVManager.StartGame()."
                );

                LVManager.Instance
                    .StartGame(bag, -1);
            }
            else if (bag.LoadType == 1)
            {
                Debug.Log(
                    "[SocketClient] Llamando LVManager.ReStartGame()."
                );

                LVManager.Instance
                    .ReStartGame();
            }
            else if (bag.LoadType == 2)
            {
                Debug.Log(
                    "[SocketClient] Llamando LVManager.QuitBattleGame()."
                );

                LVManager.Instance
                    .QuitBattleGame();
            }

            return;
        }

        if (b == 1)
        {
            Debug.Log(
                "[SocketClient] >>> RECIBIDO START DE BATALLA (1/1) <<<"
            );

            Debug.Log(
                "[SocketClient] Estado antes del arranque -> " +
                "SeedChooser=" +
                (
                    SeedChooser.Instance != null
                        ? "OK"
                        : "NULL"
                ) +
                " | ZombieChooser=" +
                (
                    ZombieChooser.Instance != null
                        ? "OK"
                        : "NULL"
                ) +
                " | LVManager=" +
                (
                    LVManager.Instance != null
                        ? "OK"
                        : "NULL"
                ) +
                " | GameManager=" +
                (
                    GameManager.Instance != null
                        ? "OK"
                        : "NULL"
                )
            );

            if (SeedChooser.Instance != null)
            {
                Debug.Log(
                    "[SocketClient] Llamando SeedChooser.StartRunLv(true)..."
                );

                SeedChooser.Instance.StartRunLv(true);

                Debug.Log(
                    "[SocketClient] SeedChooser.StartRunLv(true) TERMINADO."
                );
            }
            else
            {
                Debug.LogError(
                    "[SocketClient] SeedChooser.Instance es NULL."
                );
            }

            if (ZombieChooser.Instance != null)
            {
                Debug.Log(
                    "[SocketClient] Llamando ZombieChooser.StartRunLv(true)..."
                );

                ZombieChooser.Instance.StartRunLv(true);

                Debug.Log(
                    "[SocketClient] ZombieChooser.StartRunLv(true) TERMINADO."
                );
            }
            else
            {
                Debug.LogError(
                    "[SocketClient] ZombieChooser.Instance es NULL."
                );
            }

            Debug.Log(
                "[SocketClient] <<< FIN START DE BATALLA (1/1) >>>"
            );

            return;
        }

        if (b == 2)
        {
            if (LVManager.Instance != null)
                LVManager.Instance.ClientShowBigWave(
                    JsonUtility.FromJson<WaveComing>(s)
                );
        }
        else if (b == 3)
        {
            if (PlayerManager.Instance != null)
                PlayerManager.Instance.ClientUpdateSunNum(
                    JsonUtility.FromJson<SunNumBag>(s)
                );
        }
        else if (b == 4)
        {
            SynItem(
                JsonUtility.FromJson<SynItem>(s)
            );
        }
        else if (b == 5)
        {
            PlayerMap m =
                JsonUtility.FromJson<PlayerMap>(s);

            if (
                m != null &&
                BattlePlayerList.Instance != null
            )
            {
                BattlePlayerList.Instance.UpdateMapSprite(
                    m.PlayerName,
                    m.Pos
                );
            }
        }
        else if (b == 6)
        {
            SelectCard c =
                JsonUtility.FromJson<SelectCard>(s);

            if (
                c != null &&
                GameManager.Instance != null &&
                BattlePlayerList.Instance != null &&
                GameManager.Instance.LocalPlayerSave != null &&
                c.PlayerName !=
                GameManager.Instance.LocalPlayerSave.playerName
            )
            {
                if (c.isBack)
                {
                    BattlePlayerList.Instance.CancelCard(
                        c.PlayerName,
                        c.cardId
                    );
                }
                else
                {
                    BattlePlayerList.Instance.SelectCard(
                        c.PlayerName,
                        c.plantType,
                        c.zombieType,
                        c.noAnim
                    );
                }
            }
        }
        else if (b == 7)
        {
            SelectPrepare p =
                JsonUtility.FromJson<SelectPrepare>(s);

            if (
                p != null &&
                BattlePlayerList.Instance != null
            )
            {
                BattlePlayerList.Instance.UpdateState(
                    p.PlayerName,
                    p.isPrepare
                );
            }
        }
        else if (b == 8)
        {
            GameOver g =
                JsonUtility.FromJson<GameOver>(s);

            if (
                g != null &&
                LV.Instance != null &&
                LVManager.Instance != null
            )
            {
                if (LV.Instance.CurrLVType == LVType.PvP)
                    LVManager.Instance.PvPGameOver(
                        g.pos,
                        g.isRedFail
                    );
                else
                    LVManager.Instance.ZombieGameOver(g.pos);
            }
        }
        else if (b == 9)
        {
            if (PvPSelector.Instance != null)
                PvPSelector.Instance.ClientSynTeam(
                    JsonUtility.FromJson<PvPTeamList>(s)
                );
        }
        else if (b == 10)
        {
            if (PvPSelector.Instance != null)
                PvPSelector.Instance.ClientSynMode(
                    JsonUtility.FromJson<PvPModeSyn>(s)
                );
        }
        else if (b == 11)
        {
            SpectList list =
                JsonUtility.FromJson<SpectList>(s);

            if (
                list != null &&
                SpectatorList.Instance != null
            )
            {
                SpectatorList.Instance.ClientSynList(
                    list.names ??
                    new List<string>()
                );
            }
        }
        else if (b == 12)
        {
            AddCardBag bag =
                JsonUtility.FromJson<AddCardBag>(s);

            if (
                bag != null &&
                SeedBank.Instance != null
            )
            {
                SeedBank.Instance.AddCards(
                    bag.CardTypes,
                    true
                );
            }
        }
        else if (b == 13)
        {
            if (FlagMeter.Instance != null)
                FlagMeter.Instance.ClientSyn(
                    JsonUtility.FromJson<FlagMeterSyn>(s)
                );
        }
        else if (b == 14)
        {
            if (Timetable.Instance != null)
                Timetable.Instance.ClientSyn(
                    JsonUtility.FromJson<TimetableSyn>(s)
                );
        }
    }

    void ProcessSpawn(
        byte b,
        string s
    )
    {
        if (b == 0)
        {
            PlantSpawn p =
                JsonUtility.FromJson<PlantSpawn>(s);

            if (
                p == null ||
                PlantManager.Instance == null ||
                SeedBank.Instance == null ||
                MapManager.Instance == null ||
                LV.Instance == null
            )
                return;

            PlantBase plant =
                PlantManager.Instance.GetNewPlant(
                    p.plantType
                );

            if (plant == null)
                return;

            if (p.SPcode == 2)
                plant.InitForCreate(
                    false,
                    null,
                    false
                );

            if (
                LV.Instance.CurrLVType == LVType.PvP &&
                PvPSelector.Instance != null &&
                !PvPSelector.Instance.IsSameTeam(
                    p.PlacePlayer
                )
            )
            {
                p.GridPos =
                    new Vector2(
                        -p.GridPos.x,
                        p.GridPos.y
                    );
            }

            plant.OnlineId = p.OnlineId;

            SeedBank.Instance.PlantConfirm(
                plant,
                MapManager.Instance.GetGridByWorldPos(
                    p.GridPos
                ),
                -1,
                p.SPcode,
                p.PlacePlayer
            );
        }
        else if (b == 1)
        {
            if (SkyManager.Instance != null)
                SkyManager.Instance.ClientSpawnSun(
                    JsonUtility.FromJson<SunSpawn>(s)
                );
        }
        else if (b == 2)
        {
            if (SkyManager.Instance != null)
                SkyManager.Instance.OnlineCollectSun(
                    JsonUtility.FromJson<ClickedSun>(s)
                );
        }
        else if (b == 3)
        {
            if (BattlePlayerList.Instance != null)
                BattlePlayerList.Instance.PreviewPlant(
                    JsonUtility.FromJson<PlantPreview>(s)
                );
        }
        else if (b == 4)
        {
            UpdateCardCD c =
                JsonUtility.FromJson<UpdateCardCD>(s);

            if (
                c == null ||
                GameManager.Instance == null ||
                GameManager.Instance.LocalPlayerSave == null
            )
                return;

            if (
                c.name ==
                GameManager.Instance.LocalPlayerSave.playerName
            )
            {
                if (
                    c.OK &&
                    SeedBank.Instance != null
                )
                {
                    SeedBank.Instance.PlantFailClearCD(
                        c.CardId
                    );
                }
            }
            else if (BattlePlayerList.Instance != null)
            {
                BattlePlayerList.Instance.UpdateCardCD(
                    c.name,
                    c.CardId,
                    c.OK
                );
            }
        }
        else if (b == 5)
        {
            ShovelPreview p =
                JsonUtility.FromJson<ShovelPreview>(s);

            if (
                p != null &&
                BattlePlayerList.Instance != null
            )
            {
                BattlePlayerList.Instance.PreviewShovel(
                    p.PlayerName,
                    p.GridPos,
                    p.isShow
                );
            }
        }
        else if (b == 6)
        {
            ToolApply t =
                JsonUtility.FromJson<ToolApply>(s);

            if (
                t != null &&
                BattlePlayerList.Instance != null
            )
            {
                BattlePlayerList.Instance.PlayShovelAnimation(
                    t.GridPos,
                    t.Sound,
                    t.User
                );
            }
        }
    }

    void ProcessWorld(
        byte b,
        string s
    )
    {
        if (b == 0)
        {
            if (ZombieManager.Instance != null)
                ZombieManager.Instance.UpdateZombie(
                    JsonUtility.FromJson<ZombieSpawn>(s)
                );
        }
        else if (b == 1)
        {
            GraveStoneSpawn g =
                JsonUtility.FromJson<GraveStoneSpawn>(s);

            if (
                g != null &&
                MapManager.Instance != null
            )
            {
                Grid grid =
                    MapManager.Instance.GetGridByWorldPos(
                        g.MapPos
                    );

                if (grid != null)
                    grid.ClientSynGrave(
                        g.Type,
                        g.isHave
                    );
            }
        }
        else if (b == 2)
        {
            PuddleSpawn p =
                JsonUtility.FromJson<PuddleSpawn>(s);

            if (
                p == null ||
                MapManager.Instance == null ||
                GameManager.Instance == null ||
                GameManager.Instance.GameConf == null
            )
                return;

            List<Vector2> positions =
                p.MapPos ??
                new List<Vector2>();

            List<Grid> grids =
                new List<Grid>(
                    positions.Count
                );

            for (
                int i = 0;
                i < positions.Count;
                i++
            )
            {
                Grid grid =
                    MapManager.Instance.GetGridByWorldPos(
                        positions[i]
                    );

                if (grid != null)
                    grids.Add(grid);
            }

            if (
                GameManager.Instance.GameConf.Puddle == null
            )
                return;

            Puddle puddle =
                UnityEngine.Object.Instantiate(
                    GameManager.Instance.GameConf.Puddle
                ).GetComponent<Puddle>();

            if (puddle == null)
                return;

            puddle.CreateInit(
                grids,
                p.InitPos,
                p.OnlineId
            );

            MapManager.Instance.puddles.Add(puddle);
        }
        else if (b == 3)
        {
            LightingSpawn l =
                JsonUtility.FromJson<LightingSpawn>(s);

            if (
                l != null &&
                SkyManager.Instance != null &&
                MapManager.Instance != null
            )
            {
                Grid grid =
                    MapManager.Instance.GetGridByWorldPos(
                        l.Pos
                    );

                if (grid != null)
                    SkyManager.Instance.ClientLightningThis(grid);
            }
        }
        else if (b == 4)
        {
            SynMap m =
                JsonUtility.FromJson<SynMap>(s);

            if (
                m != null &&
                MapManager.Instance != null
            )
            {
                var map =
                    MapManager.Instance.GetCurrMap(
                        m.mapPos
                    );

                if (map != null)
                    map.SynMap(m);
            }
        }
        else if (b == 5)
        {
            if (BattlePlayerList.Instance != null)
                BattlePlayerList.Instance.PreviewZombie(
                    JsonUtility.FromJson<ZombiePreview>(s)
                );
        }
        else if (b == 6)
        {
            if (MapManager.Instance != null)
                MapManager.Instance.ClientCreatePortal(
                    JsonUtility.FromJson<PortalSpawn>(s)
                );
        }
        else if (b == 7)
        {
            SynGrid g =
                JsonUtility.FromJson<SynGrid>(s);

            if (
                g == null ||
                MapManager.Instance == null ||
                LV.Instance == null
            )
                return;

            if (
                LV.Instance.CurrLVType == LVType.PvP &&
                PvPSelector.Instance != null &&
                GameManager.Instance != null &&
                !PvPSelector.Instance.IsSameTeam(
                    GameManager.Instance.HostName
                )
            )
            {
                g.GridPos =
                    MyTool.ReverseX(g.GridPos);
            }

            Grid grid =
                MapManager.Instance.GetGridByWorldPos(
                    g.GridPos
                );

            if (grid != null)
                grid.ClientSynState(g);
        }
        else if (b == 8)
        {
            SynBooty booty =
                JsonUtility.FromJson<SynBooty>(s);

            if (
                booty != null &&
                LVManager.Instance != null &&
                LVManager.Instance.InGame
            )
            {
                if (booty.isSpawn)
                {
                    LVManager.Instance.SpawnBooty(
                        booty.pos,
                        booty
                    );
                }
                else if (
                    LVManager.Instance.OnlyBooty != null
                )
                {
                    LVManager.Instance.OnlyBooty.CollectBooty();
                }
            }
        }
        else if (b == 9)
        {
            if (LvItemManager.Instance != null)
                LvItemManager.Instance.ClientCreateVase(
                    JsonUtility.FromJson<VaseSpawn>(s)
                );
        }
        else if (b == 10)
        {
            if (SeedBank.Instance != null)
                SeedBank.Instance.ClientSpawnCard(
                    JsonUtility.FromJson<CardSpawn>(s)
                );
        }
        else if (b == 11)
        {
            if (LvItemManager.Instance != null)
                LvItemManager.Instance.SpawnMelt(
                    JsonUtility.FromJson<MeltSpawn>(s)
                );
        }
        else if (b == 12)
        {
            if (LvItemManager.Instance != null)
                LvItemManager.Instance.SpawnFallHail(
                    JsonUtility.FromJson<FallHailSpawn>(s)
                );
        }
    }

    void ProcessCommand(
        byte b,
        string s
    )
    {
        if (b == 0)
        {
            if (ChatInput.Instance != null)
                ChatInput.Instance.AddMessage(s);
        }
        else if (b == 1)
        {
            if (ChatInput.Instance != null)
            {
                ChatInput.Instance.AddMessage(
                    s,
                    new Color32(
                        255,
                        255,
                        0,
                        255
                    )
                );
            }
        }
        else if (b == 2)
        {
            PrivateChatMsg m =
                JsonUtility.FromJson<PrivateChatMsg>(s);

            if (
                m != null &&
                ChatInput.Instance != null
            )
            {
                ChatInput.Instance.AddMessage(
                    "Jugador" +
                    m.PlayerName +
                    "Susurro:" +
                    m.content,
                    new Color32(
                        123,
                        123,
                        123,
                        255
                    )
                );
            }
        }
        else if (b == 3)
        {
            CommandBag c =
                JsonUtility.FromJson<CommandBag>(s);

            if (c == null)
                return;

            if (PlantManager.Instance != null)
                PlantManager.Instance.PlantInvincible = c.Pinv;

            if (ZombieManager.Instance != null)
                ZombieManager.Instance.ZombieInvincible = c.Zinv;

            if (SkyManager.Instance != null)
                SkyManager.Instance.DayLightCycle = c.DLiCy;

            if (PlayerManager.Instance != null)
                PlayerManager.Instance.SunInfinite = c.SnInf;

            if (SeedBank.Instance != null)
                SeedBank.Instance.isNoCD = c.CdCle;

            if (ZombieManager.Instance != null)
                ZombieManager.Instance.ZombieDontMove = c.ZomStop;

            if (LvItemManager.Instance != null)
                LvItemManager.Instance.VaseAlwaysLight = c.VaseXray;
        }
        else if (b == 4)
        {
            if (SkyManager.Instance != null)
                SkyManager.Instance.ClientSynWeather(
                    JsonUtility.FromJson<WeatherChange>(s)
                );
        }
        else if (b == 5)
        {
            TimeCmd t =
                JsonUtility.FromJson<TimeCmd>(s);

            if (
                t == null ||
                SkyManager.Instance == null
            )
                return;

            if (t.time >= 10000)
            {
                t.time -= 10000;

                SkyManager.Instance.DirectSetTime(
                    t.time,
                    true
                );
            }
            else
            {
                SkyManager.Instance.Time = t.time;
            }
        }
        else if (b == 6)
        {
            GetAcvment a =
                JsonUtility.FromJson<GetAcvment>(s);

            if (
                a != null &&
                AcvmentManager.Instance != null
            )
            {
                AcvmentManager.Instance.GetAchievement(a.acv);
            }
        }
        else if (b == 99)
        {
            Debug.Log(s);
        }
    }

    void SendMsg(
        string content,
        byte type1,
        byte type2,
        Socket socket = null
    )
    {
        Socket target =
            socket ?? GetSocket();

        if (target == null)
        {
            Debug.LogError(
                type1 +
                "/" +
                type2 +
                "Enviar desconexión"
            );

            return;
        }

        if (isClosing)
            return;

        if (content == null)
            content = "";

        try
        {
            int count =
                Encoding.UTF8.GetByteCount(content);

            int len =
                count + 2;

            if (len > MaxPacket)
            {
                Debug.LogError(
                    "Los datos enviados son demasiado grandes."
                );

                return;
            }

            byte[] buffer =
                new byte[count + 6];

            buffer[0] = (byte)len;
            buffer[1] = (byte)(len >> 8);
            buffer[2] = (byte)(len >> 16);
            buffer[3] = (byte)(len >> 24);
            buffer[4] = type1;
            buffer[5] = type2;

            Encoding.UTF8.GetBytes(
                content,
                0,
                content.Length,
                buffer,
                6
            );

            int sent = 0;

            while (sent < buffer.Length)
            {
                int n =
                    target.Send(
                        buffer,
                        sent,
                        buffer.Length - sent,
                        SocketFlags.None
                    );

                if (n <= 0)
                    throw new SocketException();

                sent += n;
            }

            if (
                type1 == 1 &&
                type2 == 1
            )
            {
                Debug.Log(
                    "[SocketClient] >>> ENVIADO PAQUETE START 1/1 <<<"
                );
            }
        }
        catch (ObjectDisposedException)
        {
            if (!isClosing)
            {
                Debug.LogError(
                    type1 +
                    "/" +
                    type2 +
                    "Enviar desconexión"
                );
            }
        }
        catch (SocketException e)
        {
            if (!isClosing)
            {
                Debug.LogError(
                    type1 +
                    "/" +
                    type2 +
                    "Enviar desconexión"
                );

                Debug.LogError(e);

                CloseSocket();
            }
        }
        catch (Exception e)
        {
            if (!isClosing)
            {
                Debug.LogError(
                    type1 +
                    "/" +
                    type2 +
                    "Enviar desconexión"
                );

                Debug.LogError(e);

                CloseSocket();
            }
        }
    }

    IEnumerator SendHeartbeat()
    {
        float t =
            Time.realtimeSinceStartup;

        while (
            GameManager.Instance != null &&
            GameManager.Instance.isOnline
        )
        {
            yield return null;

            if (
                Time.realtimeSinceStartup - t >
                0.5f
            )
            {
                SendMsg(
                    "",
                    0,
                    byte.MaxValue
                );

                t =
                    Time.realtimeSinceStartup;
            }
        }
    }

    IEnumerator CheckConnect()
    {
        float t =
            Time.realtimeSinceStartup;

        while (
            GameManager.Instance != null &&
            GameManager.Instance.isOnline
        )
        {
            yield return null;

            if (
                Time.realtimeSinceStartup - t >
                3f
            )
            {
                if (!OnlineCheck)
                {
                    Debug.LogError(
                        "Desconexión por falta de señal"
                    );

                    CloseSocket();

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

        while (
            Time.realtimeSinceStartup - t <= 3f &&
            GameManager.Instance != null &&
            !GameManager.Instance.isOnline &&
            WaitConnect != null &&
            !connectionResponseReceived
        )
        {
            yield return null;
        }

        if (
            GameManager.Instance != null &&
            GameManager.Instance.isOnline
        )
        {
            WaitConnect = null;
            yield break;
        }

        if (connectionResponseReceived)
        {
            WaitConnect = null;
            yield break;
        }

        Debug.Log("e999");

        WaitConnect = null;

        CloseSocket();

        if (
            UIManager.Instance != null &&
            UIManager.Instance.LogPanel != null
        )
        {
            UIManager.Instance.LogPanel.Confirm();
        }
    }

    void ConnectSuccess()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError(
                "ConnectSuccess: " +
                "GameManager.Instance es null."
            );

            return;
        }

        if (GameManager.Instance.isOnline)
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

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ConnectSuccess();
        }
        else
        {
            Debug.LogError(
                "ConnectSuccess: " +
                "UIManager.Instance es null."
            );
        }
    }

    public void CloseClient()
    {
        SendMsg(
            "",
            0,
            2
        );

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
            if (GameManager.Instance == null)
                return;

            if (!GameManager.Instance.isOnline)
            {
                if (WaitConnect != null)
                {
                    StopCoroutine(WaitConnect);
                    WaitConnect = null;
                }

                return;
            }

            Debug.LogError("6666");

            GameManager.Instance.isOnline = false;

            if (WaitConnect != null)
            {
                StopCoroutine(WaitConnect);
                WaitConnect = null;
            }

            if (
                LVManager.Instance != null &&
                LVManager.Instance.InGame
            )
            {
                if (IsHandOver)
                {
                    LVManager.Instance.QuitBattleGame();

                    ConnectOverDone();
                }
                else
                {
                    StopAllCoroutines();

                    if (ReConnect.Instance != null)
                    {
                        ReConnect.Instance.OpenInit(true);
                    }
                    else
                    {
                        Debug.LogError(
                            "ConnectOver: " +
                            "ReConnect.Instance es null."
                        );

                        ConnectOverDone();
                    }
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

        if (SpectatorList.Instance != null)
        {
            SpectatorList.Instance.ClientSynList(
                new List<string>()
            );
        }

        if (PvPSelector.Instance != null)
        {
            PvPSelector.Instance.ResetPvPInfo();
        }

        if (PlayerList.Instance != null)
        {
            PlayerList.Instance.UpdatePlayerList(
                null,
                new List<PlayerInfo>()
            );
        }

        if (BattlePlayerList.Instance != null)
        {
            BattlePlayerList.Instance.UpdatePlayerList(
                null,
                new List<PlayerInfo>()
            );
        }

        StopAllCoroutines();

        if (
            UIManager.Instance != null &&
            UIManager.Instance.LogPanel != null
        )
        {
            if (needLog && needLog2)
            {
                UIManager.Instance.LogPanel.DisplayLog(
                    "Desconectado del servidor",
                    null
                );
            }
            else if (needLog)
            {
                UIManager.Instance.LogPanel.DisplayLog(
                    "La conexión ha caducado.",
                    () =>
                    {
                        if (
                            UIManager.Instance != null &&
                            UIManager.Instance.JoinGame != null
                        )
                        {
                            UIManager.Instance.JoinGame
                                .gameObject
                                .SetActive(true);
                        }
                    }
                );
            }
        }
    }

    public void SendChatMsg(string msg)
    {
        SendMsg(
            msg,
            2,
            0
        );
    }

    public void SendPrivateChatMsg(
        string name,
        string content
    )
    {
        SendMsg(
            JsonUtility.ToJson(
                new PrivateChatMsg
                {
                    PlayerName = name,
                    content = content
                }
            ),
            2,
            1
        );
    }

    public void ChangeMap(PlayerMap map)
    {
        SendMsg(
            JsonUtility.ToJson(map),
            1,
            3
        );
    }

    public void SelectCard(SelectCard card)
    {
        SendMsg(
            JsonUtility.ToJson(card),
            1,
            4
        );
    }

    public void SelectPrepare(
        SelectPrepare prepare
    )
    {
        SendMsg(
            JsonUtility.ToJson(prepare),
            1,
            5
        );
    }

    public void ApplyTool(
        ToolApply apply
    )
    {
        SendMsg(
            JsonUtility.ToJson(apply),
            1,
            1
        );
    }

    public void UpdateCD(
        int cardID,
        bool Ok
    )
    {
        SendMsg(
            JsonUtility.ToJson(
                new UpdateCardCD
                {
                    CardId = cardID,
                    OK = Ok
                }
            ),
            1,
            8
        );
    }

    public void ClickedSun(
        ClickedSun sun
    )
    {
        SendMsg(
            JsonUtility.ToJson(sun),
            1,
            2
        );
    }

    public void ApplyPlacePlant(
        PlantSpawn spawn
    )
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            0
        );
    }

    public void ApplyPlaceZombie(
        ZombieSpawnApply spawn
    )
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            10
        );
    }

    public void ApplyPlacePreview(
        PlantPreview spawn
    )
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            7
        );
    }

    public void ApplyShovelPreview(
        ShovelPreview spawn
    )
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            9
        );
    }

    public void ApplyZombiePreview(
        ZombiePreview spawn
    )
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            11
        );
    }

    public void ApplyJoinTeam(
        JoinTeamApply spawn
    )
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            12
        );
    }

    public void ApplyJoinSpect(
        JoinSpecApply spawn
    )
    {
        SendMsg(
            JsonUtility.ToJson(spawn),
            1,
            13
        );
    }

    public void SendSynBag(
        SynItem syn
    )
    {
        SendMsg(
            JsonUtility.ToJson(syn),
            1,
            6
        );
    }

    public void SendSlotMBag(
        SlotMchBag bag
    )
    {
        SendMsg(
            JsonUtility.ToJson(bag),
            1,
            14
        );
    }

    public void SynItem(SynItem syn)
    {
        if (syn == null)
            return;

        if (
            syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Plant
        )
        {
            if (PlantManager.Instance != null)
            {
                List<PlantBase> plants =
                    PlantManager.Instance.plants;

                if (plants != null)
                {
                    for (
                        int i = 0;
                        i < plants.Count;
                        i++
                    )
                    {
                        if (
                            plants[i] != null &&
                            plants[i].OnlineId ==
                            syn.OnlineId
                        )
                        {
                            plants[i].OnlineSynPlant(syn);
                            break;
                        }
                    }
                }
            }
        }

        if (
            syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Zombie
        )
        {
            if (ZombieManager.Instance != null)
            {
                List<ZombieBase> zombies =
                    new List<ZombieBase>(
                        ZombieManager.Instance.GetAllZombies()
                    );

                List<ZombieBase> hypZombies =
                    ZombieManager.Instance.GetAllHypZombies();

                if (hypZombies != null)
                    zombies.AddRange(hypZombies);

                for (
                    int i = 0;
                    i < zombies.Count;
                    i++
                )
                {
                    if (
                        zombies[i] != null &&
                        zombies[i].OnlineId ==
                        syn.OnlineId
                    )
                    {
                        zombies[i].OnlineSynZombie(syn);
                        break;
                    }
                }
            }
        }

        if (
            syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Puddle
        )
        {
            if (MapManager.Instance != null)
            {
                List<Puddle> puddles =
                    MapManager.Instance.puddles;

                if (puddles != null)
                {
                    for (
                        int i = 0;
                        i < puddles.Count;
                        i++
                    )
                    {
                        if (
                            puddles[i] != null &&
                            puddles[i].OnlineId ==
                            syn.OnlineId
                        )
                        {
                            puddles[i].StartDisappear();
                            break;
                        }
                    }
                }
            }
        }

        if (
            syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Portal
        )
        {
            if (MapManager.Instance != null)
            {
                List<PortalController> portals =
                    MapManager.Instance.portalCs;

                if (portals != null)
                {
                    for (
                        int i = 0;
                        i < portals.Count;
                        i++
                    )
                    {
                        if (
                            portals[i] != null &&
                            portals[i].OnlineId ==
                            syn.OnlineId
                        )
                        {
                            portals[i].ClientReset(syn);
                            break;
                        }
                    }
                }
            }
        }

        if (
            syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Vase
        )
        {
            if (LvItemManager.Instance != null)
            {
                LvItemManager.Instance.SynVase(syn);
            }
        }

        if (
            syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Card
        )
        {
            if (SeedBank.Instance != null)
            {
                SeedBank.Instance.SynDropCard(syn);
            }
        }
    }
}