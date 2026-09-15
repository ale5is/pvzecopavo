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

    public bool HasOpenSlots =>
        CurrentPlayers < MaxPlayers;
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

    [SerializeField]
    private string defaultRoomName =
        "Sala de PvZ Eco Pavo";

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

    private SessionOperation currentOperation =
        SessionOperation.None;

    private readonly List<EOSOnlineRoom> availableRooms =
        new List<EOSOnlineRoom>();

    public bool IsHosting { get; private set; }

    public bool IsJoined { get; private set; }

    public bool IsSearching { get; private set; }

    public bool IsOperationInProgress =>
        currentOperation != SessionOperation.None ||
        leavingOnlineGame ||
        destroyingSession;

    public string LocalSessionName =>
        localSessionName;

    public string LocalRoomName =>
        localRoomName;

    public bool IsInitialized =>
        sessionsInitialized;

    public ProductUserId HostUserId =>
        foundHostUserId;

    public IReadOnlyList<EOSOnlineRoom> AvailableRooms =>
        availableRooms;

    public int AvailableRoomCount =>
        availableRooms.Count;

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
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
        {
            TryInitialize();
        }
    }

    private void TryInitialize()
    {
        if (sessionsInitialized)
        {
            return;
        }

        if (EOSManager.Instance == null)
        {
            return;
        }

        if (EOSAutoLogin.Instance == null)
        {
            return;
        }

        if (!EOSAutoLogin.Instance.IsLoggedIn)
        {
            return;
        }

        sessionsInterface =
            EOSManager.Instance.GetEOSSessionsInterface();

        if (sessionsInterface == null)
        {
            return;
        }

        sessionsInitialized = true;

        Debug.Log(
            "[EOS SESSION] Sesiones inicializadas correctamente."
        );
    }

    public void SetRoomName(
        string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            localRoomName =
                defaultRoomName;
        }
        else
        {
            localRoomName =
                roomName.Trim();
        }

        Debug.Log(
            "[EOS SESSION] Nombre de sala: " +
            localRoomName
        );
    }

    public void CreateOnlineGame()
    {
        CreateOnlineGame(
            defaultRoomName
        );
    }

    public void CreateOnlineGame(
        string roomName)
    {
        if (!CanUseSessions())
        {
            return;
        }

        if (IsOperationInProgress)
        {
            Debug.LogWarning(
                "[EOS SESSION] Ya hay una operación EOS en proceso."
            );

            return;
        }

        if (IsHosting ||
            IsJoined)
        {
            Debug.LogWarning(
                "[EOS SESSION] Ya estás dentro de una sesión."
            );

            return;
        }

        leavingOnlineGame = false;

        currentOperation =
            SessionOperation.Creating;

        SetRoomName(
            roomName
        );

        ProductUserId localUserId =
            EOSManager.Instance.GetProductUserId();

        if (localUserId == null ||
            !localUserId.IsValid())
        {
            currentOperation =
                SessionOperation.None;

            Debug.LogError(
                "[EOS SESSION] ProductUserId no disponible."
            );

            return;
        }

        localSessionName =
            "PvZEcoPavo_" +
            localUserId.ToString();

        CreateSessionModificationOptions createOptions =
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

        Debug.Log(
            "[EOS SESSION] Sala: " +
            localRoomName
        );

        Result result =
            sessionsInterface.CreateSessionModification(
                ref createOptions,
                out sessionModification
            );

        if (result != Result.Success)
        {
            currentOperation =
                SessionOperation.None;

            Debug.LogError(
                "[EOS SESSION] CreateSessionModification: " +
                result
            );

            sessionModification = null;
            localSessionName = null;

            return;
        }

        SessionModificationSetPermissionLevelOptions permissionOptions =
            new SessionModificationSetPermissionLevelOptions
            {
                PermissionLevel =
                    OnlineSessionPermissionLevel.PublicAdvertised
            };

        result =
            sessionModification.SetPermissionLevel(
                ref permissionOptions
            );

        if (result != Result.Success)
        {
            FailCurrentOperation(
                "SetPermissionLevel",
                result
            );

            return;
        }

        SessionModificationSetJoinInProgressAllowedOptions joinOptions =
            new SessionModificationSetJoinInProgressAllowedOptions
            {
                AllowJoinInProgress = true
            };

        result =
            sessionModification.SetJoinInProgressAllowed(
                ref joinOptions
            );

        if (result != Result.Success)
        {
            FailCurrentOperation(
                "SetJoinInProgressAllowed",
                result
            );

            return;
        }

        SessionModificationSetInvitesAllowedOptions inviteOptions =
            new SessionModificationSetInvitesAllowedOptions
            {
                InvitesAllowed = true
            };

        result =
            sessionModification.SetInvitesAllowed(
                ref inviteOptions
            );

        if (result != Result.Success)
        {
            FailCurrentOperation(
                "SetInvitesAllowed",
                result
            );

            return;
        }

        if (!AddSessionAttribute(
                "Game",
                "PvZEcoPavo"))
        {
            currentOperation =
                SessionOperation.None;

            return;
        }

        if (!AddSessionAttribute(
                "GameVersion",
                "0.7"))
        {
            currentOperation =
                SessionOperation.None;

            return;
        }

        if (!AddSessionAttribute(
                "RoomName",
                localRoomName))
        {
            currentOperation =
                SessionOperation.None;

            return;
        }

        UpdateSessionOptions updateOptions =
            new UpdateSessionOptions
            {
                SessionModificationHandle =
                    sessionModification
            };

        Debug.Log(
            "[EOS SESSION] Publicando sesión..."
        );

        sessionsInterface.UpdateSession(
            ref updateOptions,
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

        currentOperation =
            SessionOperation.None;
    }

    private bool AddSessionAttribute(
        string key,
        string value)
    {
        if (sessionModification == null)
        {
            return false;
        }

        AttributeData attribute =
            new AttributeData
            {
                Key = key,
                Value =
                    new AttributeDataValue
                    {
                        AsUtf8 = value
                    }
            };

        SessionModificationAddAttributeOptions attributeOptions =
            new SessionModificationAddAttributeOptions
            {
                SessionAttribute = attribute,
                AdvertisementType =
                    SessionAttributeAdvertisementType.Advertise
            };

        Result result =
            sessionModification.AddAttribute(
                ref attributeOptions
            );

        if (result != Result.Success)
        {
            Debug.LogError(
                "[EOS SESSION] AddAttribute " +
                key +
                ": " +
                result
            );

            ReleaseSessionModification();

            localSessionName = null;

            return false;
        }

        return true;
    }

    private void OnSessionCreated(
        ref UpdateSessionCallbackInfo data)
    {
        Debug.Log(
            "[EOS SESSION] Create/Update: " +
            data.ResultCode
        );

        ReleaseSessionModification();

        /*
         * Si el usuario pulsó Salir mientras EOS todavía
         * estaba creando la sesión, NO iniciamos P2P.
         */
        if (leavingOnlineGame ||
            currentOperation != SessionOperation.Creating)
        {
            Debug.LogWarning(
                "[EOS SESSION] Callback de creación recibido durante el cierre."
            );

            currentOperation =
                SessionOperation.None;

            if (data.ResultCode == Result.Success &&
                !destroyingSession &&
                sessionsInterface != null &&
                !string.IsNullOrEmpty(localSessionName))
            {
                DestroyLocalSession();
            }

            return;
        }

        currentOperation =
            SessionOperation.None;

        if (data.ResultCode != Result.Success)
        {
            IsHosting = false;
            IsJoined = false;

            Debug.LogError(
                "[EOS SESSION] No se pudo crear la sesión."
            );

            localSessionName = null;

            return;
        }

        IsHosting = true;
        IsJoined = true;

        foundHostUserId =
            EOSAutoLogin.Instance.LocalProductUserId;

        Debug.Log(
            "[EOS SESSION] PARTIDA ONLINE CREADA."
        );

        Debug.Log(
            "[EOS SESSION] SessionName: " +
            localSessionName
        );

        Debug.Log(
            "[EOS SESSION] Sala: " +
            localRoomName
        );

        Debug.Log(
            "[EOS SESSION] Host ProductUserId: " +
            foundHostUserId
        );

        StartOnlineServer();
    }

    private void StartOnlineServer()
    {
        /*
         * Nunca iniciar el servidor si ya empezó
         * el proceso de salida.
         */
        if (leavingOnlineGame)
        {
            Debug.LogWarning(
                "[EOS SESSION] No se inicia el servidor porque se está saliendo."
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
        {
            return;
        }

        if (IsOperationInProgress)
        {
            Debug.LogWarning(
                "[EOS SESSION] Ya hay una operación EOS en proceso."
            );

            return;
        }

        if (IsHosting ||
            IsJoined)
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

        currentOperation =
            SessionOperation.Searching;

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
            currentOperation =
                SessionOperation.None;

            Debug.LogError(
                "[EOS SESSION] CreateSessionSearch: " +
                result
            );

            sessionSearch = null;

            return;
        }

        AttributeData searchAttribute =
            new AttributeData
            {
                Key = "Game",
                Value =
                    new AttributeDataValue
                    {
                        AsUtf8 = "PvZEcoPavo"
                    }
            };

        SessionSearchSetParameterOptions parameterOptions =
            new SessionSearchSetParameterOptions
            {
                ComparisonOp =
                    ComparisonOp.Equal,
                Parameter =
                    searchAttribute
            };

        result =
            sessionSearch.SetParameter(
                ref parameterOptions
            );

        if (result != Result.Success)
        {
            currentOperation =
                SessionOperation.None;

            Debug.LogError(
                "[EOS SESSION] SetParameter: " +
                result
            );

            ReleaseSearch();

            return;
        }

        ProductUserId localUserId =
            EOSManager.Instance.GetProductUserId();

        if (localUserId == null ||
            !localUserId.IsValid())
        {
            currentOperation =
                SessionOperation.None;

            Debug.LogError(
                "[EOS SESSION] ProductUserId no disponible."
            );

            ReleaseSearch();

            return;
        }

        SessionSearchFindOptions findOptions =
            new SessionSearchFindOptions
            {
                LocalUserId =
                    localUserId
            };

        IsSearching = true;

        Debug.Log(
            "[EOS SESSION] Buscando partidas online..."
        );

        sessionSearch.Find(
            ref findOptions,
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

        if (currentOperation ==
            SessionOperation.Searching)
        {
            currentOperation =
                SessionOperation.None;
        }

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

        SessionSearchGetSearchResultCountOptions countOptions =
            new SessionSearchGetSearchResultCountOptions();

        uint count =
            sessionSearch.GetSearchResultCount(
                ref countOptions
            );

        Debug.Log(
            "[EOS SESSION] Partidas encontradas: " +
            count
        );

        ReleaseFoundSession();

        ClearAvailableRooms();

        for (uint i = 0; i < count; i++)
        {
            SessionSearchCopySearchResultByIndexOptions indexOptions =
                new SessionSearchCopySearchResultByIndexOptions
                {
                    SessionIndex = i
                };

            Result result =
                sessionSearch.CopySearchResultByIndex(
                    ref indexOptions,
                    out SessionDetails details
                );

            if (result != Result.Success ||
                details == null)
            {
                continue;
            }

            SessionDetailsCopyInfoOptions infoOptions =
                new SessionDetailsCopyInfoOptions();

            result =
                details.CopyInfo(
                    ref infoOptions,
                    out SessionDetailsInfo? info
                );

            if (result != Result.Success ||
                !info.HasValue)
            {
                details.Release();
                continue;
            }

            uint maxConnections = 0;

            uint openConnections =
                info.Value.NumOpenPublicConnections;

            if (info.Value.Settings.HasValue)
            {
                maxConnections =
                    info.Value.Settings.Value.NumPublicConnections;
            }

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
            {
                roomName =
                    info.Value.SessionId;
            }

            EOSOnlineRoom room =
                new EOSOnlineRoom
                {
                    RoomName = roomName,
                    SessionId =
                        info.Value.SessionId,
                    HostUserId =
                        info.Value.OwnerUserId,
                    CurrentPlayers = players,
                    MaxPlayers =
                        maxConnections,
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
                    room.MaxPlayers +
                    " | Host: " +
                    room.HostUserId
                );

                if (foundSession == null)
                {
                    foundSession =
                        details;

                    foundHostUserId =
                        room.HostUserId;

                    Debug.Log(
                        "[EOS SESSION] Primera sala seleccionada: " +
                        room.RoomName
                    );
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

        if (foundSession != null)
        {
            Debug.Log(
                "[EOS SESSION] Hay una partida disponible."
            );
        }
        else
        {
            Debug.Log(
                "[EOS SESSION] No hay partidas disponibles."
            );
        }
    }

    private string GetSessionAttributeString(
        SessionDetails details,
        string key)
    {
        if (details == null)
        {
            return null;
        }

        for (uint i = 0; i < 64; i++)
        {
            SessionDetailsCopySessionAttributeByIndexOptions indexOptions =
                new SessionDetailsCopySessionAttributeByIndexOptions
                {
                    AttrIndex = i
                };

            SessionDetailsAttribute? attribute;

            Result result =
                details.CopySessionAttributeByIndex(
                    ref indexOptions,
                    out attribute
                );

            if (result != Result.Success)
            {
                break;
            }

            if (!attribute.HasValue)
            {
                continue;
            }

            AttributeData? data =
                attribute.Value.Data;

            if (!data.HasValue)
            {
                continue;
            }

            if (data.Value.Key != key)
            {
                continue;
            }

            AttributeDataValue value =
                data.Value.Value;

            return value.AsUtf8;
        }

        return null;
    }

    public EOSOnlineRoom GetAvailableRoom(
        int index)
    {
        if (index < 0 ||
            index >= availableRooms.Count)
        {
            return null;
        }

        return availableRooms[index];
    }

    public EOSOnlineRoom GetFirstAvailableRoom()
    {
        if (availableRooms.Count == 0)
        {
            return null;
        }

        return availableRooms[0];
    }

    public bool SelectRoom(
        int index)
    {
        EOSOnlineRoom room =
            GetAvailableRoom(index);

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

    public bool SelectRoom(
        EOSOnlineRoom room)
    {
        if (room == null)
        {
            return false;
        }

        if (room.Details == null)
        {
            Debug.LogError(
                "[EOS SESSION] La sala seleccionada no tiene SessionDetails."
            );

            return false;
        }

        if (room.HostUserId == null ||
            !room.HostUserId.IsValid())
        {
            Debug.LogError(
                "[EOS SESSION] La sala no tiene un HostUserId válido."
            );

            return false;
        }

        foundSession =
            room.Details;

        foundHostUserId =
            room.HostUserId;

        Debug.Log(
            "[EOS SESSION] Sala seleccionada: " +
            room.RoomName
        );

        return true;
    }

    public void JoinFirstOnlineGame()
    {
        EOSOnlineRoom room =
            GetFirstAvailableRoom();

        if (room != null)
        {
            SelectRoom(room);
        }

        JoinSelectedOnlineGame();
    }

    public void JoinSelectedOnlineGame()
    {
        if (!CanUseSessions())
        {
            return;
        }

        if (IsOperationInProgress)
        {
            Debug.LogWarning(
                "[EOS SESSION] Ya hay una operación EOS en proceso."
            );

            return;
        }

        if (foundSession == null)
        {
            Debug.LogError(
                "[EOS SESSION] No hay ninguna sala seleccionada."
            );

            return;
        }

        if (foundHostUserId == null ||
            !foundHostUserId.IsValid())
        {
            Debug.LogError(
                "[EOS SESSION] ProductUserId del host no disponible."
            );

            return;
        }

        ProductUserId localUserId =
            EOSManager.Instance.GetProductUserId();

        if (localUserId == null ||
            !localUserId.IsValid())
        {
            Debug.LogError(
                "[EOS SESSION] ProductUserId local no disponible."
            );

            return;
        }

        if (foundHostUserId == localUserId)
        {
            Debug.LogError(
                "[EOS SESSION] La partida encontrada pertenece al usuario local."
            );

            return;
        }

        string joinedSessionName =
            "PvZEcoPavo_Client_" +
            localUserId.ToString();

        localSessionName =
            joinedSessionName;

        currentOperation =
            SessionOperation.Joining;

        JoinSessionOptions joinOptions =
            new JoinSessionOptions
            {
                SessionHandle =
                    foundSession,
                SessionName =
                    joinedSessionName,
                LocalUserId =
                    localUserId,
                PresenceEnabled = false
            };

        Debug.Log(
            "[EOS SESSION] Uniéndose a partida..."
        );

        Debug.Log(
            "[EOS SESSION] Host ProductUserId: " +
            foundHostUserId
        );

        sessionsInterface.JoinSession(
            ref joinOptions,
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

        if (currentOperation ==
            SessionOperation.Joining)
        {
            currentOperation =
                SessionOperation.None;
        }

        /*
         * Si Salir fue pulsado mientras JoinSession
         * todavía estaba pendiente, no iniciamos P2P.
         */
        if (leavingOnlineGame)
        {
            Debug.LogWarning(
                "[EOS SESSION] JoinSession terminó durante el cierre."
            );

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

            Debug.LogError(
                "[EOS SESSION] No se pudo unir."
            );

            localSessionName = null;

            return;
        }

        IsJoined = true;
        IsHosting = false;

        Debug.Log(
            "[EOS SESSION] PARTIDA ONLINE UNIDA."
        );

        Debug.Log(
            "[EOS SESSION] SessionName local: " +
            localSessionName
        );

        if (foundHostUserId == null ||
            !foundHostUserId.IsValid())
        {
            Debug.LogError(
                "[EOS SESSION] El host no tiene un ProductUserId válido."
            );

            return;
        }

        StartOnlineClient();
    }

    private void StartOnlineClient()
    {
        if (leavingOnlineGame)
        {
            Debug.LogWarning(
                "[EOS SESSION] No se inicia el cliente porque se está saliendo."
            );

            return;
        }

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

        if (!EOSOnlineClient.Instance.ConnectToHost(
                foundHostUserId))
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
        {
            Debug.Log(
                "[EOS SESSION] LeaveOnlineGame ya está en proceso."
            );

            return;
        }

        if (!IsHosting &&
            !IsJoined &&
            string.IsNullOrEmpty(localSessionName) &&
            currentOperation == SessionOperation.None)
        {
            return;
        }

        /*
         * IMPORTANTE:
         *
         * Marcamos la salida ANTES de tocar ningún objeto.
         * De esta manera cualquier callback EOS que llegue
         * después sabe que no debe volver a iniciar P2P.
         */
        leavingOnlineGame = true;

        currentOperation =
            SessionOperation.Leaving;

        Debug.Log(
            "[EOS SESSION] Saliendo de sesión..."
        );

        /*
         * 1. Detener lógica del servidor.
         *
         * StopServer() NO toca P2P.
         */
        if (EOSOnlineServer.Instance != null)
        {
            EOSOnlineServer.Instance.StopServer();
        }

        /*
         * 2. Limpiar estado lógico del cliente.
         *
         * EOSOnlineClient ya NO desconecta el transporte.
         */
        if (EOSOnlineClient.Instance != null)
        {
            EOSOnlineClient.Instance.Disconnect();
        }

        /*
         * 3. Desconectar P2P una sola vez.
         */
        if (EOSOnlineTransport.Instance != null)
        {
            EOSOnlineTransport.Instance.Disconnect();
        }

        /*
         * 4. Destruir la sesión EOS.
         */
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
        {
            return;
        }

        if (sessionsInterface == null ||
            string.IsNullOrEmpty(localSessionName))
        {
            FinishLeaveCleanup();
            return;
        }

        destroyingSession = true;

        DestroySessionOptions destroyOptions =
            new DestroySessionOptions
            {
                SessionName =
                    localSessionName
            };

        Debug.Log(
            "[EOS SESSION] Destruyendo sesión: " +
            localSessionName
        );

        sessionsInterface.DestroySession(
            ref destroyOptions,
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

        currentOperation =
            SessionOperation.None;

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
        {
            TryInitialize();
        }

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
        for (int i = 0;
             i < availableRooms.Count;
             i++)
        {
            EOSOnlineRoom room =
                availableRooms[i];

            if (room == null ||
                room.Details == null)
            {
                continue;
            }

            if (room.Details == foundSession)
            {
                continue;
            }

            room.Details.Release();
        }

        availableRooms.Clear();
    }

    private void ReleaseSessionModification()
    {
        if (sessionModification != null)
        {
            sessionModification.Release();

            sessionModification = null;
        }
    }

    private void ReleaseFoundSession()
    {
        if (foundSession != null)
        {
            bool belongsToRoom = false;

            for (int i = 0;
                 i < availableRooms.Count;
                 i++)
            {
                EOSOnlineRoom room =
                    availableRooms[i];

                if (room != null &&
                    room.Details == foundSession)
                {
                    belongsToRoom = true;
                    break;
                }
            }

            if (!belongsToRoom)
            {
                foundSession.Release();
            }

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

        if (currentOperation ==
            SessionOperation.Searching)
        {
            currentOperation =
                SessionOperation.None;
        }
    }

    private void OnApplicationQuit()
    {
        /*
         * No ejecutamos LeaveOnlineGame aquí.
         *
         * Unity está cerrando y EOSManager/Transport
         * se encargan de su propio ciclo de destrucción.
         */
        leavingOnlineGame = true;

        currentOperation =
            SessionOperation.Leaving;
    }

    private void OnDestroy()
    {
        ReleaseSessionModification();

        ReleaseFoundSession();

        ClearAvailableRooms();

        ReleaseSearch();

        sessionsInterface = null;

        if (Instance == this)
        {
            Instance = null;
        }
    }
}