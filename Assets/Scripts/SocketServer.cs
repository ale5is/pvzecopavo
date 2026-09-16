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

public class SocketServer : MonoBehaviour
{
    public static SocketServer Instance;

    const int MaxPacketSize = 1048576;

    [Header("UI")]
    [SerializeField] BattlePlayerList battlePlayerList;
    [SerializeField] PlayerList playerList;

    Socket socketWatch;
    public bool isServerOpen;

    readonly ConcurrentQueue<Action> actions = new();
    readonly List<PlayerInfo> players = new();
    readonly List<Socket> sockets = new();
    readonly Dictionary<Socket, PlayerInfo> socketPlayers = new();
    readonly object socketLock = new();

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

    void Update()
    {
        while (actions.TryDequeue(out var action))
        {
            try { action?.Invoke(); }
            catch (Exception e) { Debug.Log("El servidor desconectó activamente la conexión. " + e); }
        }
    }

    void Enqueue(Action action)
    {
        if (action != null)
            actions.Enqueue(action);
    }

    void ClearPlayerUI()
    {
        battlePlayerList?.UpdatePlayerList(null, new List<PlayerInfo>());
        playerList?.UpdatePlayerList(null, new List<PlayerInfo>());
    }

    void CloseSocketList()
    {
        Socket[] list;
        lock (socketLock)
        {
            list = sockets.ToArray();
            sockets.Clear();
            socketPlayers.Clear();
        }

        foreach (var socket in list)
            SafeCloseSocket(socket);
    }

    void ClearServerState(bool clearUI)
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
        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("No se pudo iniciar el servidor: GameManager.Instance es null.");
            return;
        }

        if (gm.isOnline)
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

            var thread = new Thread(Recevice) { IsBackground = true };
            thread.Start(socketWatch);

            string name = gm.LocalPlayerSave?.playerName ?? "Host";

            HostPlayer = new PlayerInfo
            {
                Name = name,
                VersionCode = gm.VersionCode
            };

            gm.HostName = name;
            gm.isOnline = true;

            UpdatePlayerLists();
            StartCoroutine(SendHeartbeat());
            StartCoroutine(CheckConnect());

            Debug.Log($"Servidor iniciado. Host: {name} | Versión: v{gm.VersionCode}");
        }
        catch (Exception e)
        {
            ClearServerState(true);
            Debug.LogError("No se pudo iniciar el servidor: " + e);
        }
    }

    void Recevice(object obj)
    {
        if (obj is not Socket serverSocket)
            return;

        Debug.Log("Inicio exitoso.");

        while (isServerOpen)
        {
            try
            {
                var client = serverSocket.Accept();

                if (!isServerOpen)
                {
                    SafeCloseSocket(client);
                    break;
                }

                ReceseMsgGoing(client);
                Debug.Log($"{client.RemoteEndPoint?.ToString() ?? "Unknown"}:Conectando al servidor.");
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
            catch (Exception e)
            {
                if (isServerOpen)
                    Debug.LogError("Error aceptando conexión: " + e);
            }
        }
    }

    void ReceseMsgGoing(Socket socket)
    {
        var thread = new Thread(() =>
        {
            PlayerInfo player = null;
            var buffer = new byte[65536];
            var pending = new byte[MaxPacketSize + 65536 + 4];
            int pendingCount = 0;

            try
            {
                while (isServerOpen)
                {
                    int received;

                    try
                    {
                        received = socket.Receive(buffer);
                    }
                    catch (SocketException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    if (received <= 0)
                        break;

                    if (pendingCount + received > pending.Length)
                    {
                        Debug.LogError("Los datos recibidos son demasiado grandes.");
                        break;
                    }

                    Buffer.BlockCopy(buffer, 0, pending, pendingCount, received);
                    pendingCount += received;

                    int offset = 0;

                    while (pendingCount - offset >= 4)
                    {
                        int length = BitConverter.ToInt32(pending, offset);

                        if (length < 2 || length > MaxPacketSize)
                        {
                            Debug.LogError("Paquete inválido: " + length);
                            return;
                        }

                        int total = length + 4;

                        if (pendingCount - offset < total)
                            break;

                        if (!ProcessPacket(pending, offset, total, ref player, socket))
                            return;

                        offset += total;
                    }

                    if (offset > 0)
                    {
                        int left = pendingCount - offset;
                        if (left > 0)
                            Buffer.BlockCopy(pending, offset, pending, 0, left);
                        pendingCount = left;
                    }
                }
            }
            catch (Exception e)
            {
                if (isServerOpen)
                    Debug.Log("Error de conexión con el servidor: " + e);
            }
            finally
            {
                RemoveSocket(socket);

                if (player != null &&
                    !string.IsNullOrEmpty(player.Name) &&
                    GameManager.Instance != null &&
                    player.VersionCode == GameManager.Instance.VersionCode)
                {
                    var disconnected = player;
                    Enqueue(() => RemovePlayer(disconnected));
                }
            }
        })
        { IsBackground = true };

        thread.Start();
    }

    bool ProcessPacket(byte[] data, int offset, int totalLength, ref PlayerInfo player, Socket socket)
    {
        if (totalLength < 6)
            return true;

        byte type1 = data[offset + 4];
        byte type2 = data[offset + 5];
        string msg = Encoding.UTF8.GetString(data, offset + 6, totalLength - 6);

        switch (type1)
        {
            case 0:
                return HandleConnectionMessage(type2, msg, ref player, socket);
            case 1:
                HandleGameMessage(type2, msg, player, socket);
                break;
            case 2:
                HandleChatMessage(type2, msg, player, socket);
                break;
        }

        return true;
    }

    bool HandleConnectionMessage(byte type, string msg, ref PlayerInfo player, Socket socket)
    {
        switch (type)
        {
            case 1:
                PlayerInfo info;

                try { info = JsonUtility.FromJson<PlayerInfo>(msg); }
                catch { info = null; }

                if (!ValidatePlayer(info, socket))
                    return false;

                player = info;

                lock (socketLock)
                {
                    if (!sockets.Contains(socket))
                        sockets.Add(socket);
                    socketPlayers[socket] = info;
                }

                var accepted = info;

                Enqueue(() =>
                {
                    accepted.Heartbeat = true;

                    if (CanReConnect(accepted))
                    {
                        players.Add(accepted);
                        ReConnectPlayer.RemoveAll(p => p?.Name == accepted.Name);
                        ReConnectListChange();
                        return;
                    }

                    AddNewPlayer(accepted);
                    SendCommandBag(socket);

                    if (PvPSelector.Instance != null)
                    {
                        SynPvPMode(new PvPModeSyn
                        {
                            Mode = PvPSelector.Instance.CurrMode
                        }, socket);

                        PvPSelector.Instance.ServerSynTeam();
                    }

                    SpectatorList.Instance?.ServerSynSpectList();
                });

                return true;

            case 2:
                if (player != null)
                    HandQuitPlayer.Add(player);

                RemoveSocket(socket);
                return false;

            case byte.MaxValue:
                if (player != null)
                    player.Heartbeat = true;
                break;
        }

        return true;
    }

    bool ValidatePlayer(PlayerInfo info, Socket socket)
    {
        if (info == null)
            return Reject(socket, "Información del jugador no válida.");

        var gm = GameManager.Instance;
        if (gm == null)
            return Reject(socket, "GameManager no está disponible.");

        if (info.VersionCode != gm.VersionCode)
            return Reject(socket, "Diferente a la versión del juego del servidor. Versión del juego del servidor :v" + gm.VersionCode);

        if (CanReConnect(info))
            return info.ReCntCode == ReConnectCode
                ? true
                : Reject(socket, "Error de código de reconexión.");

        var ui = UIManager.Instance;
        if (ui?.HostPasswordInput == null)
            return Reject(socket, "La interfaz del servidor no está disponible.");

        if (!string.IsNullOrEmpty(ui.HostPasswordInput.text) &&
            info.Password != ui.HostPasswordInput.text)
            return Reject(socket, "Contraseña incorrecta; no se pudo unir.");

        if (players.Count >= 3)
            return Reject(socket, "Server lleno; error al unirse.");

        if (HostPlayer?.Name == info.Name)
            return Reject(socket, "El nombre entra en conflicto con el de un jugador en línea; no se pudo unir a la partida.");

        if (LVManager.Instance?.InGame == true)
            return Reject(socket, "La partida ya ha comenzado; no se pudo unir.");

        if (gm.LocalPlayerSave == null)
            return Reject(socket, "No se pudo cargar la información del jugador.");

        if (info.CmdEnable != gm.LocalPlayerSave.CmdEnable)
            return Reject(socket, info.CmdEnable
                ? "Has habilitado los comandos, pero el servidor no; no puedes unirte"
                : "No has habilitado los comandos, pero el servidor sí; no puedes unirte");

        if (players.Any(p => p?.Name == info.Name))
            return Reject(socket, "El nombre entra en conflicto con el de un jugador en línea; no se pudo unir a la partida.");

        return true;
    }

    bool Reject(Socket socket, string message)
    {
        SendFailConnectMsg(message, socket);
        SafeCloseSocket(socket);
        return false;
    }

    void HandleGameMessage(byte type, string msg, PlayerInfo player, Socket socket)
    {
        Enqueue(() => ProcessGameMessage(type, msg, player, socket));
    }

    void ProcessGameMessage(byte type, string msg, PlayerInfo player, Socket socket)
    {
        switch (type)
        {
            case 0:
                PlacePlant(JsonUtility.FromJson<PlantSpawn>(msg), player, socket);
                break;
            case 1:
                ApplyTool(JsonUtility.FromJson<ToolApply>(msg), player, socket);
                break;
            case 2:
                if (SkyManager.Instance != null)
                    SkyManager.Instance.OnlineCollectSun(JsonUtility.FromJson<ClickedSun>(msg));
                break;
            case 3:
                ChangeMap(JsonUtility.FromJson<PlayerMap>(msg), player);
                break;
            case 4:
                SelectCard(JsonUtility.FromJson<SelectCard>(msg), player);
                break;
            case 5:
                SelectPrepare(JsonUtility.FromJson<SelectPrepare>(msg), player);
                break;
            case 6:
                var item = JsonUtility.FromJson<SynItem>(msg);
                if (item != null) SynItem(item);
                break;
            case 7:
                var pp = JsonUtility.FromJson<PlantPreview>(msg);
                if (pp != null)
                {
                    BattlePlayerList.Instance?.PreviewPlant(pp);
                    PlacePreview(pp, socket);
                }
                break;
            case 8:
                UpdateCD(JsonUtility.FromJson<UpdateCardCD>(msg), player, socket);
                break;
            case 9:
                var sp = JsonUtility.FromJson<ShovelPreview>(msg);
                if (sp != null)
                {
                    BattlePlayerList.Instance?.PreviewShovel(sp.PlayerName, sp.GridPos, sp.isShow);
                    ShovelPreview(sp, socket);
                }
                break;
            case 10:
                PlaceZombie(JsonUtility.FromJson<ZombieSpawnApply>(msg), player, socket);
                break;
            case 11:
                var zp = JsonUtility.FromJson<ZombiePreview>(msg);
                if (zp != null)
                {
                    BattlePlayerList.Instance?.PreviewZombie(zp);
                    ZombiePreview(zp, socket);
                }
                break;
            case 12:
                var team = JsonUtility.FromJson<JoinTeamApply>(msg);
                if (team != null && player != null && PvPSelector.Instance != null)
                {
                    if (team.isRed) PvPSelector.Instance.JoinRed(player.Name);
                    else PvPSelector.Instance.JoinBlue(player.Name);
                }
                break;
            case 13:
                var spect = JsonUtility.FromJson<JoinSpecApply>(msg);
                if (spect != null && player != null && SpectatorList.Instance != null)
                    SpectatorList.Instance.ClientJoinSpect(player.Name, spect.isJoin);
                break;
            case 14:
                var bag = JsonUtility.FromJson<SlotMchBag>(msg);
                if (bag != null) SlotMachine.Instance?.ClientSyn(bag);
                break;
        }
    }

    void PlacePlant(PlantSpawn spawn, PlayerInfo player, Socket socket)
    {
        if (spawn == null || player == null ||
            PlantManager.Instance == null ||
            MapManager.Instance == null ||
            SeedBank.Instance == null)
            return;

        var cd = new UpdateCardCD { CardId = spawn.CardId, name = player.Name };

        ReversePvP(ref spawn.GridPos, player.Name);

        var plant = PlantManager.Instance.GetNewPlant(spawn.plantType);
        var grid = MapManager.Instance.GetGridByWorldPos(spawn.GridPos);

        if (plant == null || grid == null)
            return;

        if (SeedBank.Instance.CheckPlant(plant, grid, -1, player.Name))
        {
            int needSun = -1;
            cd.OK = false;

            if (spawn.SPcode == 2)
            {
                plant.InitForCreate(false, null, false);
                needSun = SeedBank.Instance.GetPlantNc(plant.GetPlantType()).NeedNum;
            }

            SeedBank.Instance.PlantConfirm(plant, grid, needSun, spawn.SPcode, player.Name);
            SendJson(2, 4, cd);
            BattlePlayerList.Instance?.UpdateCardCD(cd.name, cd.CardId, cd.OK);
        }
        else
        {
            cd.OK = true;
            SendJson(2, 4, cd, socket);
            Destroy(plant.gameObject);
        }

        SeedBank.Instance.LikeColumnPlace(grid, spawn.plantType, ZombieType.Nope, -1, player.Name, false);
    }

    void PlaceZombie(ZombieSpawnApply spawn, PlayerInfo player, Socket socket)
    {
        if (spawn == null || player == null ||
            ZombieManager.Instance == null ||
            MapManager.Instance == null ||
            SeedBank.Instance == null)
            return;

        var cd = new UpdateCardCD { CardId = spawn.CardId, name = player.Name };

        ReversePvP(ref spawn.GridPos, player.Name);

        var zombie = ZombieManager.Instance.GetNewZombie(spawn.Type);
        var grid = MapManager.Instance.GetGridByWorldPos(spawn.GridPos);

        if (zombie == null || grid == null)
            return;

        if (SeedBank.Instance.CheckZombie(spawn.Type, grid, -1, player.Name))
        {
            cd.OK = false;
            SeedBank.Instance.ZombieConfirm(spawn.Type, zombie, grid, -1, player.Name, spawn.isRat);
            SendJson(2, 4, cd);
            BattlePlayerList.Instance?.UpdateCardCD(cd.name, cd.CardId, cd.OK);
        }
        else
        {
            cd.OK = true;
            SendJson(2, 4, cd, socket);
            Destroy(zombie.gameObject);
        }

        SeedBank.Instance.LikeColumnPlace(grid, PlantType.Nope, spawn.Type, -1, player.Name, spawn.isRat);
    }

    void ReversePvP(ref Vector2 pos, string player)
    {
        if (LV.Instance?.CurrLVType == LVType.PvP &&
            PvPSelector.Instance != null &&
            !PvPSelector.Instance.IsSameTeam(player))
            pos.x = -pos.x;
    }

    void ApplyTool(ToolApply apply, PlayerInfo player, Socket socket)
    {
        if (apply == null || player == null || MapManager.Instance == null)
            return;

        apply.User = player.Name;
        var grid = MapManager.Instance.GetGridByWorldPos(apply.GridPos);
        if (grid == null)
            return;

        if (apply.type == ToolType.Shovel)
        {
            if (Shovel.Instance?.ClearPlant(grid, apply.GridPos, player.Name) == true)
                BattlePlayerList.Instance?.PlayShovelAnimation(grid.Position, apply.Sound, player.Name);

            SendJson(2, 6, apply);
        }
        else if (apply.type == ToolType.Glove)
        {
            Glove.Instance?.SynClient(apply.OnlineId, apply.GridPos);
        }
    }

    void ChangeMap(PlayerMap map, PlayerInfo player)
    {
        if (map == null || player == null)
            return;

        map.PlayerName = player.Name;
        ChangeMap(map);
        BattlePlayerList.Instance?.UpdateMapSprite(map.PlayerName, map.Pos);
    }

    void SelectCard(SelectCard card, PlayerInfo player)
    {
        if (card == null || player == null)
            return;

        card.PlayerName = player.Name;
        SelectCard(card);

        if (BattlePlayerList.Instance != null)
        {
            if (card.isBack)
                BattlePlayerList.Instance.CancelCard(card.PlayerName, card.cardId);
            else
                BattlePlayerList.Instance.SelectCard(card.PlayerName, card.plantType, card.zombieType, card.noAnim);
        }
    }

    void SelectPrepare(SelectPrepare prepare, PlayerInfo player)
    {
        if (prepare == null || player == null)
            return;

        prepare.PlayerName = player.Name;
        SelectPrepare(prepare);
        BattlePlayerList.Instance?.UpdateState(prepare.PlayerName, prepare.isPrepare);
    }

    void UpdateCD(UpdateCardCD cd, PlayerInfo player, Socket socket)
    {
        if (cd == null || player == null)
            return;

        cd.name = player.Name;
        BattlePlayerList.Instance?.UpdateCardCD(cd.name, cd.CardId, cd.OK);
        SendJson(2, 4, cd, socket, true);
    }

    void HandleChatMessage(byte type, string msg, PlayerInfo player, Socket socket)
    {
        Enqueue(() =>
        {
            if (type == 0)
            {
                ChatInput.Instance?.AddMessage(msg);
                SendMsg(4, 0, msg, socket, true);
            }
            else if (type == 1)
            {
                var chat = JsonUtility.FromJson<PrivateChatMsg>(msg);
                if (chat == null || player == null || GameManager.Instance?.LocalPlayerSave == null)
                    return;

                if (chat.PlayerName == GameManager.Instance.LocalPlayerSave.playerName)
                    ChatInput.Instance?.AddMessage("Jugador" + player.Name + "Susurro:" + chat.content, new Color32(123, 123, 123, 255));
                else
                    SendPrivateChatMsg(chat.PlayerName, chat.content, player.Name);
            }
        });
    }

    void RemoveSocket(Socket socket)
    {
        if (socket == null)
            return;

        lock (socketLock)
        {
            sockets.Remove(socket);
            socketPlayers.Remove(socket);
        }

        SafeCloseSocket(socket);
    }

    void SafeCloseSocket(Socket socket)
    {
        if (socket == null)
            return;

        try { socket.Shutdown(SocketShutdown.Both); } catch { }
        try { socket.Close(); } catch { }
        try { socket.Dispose(); } catch { }
    }

    void SendMsg(byte type1, byte type2, string content = "", Socket socket = null, bool outThis = false)
    {
        byte[] payload = Encoding.UTF8.GetBytes(content ?? "");
        byte[] buffer = new byte[payload.Length + 6];

        Buffer.BlockCopy(BitConverter.GetBytes(payload.Length + 2), 0, buffer, 0, 4);
        buffer[4] = type1;
        buffer[5] = type2;

        if (payload.Length > 0)
            Buffer.BlockCopy(payload, 0, buffer, 6, payload.Length);

        Socket[] targets;
        lock (socketLock)
            targets = sockets.ToArray();

        if (socket == null)
        {
            foreach (var target in targets)
                SendToSocket(target, buffer);
            return;
        }

        foreach (var target in targets)
        {
            if (!outThis && target == socket)
            {
                SendToSocket(target, buffer);
                return;
            }

            if (outThis && target != null && target != socket)
                SendToSocket(target, buffer);
        }
    }

    void SendJson(byte type1, byte type2, object value, Socket socket = null, bool outThis = false) =>
        SendMsg(type1, type2, JsonUtility.ToJson(value), socket, outThis);

    void SendJson(byte type1, byte type2, object value, string playerName)
    {
        var socket = GetSocketByPlayer(playerName);
        if (socket != null)
            SendJson(type1, type2, value, socket);
    }

    void SendMsg(byte type1, byte type2, string content, string playerName)
    {
        var socket = GetSocketByPlayer(playerName);
        if (socket != null)
            SendMsg(type1, type2, content, socket);
    }

    Socket GetSocketByPlayer(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        lock (socketLock)
        {
            foreach (var pair in socketPlayers)
                if (pair.Value?.Name == name)
                    return pair.Key;
        }

        return null;
    }

    void SendToSocket(Socket socket, byte[] buffer)
    {
        if (socket == null || buffer == null)
            return;

        try
        {
            int sent = 0;
            while (sent < buffer.Length)
            {
                int count = socket.Send(buffer, sent, buffer.Length - sent, SocketFlags.None);
                if (count <= 0)
                    throw new SocketException();
                sent += count;
            }
        }
        catch
        {
            var disconnected = GetPlayerBySocket(socket);
            RemoveSocket(socket);

            if (disconnected != null)
                Enqueue(() => RemovePlayer(disconnected));
        }
    }

    PlayerInfo GetPlayerBySocket(Socket socket)
    {
        if (socket == null)
            return null;

        lock (socketLock)
        {
            socketPlayers.TryGetValue(socket, out var player);
            return player;
        }
    }

    IEnumerator SendHeartbeat()
    {
        float timer = Time.realtimeSinceStartup;

        while (isServerOpen && GameManager.Instance?.isOnline == true)
        {
            yield return null;

            if (Time.realtimeSinceStartup - timer > .5f)
            {
                SendMsg(0, byte.MaxValue);
                timer = Time.realtimeSinceStartup;
            }
        }
    }

    IEnumerator CheckConnect()
    {
        float timer = Time.realtimeSinceStartup;

        while (isServerOpen && GameManager.Instance?.isOnline == true)
        {
            yield return null;

            if (Time.realtimeSinceStartup - timer <= 3f)
                continue;

            for (int i = players.Count - 1; i >= 0; i--)
            {
                var player = players[i];

                if (player == null)
                {
                    players.RemoveAt(i);
                    continue;
                }

                if (!player.Heartbeat)
                {
                    var socket = GetSocketByPlayer(player.Name);
                    if (socket != null)
                        RemoveSocket(socket);

                    Debug.LogError(player.Name + "Desconexión por tiempo de espera");
                    RemovePlayer(player);
                }
                else
                    player.Heartbeat = false;
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

    void OnApplicationQuit()
    {
        try
        {
            StopAllCoroutines();
            ClearServerState(false);
        }
        catch { }
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
        catch { }

        Instance = null;
    }

    public void SynItem(SynItem syn)
    {
        if (syn == null)
            return;

        if (syn.Type == SynItemType.AllItem || syn.Type == SynItemType.Plant)
            PlantManager.Instance?.OnlineGetPlant(syn.OnlineId)?.OnlineSynPlant(syn);

        if (syn.Type == SynItemType.AllItem || syn.Type == SynItemType.Zombie)
            ZombieManager.Instance?.OnlineGetZombie(syn.OnlineId)?.OnlineSynZombie(syn);

        if (syn.Type == SynItemType.AllItem || syn.Type == SynItemType.Puddle)
        {
            var list = MapManager.Instance?.puddles;
            if (list != null)
                foreach (var item in list)
                    if (item?.OnlineId == syn.OnlineId)
                    {
                        item.StartDisappear();
                        break;
                    }
        }

        if (syn.Type == SynItemType.AllItem || syn.Type == SynItemType.Portal)
        {
            var list = MapManager.Instance?.portalCs;
            if (list != null)
                foreach (var item in list)
                    if (item?.OnlineId == syn.OnlineId)
                    {
                        item.ClientReset(syn);
                        break;
                    }
        }

        if (syn.Type == SynItemType.AllItem || syn.Type == SynItemType.Vase)
            LvItemManager.Instance?.SynVase(syn);

        if (syn.Type == SynItemType.AllItem || syn.Type == SynItemType.Card)
            SeedBank.Instance?.SynDropCard(syn);
    }

    public List<string> GetAllPlayerNameList()
    {
        var names = new List<string>();

        if (HostPlayer != null)
            names.Add(HostPlayer.Name);

        foreach (var player in players)
            if (player != null)
                names.Add(player.Name);

        return names;
    }

    public List<PlayerInfo> GetPlayers() => new(players);
    public PlayerInfo GetHostPlayer() => HostPlayer;

    void UpdatePlayerLists()
    {
        battlePlayerList ??= BattlePlayerList.Instance;
        playerList ??= PlayerList.Instance;

        battlePlayerList?.UpdatePlayerList(HostPlayer, players);
        playerList?.UpdatePlayerList(HostPlayer, players);
    }

    public void SendFailConnectMsg(string msg, Socket socket) =>
        SendJson(0, 1, new ConnectInfo { msg = msg }, socket);

    public void SendHostCD(int cardID, bool isOK)
    {
        var save = GameManager.Instance?.LocalPlayerSave;
        if (save == null)
            return;

        SendJson(2, 4, new UpdateCardCD
        {
            name = save.playerName,
            CardId = cardID,
            OK = isOK
        });
    }

    public void SendChatMsg(string content) => SendMsg(4, 0, content);

    public void SendPrivateChatMsg(string name, string content, string sender) =>
        SendJson(4, 2, new PrivateChatMsg
        {
            PlayerName = sender,
            content = content
        }, name);

    public void KickPlayer(string name)
    {
        var player = players.FirstOrDefault(p => p?.Name == name);
        if (player == null)
            return;

        HandQuitPlayer.Add(player);

        var socket = GetSocketByPlayer(name);
        if (socket != null)
        {
            SendFailConnectMsg("Has sido expulsado de la partida", socket);
            SafeCloseSocket(socket);
        }
    }

    public void LoadLv(LoadLVBag loadLV)
    {
        ReConnectCode = UnityEngine.Random.Range(100000, 999999);
        loadLV.ReCntCode = ReConnectCode;
        SendJson(1, 0, loadLV);
    }

    public void SendAddCard(AddCardBag bag, string playerName) =>
        SendJson(1, 12, bag, playerName);

    public void StartRunLv() => SendMsg(1, 1);
    public void BigWaveComing(WaveComing wave) => SendJson(1, 2, wave);
    public void UpdateSunNum(SunNumBag sun) => SendJson(1, 3, sun);
    public void SendSynBag(SynItem syn) => SendJson(1, 4, syn);
    public void ChangeMap(PlayerMap map) => SendJson(1, 5, map);
    public void SelectCard(SelectCard card) => SendJson(1, 6, card);
    public void SelectPrepare(SelectPrepare prepare) => SendJson(1, 7, prepare);
    public void GameOver(GameOver over) => SendJson(1, 8, over);
    public void SynTeamList(PvPTeamList list) => SendJson(1, 9, list);
    public void SynPvPMode(PvPModeSyn syn, Socket socket = null) => SendJson(1, 10, syn, socket);
    public void SynSpectList(SpectList list) => SendJson(1, 11, list);
    public void SynFlagMeter(FlagMeterSyn syn) => SendJson(1, 13, syn);
    public void SynTimeTable(TimetableSyn syn) => SendJson(1, 14, syn);

    public void SpawnSun(SunSpawn spawn) => SendJson(2, 1, spawn);
    public void ClickedSun(ClickedSun sun) => SendJson(2, 2, sun);
    public void SpawnPlant(PlantSpawn spawn) => SendJson(2, 0, spawn);
    public void PlacePreview(PlantPreview preview, Socket socket) => SendJson(2, 3, preview, socket, true);
    public void ZombiePreview(ZombiePreview preview, Socket socket) => SendJson(3, 5, preview, socket, true);
    public void ShovelPreview(ShovelPreview preview, Socket socket) => SendJson(2, 5, preview, socket, true);
    public void SpawnZombie(ZombieSpawn spawn) => SendJson(3, 0, spawn);
    public void SpawnGraveStone(GraveStoneSpawn spawn) => SendJson(3, 1, spawn);
    public void SpawnPuddle(PuddleSpawn spawn) => SendJson(3, 2, spawn);
    public void SpawnPortal(PortalSpawn spawn) => SendJson(3, 6, spawn);
    public void SpawnVase(VaseSpawn spawn) => SendJson(3, 9, spawn);
    public void SpawnDropCard(CardSpawn spawn) => SendJson(3, 10, spawn);
    public void SpawnMelt(MeltSpawn spawn) => SendJson(3, 11, spawn);
    public void SpawnFallHail(FallHailSpawn spawn) => SendJson(3, 12, spawn);
    public void SpawnLightning(LightingSpawn spawn) => SendJson(3, 3, spawn);
    public void SendShovelAnim(ToolApply apply) => SendJson(2, 6, apply);
    public void SendGridState(SynGrid grid) => SendJson(3, 7, grid);
    public void SendWeatherCmd(WeatherChange cmd) => SendJson(4, 4, cmd);
    public void SendTimeCmd(TimeCmd cmd) => SendJson(4, 5, cmd);
    public void SendMapSyn(SynMap map) => SendJson(3, 4, map);
    public void SendSynBooty(SynBooty booty) => SendJson(3, 8, booty);

    public void SendAcvmentGet(Acvname acv, string playerName) =>
        SendJson(4, 6, new GetAcvment { acv = acv }, playerName);

    public void SendCommandBag(Socket socket = null)
    {
        if (PlantManager.Instance == null ||
            ZombieManager.Instance == null ||
            SkyManager.Instance == null ||
            PlayerManager.Instance == null ||
            SeedBank.Instance == null ||
            LvItemManager.Instance == null)
            return;

        SendJson(4, 3, new CommandBag
        {
            Pinv = PlantManager.Instance.PlantInvincible,
            Zinv = ZombieManager.Instance.ZombieInvincible,
            DLiCy = SkyManager.Instance.DayLightCycle,
            SnInf = PlayerManager.Instance.SunInfinite,
            CdCle = SeedBank.Instance.isNoCD,
            ZomStop = ZombieManager.Instance.ZombieDontMove,
            VaseXray = LvItemManager.Instance.VaseAlwaysLight
        }, socket);
    }

    void SendWaitReConnect(bool wait)
    {
        var names = ReConnectPlayer
            .Where(p => p != null)
            .Select(p => p.Name)
            .ToList();

        SendJson(0, 3, new ReConnectInfo
        {
            isWait = wait,
            names = names
        });
    }

    void ReConnectListChange()
    {
        if (ReConnectPlayer.Count > 0)
        {
            var names = ReConnectPlayer
                .Where(p => p != null)
                .Select(p => p.Name)
                .ToList();

            ReConnect.Instance?.LoadPlayerList(names);
            SendWaitReConnect(true);
        }
        else
        {
            SendWaitReConnect(false);
            ReConnect.Instance?.OverClose();
        }
    }

    bool CanReConnect(PlayerInfo info) =>
        info != null && ReConnectPlayer.Any(p => p?.Name == info.Name);

    public void GiveUpReConnect()
    {
        foreach (var player in ReConnectPlayer)
            RemoveDone(player);

        ReConnectPlayer.Clear();
        ReConnectListChange();
    }

    void AddNewPlayer(PlayerInfo player)
    {
        if (player == null || players.Any(p => p?.Name == player.Name))
            return;

        players.Add(player);
        UpdatePlayerLists();

        string content = player.Name + "Te has unido a la partida";

        ChatInput.Instance?.AddMessage(content, new Color32(255, 255, 0, 255));
        SendMsg(4, 1, content);

        SendJson(0, 2, new OnlinePlayerInfo
        {
            HostPlayer = HostPlayer,
            players = players
        });
    }

    void RemovePlayer(PlayerInfo player)
    {
        if (player == null)
            return;

        int index = players.IndexOf(player);
        if (index < 0)
            index = players.FindIndex(p => p?.Name == player.Name);

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
            RemoveDone(player);
    }

    void RemoveDone(PlayerInfo player)
    {
        if (player == null)
            return;

        string content = player.Name + "Saliste del juego";

        ChatInput.Instance?.AddMessage(content, new Color32(255, 255, 0, 255));
        SendMsg(4, 1, content);
        UpdatePlayerLists();

        PvPSelector.Instance?.ClearQuitPlayer(player.Name);
        SpectatorList.Instance?.ClearPlayer(player.Name);

        SendJson(0, 2, new OnlinePlayerInfo
        {
            HostPlayer = HostPlayer,
            players = players
        });
    }
}