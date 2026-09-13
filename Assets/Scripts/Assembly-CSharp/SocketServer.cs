using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using SocketSave;
using UnityEngine;
using UnityEngine.Events;

public class SocketServer : MonoBehaviour
{
    public static SocketServer Instance;

    private const int MaxPacketSize = 1048576;

    private Socket socketWatch;
    public bool isServerOpen;

    private readonly ConcurrentQueue<UnityAction> actions = new ConcurrentQueue<UnityAction>();
    private readonly List<PlayerInfo> players = new List<PlayerInfo>();
    private readonly List<Socket> sockets = new List<Socket>();
    private readonly Dictionary<Socket, PlayerInfo> socketPlayers = new Dictionary<Socket, PlayerInfo>();
    private readonly object socketListLock = new object();

    private PlayerInfo HostPlayer;
    private int ReConnectCode;
    private int itemId;

    private readonly List<PlayerInfo> ReConnectPlayer = new List<PlayerInfo>();
    private readonly List<PlayerInfo> HandQuitPlayer = new List<PlayerInfo>();

    public int noHostPlayerNum => players.Count;
    public int ItemId => ++itemId;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        while (actions.TryDequeue(out UnityAction action))
        {
            try { action?.Invoke(); }
            catch (Exception ex) { Debug.Log("El servidor desconectó activamente la conexión. " + ex); }
        }
    }

    private void Enqueue(UnityAction action)
    {
        if (action != null)
            actions.Enqueue(action);
    }

    private void ClearPlayerUI()
    {
        BattlePlayerList.Instance?.UpdatePlayerList(null, new List<PlayerInfo>());
        PlayerList.Instance?.UpdatePlayerList(null, new List<PlayerInfo>());
    }

    private void CloseSocketList()
    {
        Socket[] list;

        lock (socketListLock)
        {
            list = sockets.ToArray();
            sockets.Clear();
            socketPlayers.Clear();
        }

        for (int i = 0; i < list.Length; i++)
            SafeCloseSocket(list[i]);
    }

    private void ClearServerState(bool clearUI)
    {
        isServerOpen = false;

        if (GameManager.Instance != null)
            GameManager.Instance.isOnline = false;

        SafeCloseSocket(socketWatch);
        socketWatch = null;

        CloseSocketList();

        players.Clear();
        ReConnectPlayer.Clear();
        HandQuitPlayer.Clear();

        HostPlayer = null;
        ReConnectCode = 0;
        itemId = 0;

        while (actions.TryDequeue(out _)) { }

        if (clearUI)
            ClearPlayerUI();
    }

    public void StartServer(IPAddress ip, int port)
    {
        if (GameManager.Instance.isOnline)
            return;

        StopAllCoroutines();
        ClearServerState(true);

        try
        {
            socketWatch = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketWatch.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socketWatch.Bind(new IPEndPoint(ip, port));
            socketWatch.Listen(4);

            isServerOpen = true;

            Thread thread = new Thread(Recevice) { IsBackground = true };
            thread.Start(socketWatch);

            HostPlayer = new PlayerInfo
            {
                Name = GameManager.Instance.LocalPlayerSave.playerName,
                VersionCode = GameManager.Instance.VersionCode
            };

            GameManager.Instance.HostName = HostPlayer.Name;
            GameManager.Instance.isOnline = true;

            UpdatePlayerLists();

            StartCoroutine(SendHeartbeat());
            StartCoroutine(CheckConnect());

            Debug.Log("Servidor iniciado. Host: " + HostPlayer.Name +
                " | Versión: v" + GameManager.Instance.VersionCode);
        }
        catch (Exception ex)
        {
            ClearServerState(true);
            Debug.LogError("No se pudo iniciar el servidor: " + ex);
        }
    }

    private void Recevice(object obj)
    {
        Socket serverSocket = obj as Socket;
        Debug.Log("Inicio exitoso.");

        if (serverSocket == null)
            return;

        while (isServerOpen)
        {
            try
            {
                Socket client = serverSocket.Accept();

                if (!isServerOpen)
                {
                    SafeCloseSocket(client);
                    break;
                }

                ReceseMsgGoing(client);
                Debug.Log((client.RemoteEndPoint?.ToString() ?? "Unknown") + ":Conectando al servidor.");
            }
            catch (SocketException)
            {
                if (isServerOpen)
                    Debug.Log("Servidor socket cerrado o desconectado.");
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (isServerOpen)
                    Debug.LogError("Error aceptando conexión: " + ex);
            }
        }
    }

    private void ReceseMsgGoing(Socket TxSocket)
    {
        Thread thread = new Thread(() =>
        {
            PlayerInfo playerInfo = new PlayerInfo();
            byte[] remainingData = Array.Empty<byte>();

            try
            {
                while (isServerOpen)
                {
                    byte[] buffer = new byte[MaxPacketSize];
                    int received;

                    try
                    {
                        received = TxSocket.Receive(buffer);
                    }
                    catch (SocketException) { break; }
                    catch (ObjectDisposedException) { break; }

                    if (received <= 0)
                        break;

                    int offset = 0;

                    while (offset < received)
                    {
                        int available = received - offset;

                        if (remainingData.Length > 0)
                        {
                            byte[] combined = new byte[remainingData.Length + available];
                            Buffer.BlockCopy(remainingData, 0, combined, 0, remainingData.Length);
                            Buffer.BlockCopy(buffer, offset, combined, remainingData.Length, available);

                            int result = ProcessPacket(
                                combined,
                                combined.Length,
                                ref playerInfo,
                                TxSocket
                            );

                            if (result < 0)
                                return;

                            if (result == 0)
                            {
                                remainingData = combined;
                                break;
                            }

                            remainingData = Array.Empty<byte>();
                            offset += result - remainingData.Length;
                            continue;
                        }

                        if (available < 4)
                        {
                            remainingData = new byte[available];
                            Buffer.BlockCopy(buffer, offset, remainingData, 0, available);
                            break;
                        }

                        int packetLength = BitConverter.ToInt32(buffer, offset);

                        if (packetLength < 2 || packetLength > MaxPacketSize)
                        {
                            Debug.LogError("Paquete inválido: " + packetLength);
                            return;
                        }

                        int totalLength = packetLength + 4;

                        if (available < totalLength)
                        {
                            remainingData = new byte[available];
                            Buffer.BlockCopy(buffer, offset, remainingData, 0, available);
                            break;
                        }

                        if (!ProcessPacket(
                            buffer,
                            offset,
                            totalLength,
                            ref playerInfo,
                            TxSocket))
                        {
                            return;
                        }

                        offset += totalLength;
                    }
                }
            }
            catch (Exception ex)
            {
                if (isServerOpen)
                    Debug.Log("Error de conexión con el servidor: " + ex);
            }
            finally
            {
                RemoveSocket(TxSocket);

                if (playerInfo != null &&
                    !string.IsNullOrEmpty(playerInfo.Name) &&
                    GameManager.Instance != null &&
                    playerInfo.VersionCode == GameManager.Instance.VersionCode)
                {
                    PlayerInfo disconnected = playerInfo;
                    Enqueue(() => RemovePlayer(disconnected));
                }
            }
        })
        {
            IsBackground = true
        };

        thread.Start();
    }

    private int ProcessPacket(
        byte[] data,
        int length,
        ref PlayerInfo playerInfo,
        Socket socket)
    {
        if (length < 4)
            return 0;

        int packetLength = BitConverter.ToInt32(data, 0);

        if (packetLength < 2 || packetLength > MaxPacketSize)
            return -1;

        int totalLength = packetLength + 4;

        if (length < totalLength)
            return 0;

        if (!ProcessPacket(data, 0, totalLength, ref playerInfo, socket))
            return -1;

        return totalLength;
    }

    private bool ProcessPacket(
        byte[] data,
        int offset,
        int totalLength,
        ref PlayerInfo playerInfo,
        Socket socket)
    {
        if (totalLength < 6)
            return true;

        byte type1 = data[offset + 4];
        byte type2 = data[offset + 5];

        string msg = Encoding.UTF8.GetString(
            data,
            offset + 6,
            totalLength - 6
        );

        switch (type1)
        {
            case 0:
                return HandleConnectionMessage(
                    type2,
                    msg,
                    ref playerInfo,
                    socket
                );

            case 1:
                HandleGameMessage(type2, msg, playerInfo, socket);
                break;

            case 2:
                HandleChatMessage(type2, msg, playerInfo, socket);
                break;
        }

        return true;
    }

    private bool HandleConnectionMessage(
        byte type,
        string msg,
        ref PlayerInfo playerInfo,
        Socket socket)
    {
        switch (type)
        {
            case 1:
                PlayerInfo info;

                try
                {
                    info = JsonUtility.FromJson<PlayerInfo>(msg);
                }
                catch
                {
                    info = null;
                }

                if (!ValidatePlayer(info, socket))
                    return false;

                playerInfo = info;

                lock (socketListLock)
                {
                    if (!sockets.Contains(socket))
                        sockets.Add(socket);

                    socketPlayers[socket] = info;
                }

                PlayerInfo accepted = info;

                Enqueue(() =>
                {
                    accepted.Heartbeat = true;

                    if (CanReConnect(accepted))
                    {
                        players.Add(accepted);

                        for (int i = ReConnectPlayer.Count - 1; i >= 0; i--)
                        {
                            if (ReConnectPlayer[i]?.Name == accepted.Name)
                            {
                                ReConnectPlayer.RemoveAt(i);
                                break;
                            }
                        }

                        ReConnectListChange();
                    }
                    else
                    {
                        AddNewPlayer(accepted);
                        SendCommandBag(socket);
                        SynPvPMode(
                            new PvPModeSyn
                            {
                                Mode = PvPSelector.Instance.CurrMode
                            },
                            socket
                        );

                        PvPSelector.Instance.ServerSynTeam();
                        SpectatorList.Instance.ServerSynSpectList();
                    }
                });

                return true;

            case 2:
                HandQuitPlayer.Add(playerInfo);
                RemoveSocket(socket);
                return false;

            case byte.MaxValue:
                playerInfo.Heartbeat = true;
                return true;
        }

        return true;
    }

    private bool ValidatePlayer(PlayerInfo info, Socket socket)
    {
        if (info == null)
        {
            Reject(socket, "Información del jugador no válida.");
            return false;
        }

        if (info.VersionCode != GameManager.Instance.VersionCode)
        {
            Reject(
                socket,
                "Diferente a la versión del juego del servidor. Versión del juego del servidor :v" +
                GameManager.Instance.VersionCode
            );
            return false;
        }

        if (CanReConnect(info))
        {
            if (ReConnectCode != info.ReCntCode)
            {
                Reject(socket, "Error de código de reconexión.");
                return false;
            }

            return true;
        }

        string password =
            UIManager.Instance.HostPasswordInput.text;

        if (password != "" && info.Password != password)
        {
            Reject(socket, "Contraseña incorrecta; no se pudo unir.");
            return false;
        }

        if (players.Count >= 3)
        {
            Reject(socket, "Server lleno; error al unirse.");
            return false;
        }

        if (HostPlayer != null && HostPlayer.Name == info.Name)
        {
            Reject(socket, "El nombre entra en conflicto con el de un jugador en línea; no se pudo unir a la partida.");
            return false;
        }

        if (LVManager.Instance.InGame)
        {
            Reject(socket, "La partida ya ha comenzado; no se pudo unir.");
            return false;
        }

        if (info.CmdEnable != GameManager.Instance.LocalPlayerSave.CmdEnable)
        {
            Reject(
                socket,
                info.CmdEnable
                    ? "Has habilitado los comandos, pero el servidor no; no puedes unirte"
                    : "No has habilitado los comandos, pero el servidor sí; no puedes unirte"
            );
            return false;
        }

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i]?.Name == info.Name)
            {
                Reject(socket, "El nombre entra en conflicto con el de un jugador en línea; no se pudo unir a la partida.");
                return false;
            }
        }

        return true;
    }

    private void Reject(Socket socket, string message)
    {
        SendFailConnectMsg(message, socket);
        SafeCloseSocket(socket);
    }

    private void HandleGameMessage(
        byte type,
        string msg,
        PlayerInfo player,
        Socket socket)
    {
        switch (type)
        {
            case 0:
                Enqueue(() =>
                {
                    PlantSpawn spawn = JsonUtility.FromJson<PlantSpawn>(msg);
                    UpdateCardCD cd = new UpdateCardCD
                    {
                        CardId = spawn.CardId,
                        name = player.Name
                    };

                    if (LV.Instance.CurrLVType == LVType.PvP &&
                        !PvPSelector.Instance.IsSameTeam(player.Name))
                        spawn.GridPos = new Vector2(-spawn.GridPos.x, spawn.GridPos.y);

                    PlantBase plant =
                        PlantManager.Instance.GetNewPlant(spawn.plantType);

                    Grid grid =
                        MapManager.Instance.GetGridByWorldPos(spawn.GridPos);

                    if (SeedBank.Instance.CheckPlant(
                        plant, grid, -1, player.Name))
                    {
                        int needSun = -1;
                        cd.OK = false;

                        if (spawn.SPcode == 2)
                        {
                            plant.InitForCreate(false, null, false);
                            needSun = SeedBank.Instance
                                .GetPlantNc(plant.GetPlantType())
                                .NeedNum;
                        }

                        SeedBank.Instance.PlantConfirm(
                            plant,
                            grid,
                            needSun,
                            spawn.SPcode,
                            player.Name
                        );

                        SendJson(2, 4, cd);

                        BattlePlayerList.Instance.UpdateCardCD(
                            cd.name,
                            cd.CardId,
                            cd.OK
                        );
                    }
                    else
                    {
                        cd.OK = true;
                        SendJson(2, 4, cd, socket);
                        Destroy(plant.gameObject);
                    }

                    SeedBank.Instance.LikeColumnPlace(
                        grid,
                        spawn.plantType,
                        ZombieType.Nope,
                        -1,
                        player.Name,
                        false
                    );
                });
                break;

            case 1:
                Enqueue(() =>
                {
                    ToolApply apply =
                        JsonUtility.FromJson<ToolApply>(msg);

                    apply.User = player.Name;

                    Grid grid =
                        MapManager.Instance.GetGridByWorldPos(apply.GridPos);

                    if (apply.type == ToolType.Shovel)
                    {
                        if (Shovel.Instance.ClearPlant(
                            grid,
                            apply.GridPos,
                            player.Name))
                        {
                            BattlePlayerList.Instance.PlayShovelAnimation(
                                grid.Position,
                                apply.Sound,
                                player.Name
                            );
                        }

                        SendJson(2, 6, apply);
                    }
                    else if (apply.type == ToolType.Glove)
                    {
                        Glove.Instance.SynClient(
                            apply.OnlineId,
                            apply.GridPos
                        );
                    }
                });
                break;

            case 2:
                Enqueue(() =>
                    SkyManager.Instance.OnlineCollectSun(
                        JsonUtility.FromJson<ClickedSun>(msg)
                    )
                );
                break;

            case 3:
                Enqueue(() =>
                {
                    PlayerMap map =
                        JsonUtility.FromJson<PlayerMap>(msg);

                    map.PlayerName = player.Name;
                    ChangeMap(map);

                    BattlePlayerList.Instance.UpdateMapSprite(
                        map.PlayerName,
                        map.Pos
                    );
                });
                break;

            case 4:
                Enqueue(() =>
                {
                    SelectCard card =
                        JsonUtility.FromJson<SelectCard>(msg);

                    card.PlayerName = player.Name;
                    SelectCard(card);

                    if (card.isBack)
                        BattlePlayerList.Instance.CancelCard(
                            card.PlayerName,
                            card.cardId
                        );
                    else
                        BattlePlayerList.Instance.SelectCard(
                            card.PlayerName,
                            card.plantType,
                            card.zombieType,
                            card.noAnim
                        );
                });
                break;

            case 5:
                Enqueue(() =>
                {
                    SelectPrepare prepare =
                        JsonUtility.FromJson<SelectPrepare>(msg);

                    prepare.PlayerName = player.Name;
                    SelectPrepare(prepare);

                    BattlePlayerList.Instance.UpdateState(
                        prepare.PlayerName,
                        prepare.isPrepare
                    );
                });
                break;

            case 6:
                Enqueue(() =>
                    SynItem(JsonUtility.FromJson<SynItem>(msg))
                );
                break;

            case 7:
                Enqueue(() =>
                {
                    PlantPreview preview =
                        JsonUtility.FromJson<PlantPreview>(msg);

                    BattlePlayerList.Instance.PreviewPlant(preview);
                    PlacePreview(preview, socket);
                });
                break;

            case 8:
                Enqueue(() =>
                {
                    UpdateCardCD cd =
                        JsonUtility.FromJson<UpdateCardCD>(msg);

                    cd.name = player.Name;

                    BattlePlayerList.Instance.UpdateCardCD(
                        cd.name,
                        cd.CardId,
                        cd.OK
                    );

                    SendJson(2, 4, cd, socket, true);
                });
                break;

            case 9:
                Enqueue(() =>
                {
                    ShovelPreview preview =
                        JsonUtility.FromJson<ShovelPreview>(msg);

                    BattlePlayerList.Instance.PreviewShovel(
                        preview.PlayerName,
                        preview.GridPos,
                        preview.isShow
                    );

                    ShovelPreview(preview, socket);
                });
                break;

            case 10:
                Enqueue(() =>
                {
                    ZombieSpawnApply spawn =
                        JsonUtility.FromJson<ZombieSpawnApply>(msg);

                    UpdateCardCD cd = new UpdateCardCD
                    {
                        CardId = spawn.CardId,
                        name = player.Name
                    };

                    if (LV.Instance.CurrLVType == LVType.PvP &&
                        !PvPSelector.Instance.IsSameTeam(player.Name))
                        spawn.GridPos = new Vector2(-spawn.GridPos.x, spawn.GridPos.y);

                    ZombieBase zombie =
                        ZombieManager.Instance.GetNewZombie(spawn.Type);

                    Grid grid =
                        MapManager.Instance.GetGridByWorldPos(spawn.GridPos);

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
                            spawn.isRat
                        );

                        SendJson(2, 4, cd);

                        BattlePlayerList.Instance.UpdateCardCD(
                            cd.name,
                            cd.CardId,
                            cd.OK
                        );
                    }
                    else
                    {
                        cd.OK = true;
                        SendJson(2, 4, cd, socket);
                        Destroy(zombie.gameObject);
                    }

                    SeedBank.Instance.LikeColumnPlace(
                        grid,
                        PlantType.Nope,
                        spawn.Type,
                        -1,
                        player.Name,
                        spawn.isRat
                    );
                });
                break;

            case 11:
                Enqueue(() =>
                {
                    ZombiePreview preview =
                        JsonUtility.FromJson<ZombiePreview>(msg);

                    BattlePlayerList.Instance.PreviewZombie(preview);
                    ZombiePreview(preview, socket);
                });
                break;

            case 12:
                Enqueue(() =>
                {
                    JoinTeamApply join =
                        JsonUtility.FromJson<JoinTeamApply>(msg);

                    if (join.isRed)
                        PvPSelector.Instance.JoinRed(player.Name);
                    else
                        PvPSelector.Instance.JoinBlue(player.Name);
                });
                break;

            case 13:
                Enqueue(() =>
                {
                    JoinSpecApply join =
                        JsonUtility.FromJson<JoinSpecApply>(msg);

                    SpectatorList.Instance.ClientJoinSpect(
                        player.Name,
                        join.isJoin
                    );
                });
                break;

            case 14:
                Enqueue(() =>
                    SlotMachine.Instance.ClientSyn(
                        JsonUtility.FromJson<SlotMchBag>(msg)
                    )
                );
                break;
        }
    }

    private void HandleChatMessage(
        byte type,
        string msg,
        PlayerInfo player,
        Socket socket)
    {
        switch (type)
        {
            case 0:
                Enqueue(() =>
                {
                    ChatInput.Instance.AddMessage(msg);
                    SendMsg(4, 0, msg, socket, true);
                });
                break;

            case 1:
                Enqueue(() =>
                {
                    PrivateChatMsg chat =
                        JsonUtility.FromJson<PrivateChatMsg>(msg);

                    if (chat.PlayerName ==
                        GameManager.Instance.LocalPlayerSave.playerName)
                    {
                        ChatInput.Instance.AddMessage(
                            "Jugador" + player.Name +
                            "Susurro:" + chat.content,
                            new Color32(123, 123, 123, byte.MaxValue)
                        );
                    }
                    else
                    {
                        SendPrivateChatMsg(
                            chat.PlayerName,
                            chat.content,
                            player.Name
                        );
                    }
                });
                break;
        }
    }

    private void RemoveSocket(Socket socket)
    {
        if (socket == null)
            return;

        lock (socketListLock)
        {
            sockets.Remove(socket);
            socketPlayers.Remove(socket);
        }

        SafeCloseSocket(socket);
    }

    private void SafeCloseSocket(Socket socket)
    {
        if (socket == null)
            return;

        try { socket.Shutdown(SocketShutdown.Both); } catch { }
        try { socket.Close(); } catch { }
        try { socket.Dispose(); } catch { }
    }

    private void SendMsg(
        byte type1,
        byte type2,
        string content = "",
        Socket socket = null,
        bool OutThis = false)
    {
        byte[] payload =
            Encoding.UTF8.GetBytes(content ?? "");

        byte[] buffer =
            new byte[payload.Length + 6];

        Buffer.BlockCopy(
            BitConverter.GetBytes(payload.Length + 2),
            0,
            buffer,
            0,
            4
        );

        buffer[4] = type1;
        buffer[5] = type2;

        if (payload.Length > 0)
            Buffer.BlockCopy(payload, 0, buffer, 6, payload.Length);

        Socket[] targets;

        lock (socketListLock)
            targets = sockets.ToArray();

        if (socket == null)
        {
            for (int i = 0; i < targets.Length; i++)
                SendToSocket(targets[i], buffer);

            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (!OutThis && targets[i] == socket)
            {
                SendToSocket(socket, buffer);
                return;
            }

            if (OutThis && targets[i] != null && targets[i] != socket)
                SendToSocket(targets[i], buffer);
        }
    }

    private void SendJson(
        byte type1,
        byte type2,
        object value,
        Socket socket = null,
        bool OutThis = false)
    {
        SendMsg(
            type1,
            type2,
            JsonUtility.ToJson(value),
            socket,
            OutThis
        );
    }

    private void SendJson(
        byte type1,
        byte type2,
        object value,
        string playerName)
    {
        Socket socket = GetSocketByPlayer(playerName);

        if (socket != null)
            SendJson(type1, type2, value, socket);
    }

    private void SendToSocket(Socket socket, byte[] buffer)
    {
        if (socket == null || buffer == null)
            return;

        try
        {
            int sent = 0;

            while (sent < buffer.Length)
            {
                int count = socket.Send(
                    buffer,
                    sent,
                    buffer.Length - sent,
                    SocketFlags.None
                );

                if (count <= 0)
                    throw new SocketException();

                sent += count;
            }
        }
        catch
        {
            PlayerInfo disconnected = GetPlayerBySocket(socket);
            RemoveSocket(socket);

            if (disconnected != null)
                Enqueue(() => RemovePlayer(disconnected));
        }
    }

    private PlayerInfo GetPlayerBySocket(Socket socket)
    {
        if (socket == null)
            return null;

        lock (socketListLock)
        {
            socketPlayers.TryGetValue(
                socket,
                out PlayerInfo player
            );

            return player;
        }
    }

    private Socket GetSocketByPlayer(string playerName)
    {
        if (string.IsNullOrEmpty(playerName))
            return null;

        lock (socketListLock)
        {
            foreach (var pair in socketPlayers)
            {
                if (pair.Value?.Name == playerName)
                    return pair.Key;
            }
        }

        return null;
    }

    private void SendMsg(
        byte type1,
        byte type2,
        string content,
        string playerName)
    {
        Socket socket = GetSocketByPlayer(playerName);

        if (socket != null)
            SendMsg(type1, type2, content, socket);
    }

    private IEnumerator SendHeartbeat()
    {
        float timer = Time.realtimeSinceStartup;

        while (isServerOpen && GameManager.Instance.isOnline)
        {
            yield return null;

            if (Time.realtimeSinceStartup - timer > 0.5f)
            {
                SendMsg(0, byte.MaxValue);
                timer = Time.realtimeSinceStartup;
            }
        }
    }

    private IEnumerator CheckConnect()
    {
        float timer = Time.realtimeSinceStartup;

        while (isServerOpen && GameManager.Instance.isOnline)
        {
            yield return null;

            if (Time.realtimeSinceStartup - timer <= 3f)
                continue;

            for (int i = players.Count - 1; i >= 0; i--)
            {
                PlayerInfo player = players[i];

                if (player == null)
                {
                    players.RemoveAt(i);
                    continue;
                }

                if (!player.Heartbeat)
                {
                    Socket socket = GetSocketByPlayer(player.Name);

                    if (socket != null)
                        RemoveSocket(socket);

                    Debug.LogError(player.Name + "Desconexión por tiempo de espera");
                    RemovePlayer(player);
                }
                else
                {
                    player.Heartbeat = false;
                }
            }

            timer = Time.realtimeSinceStartup;
        }
    }

    public void CloseServer()
    {
        StopAllCoroutines();
        ClearServerState(true);
        Debug.Log("Servidor cerrado y estado limpiado.");
    }

    private void OnApplicationQuit()
    {
        try
        {
            StopAllCoroutines();
            ClearServerState(false);
        }
        catch { }
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        try
        {
            StopAllCoroutines();
            ClearServerState(false);
        }
        catch { }

        Instance = null;
    }

    public void SynItem(SynItem syn)
    {
        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Plant)
        {
            PlantBase plant =
                PlantManager.Instance.OnlineGetPlant(syn.OnlineId);

            plant?.OnlineSynPlant(syn);
        }

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Zombie)
        {
            ZombieBase zombie =
                ZombieManager.Instance.OnlineGetZombie(syn.OnlineId);

            zombie?.OnlineSynZombie(syn);
        }

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Puddle)
        {
            List<Puddle> puddles = MapManager.Instance.puddles;

            for (int i = 0; i < puddles.Count; i++)
            {
                if (puddles[i].OnlineId == syn.OnlineId)
                {
                    puddles[i].StartDisappear();
                    break;
                }
            }
        }

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Portal)
        {
            List<PortalController> portals = MapManager.Instance.portalCs;

            for (int i = 0; i < portals.Count; i++)
            {
                if (portals[i].OnlineId == syn.OnlineId)
                {
                    portals[i].ClientReset(syn);
                    break;
                }
            }
        }

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Vase)
            LvItemManager.Instance.SynVase(syn);

        if (syn.Type == SynItemType.AllItem ||
            syn.Type == SynItemType.Card)
            SeedBank.Instance.SynDropCard(syn);
    }

    public List<string> GetAllPlayerNameList()
    {
        List<string> names = new List<string>();

        if (HostPlayer != null)
            names.Add(HostPlayer.Name);

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] != null)
                names.Add(players[i].Name);
        }

        return names;
    }

    public void SendFailConnectMsg(string msg, Socket socket)
    {
        SendJson(
            0,
            1,
            new ConnectInfo { msg = msg },
            socket
        );
    }

    public void SendHostCD(int CardID, bool isOK)
    {
        SendJson(
            2,
            4,
            new UpdateCardCD
            {
                name = GameManager.Instance.LocalPlayerSave.playerName,
                CardId = CardID,
                OK = isOK
            }
        );
    }

    public void SendChatMsg(string content)
    {
        SendMsg(4, 0, content);
    }

    public void SendPrivateChatMsg(
        string name,
        string content,
        string sender)
    {
        SendJson(
            4,
            2,
            new PrivateChatMsg
            {
                PlayerName = sender,
                content = content
            },
            name
        );
    }

    public void KickPlayer(string name)
    {
        PlayerInfo player =
            players.FirstOrDefault(p => p?.Name == name);

        if (player == null)
            return;

        HandQuitPlayer.Add(player);

        Socket socket = GetSocketByPlayer(name);

        if (socket != null)
        {
            SendFailConnectMsg("Has sido expulsado de la partida", socket);
            SafeCloseSocket(socket);
        }
    }

    public void LoadLv(LoadLVBag loadLV)
    {
        ReConnectCode =
            UnityEngine.Random.Range(100000, 999999);

        loadLV.ReCntCode = ReConnectCode;

        Debug.Log(JsonUtility.ToJson(loadLV));
        SendJson(1, 0, loadLV);
    }

    public void SendAddCard(
        AddCardBag cardBag,
        string playerName)
    {
        SendJson(1, 12, cardBag, playerName);
    }

    public void StartRunLv()
    {
        SendMsg(1, 1);
    }

    public void BigWaveComing(WaveComing bigWave)
    {
        SendJson(1, 2, bigWave);
    }

    public void UpdateSunNum(SunNumBag SunNum)
    {
        SendJson(1, 3, SunNum);
    }

    public void SendSynBag(SynItem syn)
    {
        SendJson(1, 4, syn);
    }

    public void ChangeMap(PlayerMap map)
    {
        SendJson(1, 5, map);
    }

    public void SelectCard(SelectCard card)
    {
        SendJson(1, 6, card);
    }

    public void SelectPrepare(SelectPrepare prepare)
    {
        SendJson(1, 7, prepare);
    }

    public void GameOver(GameOver over)
    {
        SendJson(1, 8, over);
    }

    public void SynTeamList(PvPTeamList over)
    {
        SendJson(1, 9, over);
    }

    public void SynPvPMode(
        PvPModeSyn syn,
        Socket socket = null)
    {
        SendJson(1, 10, syn, socket);
    }

    public void SynSpectList(SpectList over)
    {
        SendJson(1, 11, over);
    }

    public void SynFlagMeter(FlagMeterSyn syn)
    {
        SendJson(1, 13, syn);
    }

    public void SynTimeTable(TimetableSyn syn)
    {
        SendJson(1, 14, syn);
    }

    public void SpawnSun(SunSpawn spawn)
    {
        SendJson(2, 1, spawn);
    }

    public void ClickedSun(ClickedSun sun)
    {
        SendJson(2, 2, sun);
    }

    public void SpawnPlant(PlantSpawn spawn)
    {
        SendJson(2, 0, spawn);
    }

    public void PlacePreview(
        PlantPreview spawn,
        Socket socket)
    {
        SendJson(2, 3, spawn, socket, true);
    }

    public void ZombiePreview(
        ZombiePreview spawn,
        Socket socket)
    {
        SendJson(3, 5, spawn, socket, true);
    }

    public void ShovelPreview(
        ShovelPreview spawn,
        Socket socket)
    {
        SendJson(2, 5, spawn, socket, true);
    }

    public void SpawnZombie(ZombieSpawn spawn)
    {
        SendJson(3, 0, spawn);
    }

    public void SpawnGraveStone(GraveStoneSpawn spawn)
    {
        SendJson(3, 1, spawn);
    }

    public void SpawnPuddle(PuddleSpawn spawn)
    {
        SendJson(3, 2, spawn);
    }

    public void SpawnPortal(PortalSpawn spawn)
    {
        SendJson(3, 6, spawn);
    }

    public void SpawnVase(VaseSpawn spawn)
    {
        SendJson(3, 9, spawn);
    }

    public void SpawnDropCard(CardSpawn spawn)
    {
        SendJson(3, 10, spawn);
    }

    public void SpawnMelt(MeltSpawn spawn)
    {
        SendJson(3, 11, spawn);
    }

    public void SpawnFallHail(FallHailSpawn spawn)
    {
        SendJson(3, 12, spawn);
    }

    public void SpawnLightning(LightingSpawn spawn)
    {
        SendJson(3, 3, spawn);
    }

    public void SendShovelAnim(ToolApply apply)
    {
        SendJson(2, 6, apply);
    }

    public void SendGridState(SynGrid apply)
    {
        SendJson(3, 7, apply);
    }

    public void SendCommandBag(Socket socket = null)
    {
        SendJson(
            4,
            3,
            new CommandBag
            {
                Pinv = PlantManager.Instance.PlantInvincible,
                Zinv = ZombieManager.Instance.ZombieInvincible,
                DLiCy = SkyManager.Instance.DayLightCycle,
                SnInf = PlayerManager.Instance.SunInfinite,
                CdCle = SeedBank.Instance.isNoCD,
                ZomStop = ZombieManager.Instance.ZombieDontMove,
                VaseXray = LvItemManager.Instance.VaseAlwaysLight
            },
            socket
        );
    }

    public void SendWeatherCmd(WeatherChange cmd)
    {
        SendJson(4, 4, cmd);
    }

    public void SendTimeCmd(TimeCmd cmd)
    {
        SendJson(4, 5, cmd);
    }

    public void SendMapSyn(SynMap cmd)
    {
        SendJson(3, 4, cmd);
    }

    public void SendSynBooty(SynBooty Syn)
    {
        SendJson(3, 8, Syn);
    }

    public void SendAcvmentGet(
        Acvname acvname,
        string playerName)
    {
        SendJson(
            4,
            6,
            new GetAcvment { acv = acvname },
            playerName
        );
    }

    private void SendWaitReConnect(bool isWait)
    {
        List<string> names = new List<string>();

        for (int i = 0; i < ReConnectPlayer.Count; i++)
        {
            if (ReConnectPlayer[i] != null)
                names.Add(ReConnectPlayer[i].Name);
        }

        SendJson(
            0,
            3,
            new ReConnectInfo
            {
                isWait = isWait,
                names = names
            }
        );
    }

    private void ReConnectListChange()
    {
        if (ReConnectPlayer.Count > 0)
        {
            List<string> names = new List<string>();

            for (int i = 0; i < ReConnectPlayer.Count; i++)
            {
                if (ReConnectPlayer[i] != null)
                    names.Add(ReConnectPlayer[i].Name);
            }

            ReConnect.Instance.LoadPlayerList(names);
            SendWaitReConnect(true);
        }
        else
        {
            SendWaitReConnect(false);
            ReConnect.Instance.OverClose();
        }
    }

    private bool CanReConnect(PlayerInfo info)
    {
        if (info == null)
            return false;

        for (int i = 0; i < ReConnectPlayer.Count; i++)
        {
            if (ReConnectPlayer[i]?.Name == info.Name)
                return true;
        }

        return false;
    }

    public void GiveUpReConnect()
    {
        for (int i = 0; i < ReConnectPlayer.Count; i++)
            RemoveDone(ReConnectPlayer[i]);

        ReConnectPlayer.Clear();
        ReConnectListChange();
    }

    private void AddNewPlayer(PlayerInfo player)
    {
        if (player == null ||
            players.Any(p => p?.Name == player.Name))
            return;

        players.Add(player);
        UpdatePlayerLists();

        string content = player.Name + "Te has unido a la partida";

        ChatInput.Instance.AddMessage(
            content,
            new Color32(
                byte.MaxValue,
                byte.MaxValue,
                0,
                byte.MaxValue
            )
        );

        SendMsg(4, 1, content);

        SendJson(
            0,
            2,
            new OnlinePlayerInfo
            {
                HostPlayer = HostPlayer,
                players = players
            }
        );
    }

    private void RemovePlayer(PlayerInfo player)
    {
        if (player == null)
            return;

        int index = players.IndexOf(player);

        if (index < 0)
        {
            index = players.FindIndex(
                p => p?.Name == player.Name
            );

            if (index >= 0)
                player = players[index];
        }

        if (index < 0)
            return;

        players.RemoveAt(index);

        if (LVManager.Instance.InGame)
        {
            if (HandQuitPlayer.Remove(player))
            {
                RemoveDone(player);
                return;
            }

            if (!ReConnectPlayer.Contains(player))
                ReConnectPlayer.Add(player);

            ReConnect.Instance.OpenInit(false);
            ReConnectListChange();
        }
        else
        {
            RemoveDone(player);
        }
    }

    private void RemoveDone(PlayerInfo player)
    {
        if (player == null)
            return;

        string content = player.Name + "Saliste del juego";

        ChatInput.Instance.AddMessage(
            content,
            new Color32(
                byte.MaxValue,
                byte.MaxValue,
                0,
                byte.MaxValue
            )
        );

        SendMsg(4, 1, content);
        UpdatePlayerLists();

        PvPSelector.Instance.ClearQuitPlayer(player.Name);
        SpectatorList.Instance.ClearPlayer(player.Name);

        SendJson(
            0,
            2,
            new OnlinePlayerInfo
            {
                HostPlayer = HostPlayer,
                players = players
            }
        );
    }

    private void UpdatePlayerLists()
    {
        BattlePlayerList.Instance.UpdatePlayerList(
            HostPlayer,
            players
        );

        PlayerList.Instance.UpdatePlayerList(
            HostPlayer,
            players
        );
    }
}