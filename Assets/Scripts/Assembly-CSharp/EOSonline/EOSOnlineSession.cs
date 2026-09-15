using UnityEngine;
using Epic.OnlineServices;
using Epic.OnlineServices.Sessions;
using PlayEveryWare.EpicOnlineServices;

public class EOSOnlineSession : MonoBehaviour
{
    public static EOSOnlineSession Instance;

    [Header("Configuración")]
    [SerializeField] private string bucketId = "PvZEcoPavo";
    [SerializeField] private uint maxPlayers = 4;

    private SessionsInterface sessionsInterface;
    private SessionModification sessionModification;
    private SessionSearch sessionSearch;
    private SessionDetails foundSession;

    private ProductUserId foundHostUserId;

    private string localSessionName;

    private bool sessionsInitialized;

    public bool IsHosting { get; private set; }
    public bool IsJoined { get; private set; }
    public bool IsSearching { get; private set; }

    public string LocalSessionName => localSessionName;

    public bool IsInitialized => sessionsInitialized;

    public ProductUserId HostUserId => foundHostUserId;

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

    public void CreateOnlineGame()
    {
        if (!CanUseSessions())
        {
            return;
        }

        if (IsHosting || IsJoined)
        {
            Debug.LogWarning(
                "[EOS SESSION] Ya estás dentro de una sesión."
            );

            return;
        }

        ProductUserId localUserId =
            EOSManager.Instance.GetProductUserId();

        if (localUserId == null ||
            !localUserId.IsValid())
        {
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

        Result result =
            sessionsInterface.CreateSessionModification(
                ref createOptions,
                out sessionModification
            );

        if (result != Result.Success)
        {
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
            Debug.LogError(
                "[EOS SESSION] SetPermissionLevel: " +
                result
            );

            ReleaseSessionModification();
            localSessionName = null;

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
            Debug.LogError(
                "[EOS SESSION] SetJoinInProgressAllowed: " +
                result
            );

            ReleaseSessionModification();
            localSessionName = null;

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
            Debug.LogError(
                "[EOS SESSION] SetInvitesAllowed: " +
                result
            );

            ReleaseSessionModification();
            localSessionName = null;

            return;
        }

        AttributeData gameAttribute =
            new AttributeData
            {
                Key = "Game",
                Value = new AttributeDataValue
                {
                    AsUtf8 = "PvZEcoPavo"
                }
            };

        SessionModificationAddAttributeOptions attributeOptions =
            new SessionModificationAddAttributeOptions
            {
                SessionAttribute = gameAttribute,
                AdvertisementType =
                    SessionAttributeAdvertisementType.Advertise
            };

        result =
            sessionModification.AddAttribute(
                ref attributeOptions
            );

        if (result != Result.Success)
        {
            Debug.LogError(
                "[EOS SESSION] AddAttribute Game: " +
                result
            );

            ReleaseSessionModification();
            localSessionName = null;

            return;
        }

        AttributeData versionAttribute =
            new AttributeData
            {
                Key = "GameVersion",
                Value = new AttributeDataValue
                {
                    AsUtf8 = "0.7"
                }
            };

        attributeOptions.SessionAttribute =
            versionAttribute;

        result =
            sessionModification.AddAttribute(
                ref attributeOptions
            );

        if (result != Result.Success)
        {
            Debug.LogError(
                "[EOS SESSION] AddAttribute GameVersion: " +
                result
            );

            ReleaseSessionModification();
            localSessionName = null;

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

    private void OnSessionCreated(
        ref UpdateSessionCallbackInfo data)
    {
        Debug.Log(
            "[EOS SESSION] Create/Update: " +
            data.ResultCode
        );

        ReleaseSessionModification();

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
            "[EOS SESSION] Host ProductUserId: " +
            foundHostUserId
        );

        StartOnlineServer();
    }

    private void StartOnlineServer()
    {
        if (EOSOnlineServer.Instance == null)
        {
            Debug.LogError(
                "[EOS SESSION] EOSOnlineServer no existe en la escena."
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

        if (IsHosting || IsJoined)
        {
            Debug.LogWarning(
                "[EOS SESSION] Ya estás dentro de una sesión."
            );

            return;
        }

        ReleaseFoundSession();
        ReleaseSearch();

        foundHostUserId = null;

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
                Value = new AttributeDataValue
                {
                    AsUtf8 = "PvZEcoPavo"
                }
            };

        SessionSearchSetParameterOptions parameterOptions =
            new SessionSearchSetParameterOptions
            {
                ComparisonOp = ComparisonOp.Equal,
                Parameter = searchAttribute
            };

        result =
            sessionSearch.SetParameter(
                ref parameterOptions
            );

        if (result != Result.Success)
        {
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
            Debug.LogError(
                "[EOS SESSION] ProductUserId no disponible."
            );

            ReleaseSearch();

            return;
        }

        SessionSearchFindOptions findOptions =
            new SessionSearchFindOptions
            {
                LocalUserId = localUserId
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

        if (data.ResultCode != Result.Success)
        {
            Debug.LogError(
                "[EOS SESSION] Error buscando partidas."
            );

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

            Debug.Log(
                "[EOS SESSION] Encontrada: " +
                info.Value.SessionId +
                " | Jugadores: " +
                players +
                "/" +
                maxConnections
            );

            Debug.Log(
                "[EOS SESSION] Host ProductUserId: " +
                info.Value.OwnerUserId
            );

            if (foundSession == null &&
                openConnections > 0 &&
                info.Value.OwnerUserId != null &&
                info.Value.OwnerUserId.IsValid())
            {
                foundSession = details;
                foundHostUserId = info.Value.OwnerUserId;

                Debug.Log(
                    "[EOS SESSION] Partida seleccionada."
                );

                Debug.Log(
                    "[EOS SESSION] Host: " +
                    foundHostUserId
                );
            }
            else
            {
                details.Release();
            }
        }

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

    public void JoinFirstOnlineGame()
    {
        if (!CanUseSessions())
        {
            return;
        }

        if (foundSession == null)
        {
            Debug.LogError(
                "[EOS SESSION] Primero ejecutá SearchOnlineGames()."
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

        JoinSessionOptions joinOptions =
            new JoinSessionOptions
            {
                SessionHandle = foundSession,
                SessionName = joinedSessionName,
                LocalUserId = localUserId,
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

        if (data.ResultCode != Result.Success)
        {
            IsJoined = false;
            IsHosting = false;

            Debug.LogError(
                "[EOS SESSION] No se pudo unir."
            );

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
        if (EOSOnlineClient.Instance == null)
        {
            Debug.LogError(
                "[EOS SESSION] EOSOnlineClient no existe en la escena."
            );

            return;
        }

        if (EOSOnlineTransport.Instance == null)
        {
            Debug.LogError(
                "[EOS SESSION] EOSOnlineTransport no existe en la escena."
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
        if (sessionsInterface == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(localSessionName))
        {
            return;
        }

        if (EOSOnlineClient.Instance != null)
        {
            EOSOnlineClient.Instance.Disconnect();
        }

        if (EOSOnlineServer.Instance != null &&
            IsHosting)
        {
            EOSOnlineServer.Instance.StopServer();
        }

        if (EOSOnlineTransport.Instance != null)
        {
            EOSOnlineTransport.Instance.Disconnect();
        }

        DestroySessionOptions destroyOptions =
            new DestroySessionOptions
            {
                SessionName = localSessionName
            };

        Debug.Log(
            "[EOS SESSION] Saliendo de sesión..."
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

        IsHosting = false;
        IsJoined = false;
        IsSearching = false;

        ReleaseFoundSession();
        ReleaseSearch();

        foundHostUserId = null;
        localSessionName = null;
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
    }

    private void OnDestroy()
    {
        ReleaseSessionModification();
        ReleaseFoundSession();
        ReleaseSearch();

        sessionsInterface = null;

        if (Instance == this)
        {
            Instance = null;
        }
    }
}