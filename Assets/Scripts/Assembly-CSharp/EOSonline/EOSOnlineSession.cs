using System.Collections.Generic;
using UnityEngine;
using Epic.OnlineServices;
using Epic.OnlineServices.Sessions;
using PlayEveryWare.EpicOnlineServices;

public class EOSOnlineRoom
{
    public string RoomName;
    public string SessionId;
    public ProductUserId HostUserId;
    public uint CurrentPlayers;
    public uint MaxPlayers;
    public SessionDetails Details;

    public bool HasOpenSlots => CurrentPlayers < MaxPlayers;
}

public class EOSOnlineSession : MonoBehaviour
{
    public static EOSOnlineSession Instance;

    private enum SessionOperation
    {
        None,
        Creating,
        Searching,
        Joining,
        Leaving
    }

    [Header("Configuración")]
    [SerializeField] private string bucketId = "PvZEcoPavo";
    [SerializeField] private uint maxPlayers = 4;
    [SerializeField] private string defaultRoomName = "Sala de PvZ Eco Pavo";

    private SessionsInterface sessionsInterface;
    private SessionModification sessionModification;
    private SessionSearch sessionSearch;
    private SessionDetails foundSession;
    private ProductUserId foundHostUserId;

    private string localSessionName;
    private string localRoomName;

    private bool sessionsInitialized;
    private bool leavingOnlineGame;
    private bool destroyingSession;

    private SessionOperation currentOperation = SessionOperation.None;

    private readonly List<EOSOnlineRoom> availableRooms = new();

    public bool IsHosting { get; private set; }
    public bool IsJoined { get; private set; }
    public bool IsSearching { get; private set; }

    public bool IsOperationInProgress =>
        currentOperation != SessionOperation.None ||
        leavingOnlineGame ||
        destroyingSession;

    public string LocalSessionName => localSessionName;
    public string LocalRoomName => localRoomName;
    public bool IsInitialized => sessionsInitialized;
    public ProductUserId HostUserId => foundHostUserId;

    public IReadOnlyList<EOSOnlineRoom> AvailableRooms =>
        availableRooms;

    public int AvailableRoomCount =>
        availableRooms.Count;

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

    private void Start()
    {
        TryInitialize();
    }

    private void Update()
    {
        if (!sessionsInitialized)
            TryInitialize();
    }

    private void TryInitialize()
    {
        if (sessionsInitialized ||
            EOSManager.Instance == null ||
            EOSAutoLogin.Instance == null ||
            !EOSAutoLogin.Instance.IsLoggedIn)
            return;

        sessionsInterface =
            EOSManager.Instance.GetEOSSessionsInterface();

        if (sessionsInterface == null)
            return;

        sessionsInitialized = true;

        Debug.Log(
            "[EOS SESSION] Sesiones inicializadas correctamente."
        );
    }

    public void SetRoomName(string roomName)
    {
        localRoomName =
            string.IsNullOrWhiteSpace(roomName)
                ? defaultRoomName
                : roomName.Trim();

        Debug.Log(
            "[EOS SESSION] Nombre de sala: " +
            localRoomName
        );
    }

    public void CreateOnlineGame()
    {
        CreateOnlineGame(defaultRoomName);
    }

    public void CreateOnlineGame(string roomName)
    {
        if (!CanUseSessions())
            return;

        if (IsOperationInProgress)
        {
            Debug.LogWarning(
                "[EOS SESSION] Ya hay una operación EOS en proceso."
            );
            return;
        }

        if (IsHosting || IsJoined)
        {
            Debug.LogWarning(
                "[EOS SESSION] Ya estás dentro de una sesión."
            );
            return;
        }

        leavingOnlineGame = false;
        currentOperation = SessionOperation.Creating;

        SetRoomName(roomName);

        ProductUserId localUserId =
            EOSManager.Instance.GetProductUserId();

        if (localUserId == null || !localUserId.IsValid())
        {
            currentOperation = SessionOperation.None;

            SetUIStatus("No se pudo crear la partida.");

            Debug.LogError(
                "[EOS SESSION] ProductUserId no disponible."
            );
            return;
        }

        localSessionName =
            "PvZEcoPavo_" + localUserId;

        CreateSessionModificationOptions options =
            new CreateSessionModificationOptions
            {
                BucketId = bucketId,
                MaxPlayers = maxPlayers,
                SessionName = localSessionName,
                LocalUserId = localUserId,
                PresenceEnabled = false
            };

        Debug.Log(
            "[EOS SESSION] Creando sesión: " +
            localSessionName
        );

        Result result =
            sessionsInterface.CreateSessionModification(
                ref options,
                out sessionModification
            );

        if (result != Result.Success)
        {
            currentOperation = SessionOperation.None;
            sessionModification = null;
            localSessionName = null;

            SetUIStatus("No se pudo crear la partida.");

            Debug.LogError(
                "[EOS SESSION] CreateSessionModification: " +
                result
            );

            return;
        }

        SessionModificationSetPermissionLevelOptions permission =
            new SessionModificationSetPermissionLevelOptions
            {
                PermissionLevel =
                    OnlineSessionPermissionLevel.PublicAdvertised
            };

        result =
            sessionModification.SetPermissionLevel(ref permission);

        if (result != Result.Success)
        {
            FailCurrentOperation(
                "SetPermissionLevel",
                result
            );
            return;
        }

        SessionModificationSetJoinInProgressAllowedOptions join =
            new SessionModificationSetJoinInProgressAllowedOptions
            {
                AllowJoinInProgress = true
            };

        result =
            sessionModification.SetJoinInProgressAllowed(ref join);

        if (result != Result.Success)
        {
            FailCurrentOperation(
                "SetJoinInProgressAllowed",
                result
            );
            return;
        }

        SessionModificationSetInvitesAllowedOptions invites =
            new SessionModificationSetInvitesAllowedOptions
            {
                InvitesAllowed = true
            };

        result =
            sessionModification.SetInvitesAllowed(ref invites);

        if (result != Result.Success)
        {
            FailCurrentOperation(
                "SetInvitesAllowed",
                result
            );
            return;
        }

        if (!AddSessionAttribute("Game", "PvZEcoPavo") ||
            !AddSessionAttribute("GameVersion", "0.7") ||
            !AddSessionAttribute("RoomName", localRoomName))
        {
            currentOperation = SessionOperation.None;
            return;
        }

        UpdateSessionOptions update =
            new UpdateSessionOptions
            {
                SessionModificationHandle =
                    sessionModification
            };

        Debug.Log("[EOS SESSION] Publicando sesión...");

        sessionsInterface.UpdateSession(
            ref update,
            null,
            OnSessionCreated
        );
    }

    private void FailCurrentOperation(
        string operation,
        Result result)
    {
        Debug.LogError(
            "[EOS SESSION] " +
            operation +
            ": " +
            result
        );

        ReleaseSessionModification();
        localSessionName = null;
        currentOperation = SessionOperation.None;

        SetUIStatus("No se pudo crear la partida.");
    }

    private bool AddSessionAttribute(
        string key,
        string value)
    {
        if (sessionModification == null)
            return false;

        AttributeData attribute =
            new AttributeData
            {
                Key = key,
                Value = new AttributeDataValue
                {
                    AsUtf8 = value
                }
            };

        SessionModificationAddAttributeOptions options =
            new SessionModificationAddAttributeOptions
            {
                SessionAttribute = attribute,
                AdvertisementType =
                    SessionAttributeAdvertisementType.Advertise
            };

        Result result =
            sessionModification.AddAttribute(ref options);

        if (result == Result.Success)
            return true;

        Debug.LogError(
            "[EOS SESSION] AddAttribute " +
            key +
            ": " +
            result
        );

        ReleaseSessionModification();
        localSessionName = null;

        SetUIStatus("No se pudo crear la partida.");

        return false;
    }

    private void OnSessionCreated(
        ref UpdateSessionCallbackInfo data)
    {
        Debug.Log(
            "[EOS SESSION] Create/Update: " +
            data.ResultCode
        );

        ReleaseSessionModification();

        if (leavingOnlineGame ||
            currentOperation != SessionOperation.Creating)
        {
            currentOperation = SessionOperation.None;

            if (data.ResultCode == Result.Success &&
                !destroyingSession &&
                sessionsInterface != null &&
                !string.IsNullOrEmpty(localSessionName))
            {
                DestroyLocalSession();
            }

            return;
        }

        currentOperation = SessionOperation.None;

        if (data.ResultCode != Result.Success)
        {
            IsHosting = false;
            IsJoined = false;
            localSessionName = null;

            SetUIStatus("No se pudo crear la partida.");

            Debug.LogError(
                "[EOS SESSION] No se pudo crear la sesión."
            );

            return;
        }

        IsHosting = true;
        IsJoined = true;

        foundHostUserId =
            EOSAutoLogin.Instance.LocalProductUserId;

        Debug.Log("[EOS SESSION] PARTIDA ONLINE CREADA.");
        Debug.Log("[EOS SESSION] SessionName: " + localSessionName);
        Debug.Log("[EOS SESSION] Sala: " + localRoomName);
        Debug.Log("[EOS SESSION] Host: " + foundHostUserId);

        SetUIStatus("Partida creada correctamente.");

        StartOnlineServer();
    }

    private void StartOnlineServer()
    {
        if (leavingOnlineGame)
            return;

        if (EOSOnlineTransport.Instance == null)
        {
            Debug.LogError(
                "[EOS SESSION] EOSOnlineTransport no existe."
            );
            return;
        }

        if (EOSOnlineServer.Instance == null)
        {
            Debug.LogError(
                "[EOS SESSION] EOSOnlineServer no existe."
            );
            return;
        }

        if (!EOSOnlineTransport.Instance.StartHost())
        {
            Debug.LogError(
                "[EOS SESSION] No se pudo iniciar EOS P2P como host."
            );
            return;
        }

        if (!EOSOnlineServer.Instance.StartServer())
        {
            Debug.LogError(
                "[EOS SESSION] No se pudo iniciar EOSOnlineServer."
            );

            EOSOnlineTransport.Instance.Disconnect();
            return;
        }

        Debug.Log(
            "[EOS SESSION] SERVIDOR ONLINE INICIADO."
        );
    }

    public void SearchOnlineGames()
    {
        if (!CanUseSessions())
            return;

        if (IsOperationInProgress)
        {
            Debug.LogWarning(
                "[EOS SESSION] Ya hay una operación EOS en proceso."
            );
            return;
        }

        if (IsHosting || IsJoined)
        {
            Debug.LogWarning(
                "[EOS SESSION] Ya estás dentro de una sesión."
            );
            return;
        }

        ReleaseFoundSession();
        ReleaseSearch();
        ClearAvailableRooms();

        foundHostUserId = null;
        currentOperation = SessionOperation.Searching;

        CreateSessionSearchOptions searchOptions =
            new CreateSessionSearchOptions
            {
                MaxSearchResults = 20
            };

        Result result =
            sessionsInterface.CreateSessionSearch(
                ref searchOptions,
                out sessionSearch
            );

        if (result != Result.Success)
        {
            currentOperation = SessionOperation.None;
            sessionSearch = null;

            Debug.LogError(
                "[EOS SESSION] CreateSessionSearch: " +
                result
            );
            return;
        }

        AttributeData attribute =
            new AttributeData
            {
                Key = "Game",
                Value = new AttributeDataValue
                {
                    AsUtf8 = "PvZEcoPavo"
                }
            };

        SessionSearchSetParameterOptions parameter =
            new SessionSearchSetParameterOptions
            {
                ComparisonOp = ComparisonOp.Equal,
                Parameter = attribute
            };

        result =
            sessionSearch.SetParameter(ref parameter);

        if (result != Result.Success)
        {
            currentOperation = SessionOperation.None;

            Debug.LogError(
                "[EOS SESSION] SetParameter: " +
                result
            );

            ReleaseSearch();
            return;
        }

        ProductUserId localUserId =
            EOSManager.Instance.GetProductUserId();

        if (localUserId == null || !localUserId.IsValid())
        {
            currentOperation = SessionOperation.None;

            Debug.LogError(
                "[EOS SESSION] ProductUserId no disponible."
            );

            ReleaseSearch();
            return;
        }

        SessionSearchFindOptions find =
            new SessionSearchFindOptions
            {
                LocalUserId = localUserId
            };

        IsSearching = true;

        Debug.Log(
            "[EOS SESSION] Buscando partidas online..."
        );

        sessionSearch.Find(
            ref find,
            null,
            OnSearchFinished
        );
    }

    private void OnSearchFinished(
        ref SessionSearchFindCallbackInfo data)
    {
        IsSearching = false;

        Debug.Log(
            "[EOS SESSION] Search: " +
            data.ResultCode
        );

        if (currentOperation == SessionOperation.Searching)
            currentOperation = SessionOperation.None;

        if (data.ResultCode != Result.Success)
        {
            Debug.LogError(
                "[EOS SESSION] Error buscando partidas."
            );

            ReleaseSearch();
            return;
        }

        if (leavingOnlineGame)
        {
            ReleaseSearch();
            return;
        }

        SessionSearchGetSearchResultCountOptions countOptions = new();

        uint count =
            sessionSearch.GetSearchResultCount(
                ref countOptions
            );

        ReleaseFoundSession();
        ClearAvailableRooms();

        for (uint i = 0; i < count; i++)
        {
            SessionSearchCopySearchResultByIndexOptions index =
                new SessionSearchCopySearchResultByIndexOptions
                {
                    SessionIndex = i
                };

            Result result =
                sessionSearch.CopySearchResultByIndex(
                    ref index,
                    out SessionDetails details
                );

            if (result != Result.Success ||
                details == null)
                continue;

            SessionDetailsCopyInfoOptions infoOptions = new();

            result =
                details.CopyInfo(
                    ref infoOptions,
                    out SessionDetailsInfo? info
                );

            if (result != Result.Success || !info.HasValue)
            {
                details.Release();
                continue;
            }

            uint openConnections =
                info.Value.NumOpenPublicConnections;

            uint maxConnections =
                info.Value.Settings.HasValue
                    ? info.Value.Settings.Value.NumPublicConnections
                    : 0;

            uint players =
                maxConnections >= openConnections
                    ? maxConnections - openConnections
                    : 0;

            string roomName =
                GetSessionAttributeString(
                    details,
                    "RoomName"
                );

            if (string.IsNullOrWhiteSpace(roomName))
                roomName = info.Value.SessionId;

            EOSOnlineRoom room =
                new EOSOnlineRoom
                {
                    RoomName = roomName,
                    SessionId = info.Value.SessionId,
                    HostUserId = info.Value.OwnerUserId,
                    CurrentPlayers = players,
                    MaxPlayers = maxConnections,
                    Details = details
                };

            if (room.HostUserId != null &&
                room.HostUserId.IsValid() &&
                room.HasOpenSlots)
            {
                availableRooms.Add(room);

                Debug.Log(
                    "[EOS SESSION] Sala encontrada: " +
                    room.RoomName +
                    " | Jugadores: " +
                    room.CurrentPlayers +
                    "/" +
                    room.MaxPlayers
                );

                if (foundSession == null)
                {
                    foundSession = details;
                    foundHostUserId = room.HostUserId;
                }
            }
            else
            {
                details.Release();
            }
        }

        Debug.Log(
            "[EOS SESSION] Salas disponibles: " +
            availableRooms.Count
        );
    }

    private string GetSessionAttributeString(
        SessionDetails details,
        string key)
    {
        if (details == null)
            return null;

        for (uint i = 0; i < 64; i++)
        {
            SessionDetailsCopySessionAttributeByIndexOptions options =
                new SessionDetailsCopySessionAttributeByIndexOptions
                {
                    AttrIndex = i
                };

            Result result =
                details.CopySessionAttributeByIndex(
                    ref options,
                    out SessionDetailsAttribute? attribute
                );

            if (result != Result.Success)
                break;

            if (!attribute.HasValue ||
                !attribute.Value.Data.HasValue)
                continue;

            AttributeData data =
                attribute.Value.Data.Value;

            if (data.Key == key)
                return data.Value.AsUtf8;
        }

        return null;
    }

    public EOSOnlineRoom GetAvailableRoom(int index)
    {
        return index >= 0 && index < availableRooms.Count
            ? availableRooms[index]
            : null;
    }

    public EOSOnlineRoom GetFirstAvailableRoom()
    {
        return availableRooms.Count > 0
            ? availableRooms[0]
            : null;
    }

    public bool SelectRoom(int index)
    {
        EOSOnlineRoom room = GetAvailableRoom(index);

        if (room == null)
        {
            Debug.LogError(
                "[EOS SESSION] Índice de sala inválido: " +
                index
            );
            return false;
        }

        return SelectRoom(room);
    }

    public bool SelectRoom(EOSOnlineRoom room)
    {
        if (room == null ||
            room.Details == null ||
            room.HostUserId == null ||
            !room.HostUserId.IsValid())
            return false;

        foundSession = room.Details;
        foundHostUserId = room.HostUserId;

        Debug.Log(
            "[EOS SESSION] Sala seleccionada: " +
            room.RoomName
        );

        return true;
    }

    public void JoinFirstOnlineGame()
    {
        EOSOnlineRoom room = GetFirstAvailableRoom();

        if (room != null)
            SelectRoom(room);

        JoinSelectedOnlineGame();
    }

    public void JoinSelectedOnlineGame()
    {
        if (!CanUseSessions())
            return;

        if (IsOperationInProgress)
        {
            Debug.LogWarning(
                "[EOS SESSION] Ya hay una operación EOS en proceso."
            );
            return;
        }

        if (foundSession == null)
        {
            SetUIStatus("No hay ninguna partida seleccionada.");
            return;
        }

        if (foundHostUserId == null ||
            !foundHostUserId.IsValid())
        {
            SetUIStatus("El host no está disponible.");
            return;
        }

        ProductUserId localUserId =
            EOSManager.Instance.GetProductUserId();

        if (localUserId == null || !localUserId.IsValid())
        {
            SetUIStatus("No se pudo obtener el usuario local.");
            return;
        }

        if (foundHostUserId == localUserId)
        {
            SetUIStatus("No podés unirte a tu propia partida.");
            return;
        }

        localSessionName =
            "PvZEcoPavo_Client_" + localUserId;

        currentOperation = SessionOperation.Joining;

        JoinSessionOptions options =
            new JoinSessionOptions
            {
                SessionHandle = foundSession,
                SessionName = localSessionName,
                LocalUserId = localUserId,
                PresenceEnabled = false
            };

        Debug.Log(
            "[EOS SESSION] Uniéndose a partida..."
        );

        sessionsInterface.JoinSession(
            ref options,
            null,
            OnJoinFinished
        );
    }

    private void OnJoinFinished(
        ref JoinSessionCallbackInfo data)
    {
        Debug.Log(
            "[EOS SESSION] JoinSession: " +
            data.ResultCode
        );

        if (currentOperation == SessionOperation.Joining)
            currentOperation = SessionOperation.None;

        if (leavingOnlineGame)
        {
            if (data.ResultCode == Result.Success &&
                !destroyingSession &&
                sessionsInterface != null &&
                !string.IsNullOrEmpty(localSessionName))
            {
                DestroyLocalSession();
            }

            return;
        }

        if (data.ResultCode != Result.Success)
        {
            IsJoined = false;
            IsHosting = false;
            localSessionName = null;

            SetUIStatus("No se pudo unir a la partida.");

            Debug.LogError(
                "[EOS SESSION] No se pudo unir."
            );

            return;
        }

        IsJoined = true;
        IsHosting = false;

        Debug.Log("[EOS SESSION] PARTIDA ONLINE UNIDA.");

        SetUIStatus("Te uniste a la partida correctamente.");

        if (foundHostUserId == null ||
            !foundHostUserId.IsValid())
        {
            SetUIStatus("El host no está disponible.");
            return;
        }

        StartOnlineClient();
    }

    private void StartOnlineClient()
    {
        if (leavingOnlineGame)
            return;

        if (EOSOnlineClient.Instance == null)
        {
            Debug.LogError(
                "[EOS SESSION] EOSOnlineClient no existe."
            );
            return;
        }

        if (EOSOnlineTransport.Instance == null)
        {
            Debug.LogError(
                "[EOS SESSION] EOSOnlineTransport no existe."
            );
            return;
        }

        Debug.Log(
            "[EOS SESSION] Conectando P2P con host: " +
            foundHostUserId
        );

        if (!EOSOnlineClient.Instance.ConnectToHost(foundHostUserId))
        {
            Debug.LogError(
                "[EOS SESSION] No se pudo iniciar el cliente P2P."
            );
            return;
        }

        Debug.Log(
            "[EOS SESSION] CLIENTE ONLINE INICIADO."
        );
    }

    public void LeaveOnlineGame()
    {
        if (leavingOnlineGame)
            return;

        if (!IsHosting &&
            !IsJoined &&
            string.IsNullOrEmpty(localSessionName) &&
            currentOperation == SessionOperation.None)
            return;

        leavingOnlineGame = true;
        currentOperation = SessionOperation.Leaving;

        Debug.Log(
            "[EOS SESSION] Saliendo de sesión..."
        );

        if (EOSOnlineServer.Instance != null)
            EOSOnlineServer.Instance.StopServer();

        if (EOSOnlineClient.Instance != null)
            EOSOnlineClient.Instance.Disconnect();

        if (EOSOnlineTransport.Instance != null)
            EOSOnlineTransport.Instance.Disconnect();

        if (sessionsInterface == null ||
            string.IsNullOrEmpty(localSessionName))
        {
            FinishLeaveCleanup();
            return;
        }

        DestroyLocalSession();
    }

    private void DestroyLocalSession()
    {
        if (destroyingSession)
            return;

        if (sessionsInterface == null ||
            string.IsNullOrEmpty(localSessionName))
        {
            FinishLeaveCleanup();
            return;
        }

        destroyingSession = true;

        DestroySessionOptions options =
            new DestroySessionOptions
            {
                SessionName = localSessionName
            };

        sessionsInterface.DestroySession(
            ref options,
            null,
            OnSessionDestroyed
        );
    }

    private void OnSessionDestroyed(
        ref DestroySessionCallbackInfo data)
    {
        Debug.Log(
            "[EOS SESSION] DestroySession: " +
            data.ResultCode
        );

        FinishLeaveCleanup();
    }

    private void FinishLeaveCleanup()
    {
        IsHosting = false;
        IsJoined = false;
        IsSearching = false;

        destroyingSession = false;
        currentOperation = SessionOperation.None;

        ReleaseSessionModification();
        ReleaseFoundSession();
        ReleaseSearch();
        ClearAvailableRooms();

        foundHostUserId = null;
        localSessionName = null;
        localRoomName = null;
        leavingOnlineGame = false;

        Debug.Log(
            "[EOS SESSION] SESIÓN ONLINE CERRADA CORRECTAMENTE."
        );
    }

    private bool CanUseSessions()
    {
        if (!sessionsInitialized)
            TryInitialize();

        if (!sessionsInitialized)
        {
            Debug.LogError(
                "[EOS SESSION] EOS todavía no está listo."
            );
            return false;
        }

        if (EOSManager.Instance == null)
        {
            Debug.LogError(
                "[EOS SESSION] EOSManager no existe."
            );
            return false;
        }

        if (EOSAutoLogin.Instance == null ||
            !EOSAutoLogin.Instance.IsLoggedIn)
        {
            Debug.LogError(
                "[EOS SESSION] EOS todavía no inició sesión."
            );
            return false;
        }

        if (sessionsInterface == null)
        {
            Debug.LogError(
                "[EOS SESSION] SessionsInterface no disponible."
            );
            return false;
        }

        return true;
    }

    private void ClearAvailableRooms()
    {
        foreach (EOSOnlineRoom room in availableRooms)
        {
            if (room == null ||
                room.Details == null ||
                room.Details == foundSession)
                continue;

            room.Details.Release();
        }

        availableRooms.Clear();
    }

    private void ReleaseSessionModification()
    {
        if (sessionModification == null)
            return;

        sessionModification.Release();
        sessionModification = null;
    }

    private void ReleaseFoundSession()
    {
        if (foundSession != null)
        {
            bool belongsToRoom = false;

            foreach (EOSOnlineRoom room in availableRooms)
            {
                if (room != null &&
                    room.Details == foundSession)
                {
                    belongsToRoom = true;
                    break;
                }
            }

            if (!belongsToRoom)
                foundSession.Release();

            foundSession = null;
        }

        foundHostUserId = null;
    }

    private void ReleaseSearch()
    {
        if (sessionSearch != null)
        {
            sessionSearch.Release();
            sessionSearch = null;
        }

        IsSearching = false;

        if (currentOperation == SessionOperation.Searching)
            currentOperation = SessionOperation.None;
    }

    private void SetUIStatus(string message)
    {
        if (EOSOnlineUI.Instance != null)
            EOSOnlineUI.Instance.SetSessionStatus(message);
    }

    private void OnApplicationQuit()
    {
        leavingOnlineGame = true;
        currentOperation = SessionOperation.Leaving;
    }

    private void OnDestroy()
    {
        ReleaseSessionModification();
        ReleaseFoundSession();
        ClearAvailableRooms();
        ReleaseSearch();

        sessionsInterface = null;

        if (Instance == this)
            Instance = null;
    }
}