using UnityEngine;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using PlayEveryWare.EpicOnlineServices;

public class EOSAutoLogin : MonoBehaviour
{
    public static EOSAutoLogin Instance;

    public ProductUserId LocalProductUserId { get; private set; }

    public bool IsLoggedIn { get; private set; }

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
        Invoke(nameof(StartDeviceLogin), 1f);
    }

    private void StartDeviceLogin()
    {
        if (EOSManager.Instance == null)
        {
            Debug.LogError("[EOS] EOSManager no existe.");
            return;
        }

        ConnectInterface connectInterface = EOSManager.Instance.GetEOSConnectInterface();

        if (connectInterface == null)
        {
            Debug.LogError("[EOS] ConnectInterface no está disponible.");
            return;
        }

        CreateDeviceIdOptions options = new CreateDeviceIdOptions
        {
            DeviceModel = SystemInfo.deviceModel
        };

        Debug.Log("[EOS] Creando/registrando Device ID...");

        connectInterface.CreateDeviceId(
            ref options,
            null,
            OnCreateDeviceId
        );
    }

    private void OnCreateDeviceId(ref CreateDeviceIdCallbackInfo data)
    {
        Debug.Log("[EOS] CreateDeviceId: " + data.ResultCode);

        ConnectInterface connectInterface = EOSManager.Instance.GetEOSConnectInterface();

        if (connectInterface == null)
        {
            Debug.LogError("[EOS] ConnectInterface no disponible después de CreateDeviceId.");
            return;
        }

        LoginWithDeviceId();
    }

    private void LoginWithDeviceId()
    {
        string displayName = System.Environment.UserName;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Jugador";
        }

        Debug.Log("[EOS] Iniciando Connect Login con Device ID...");

        EOSManager.Instance.StartConnectLoginWithOptions(
            ExternalCredentialType.DeviceidAccessToken,
            null,
            displayName,
            OnConnectLogin
        );
    }

    private void OnConnectLogin(LoginCallbackInfo data)
    {
        Debug.Log("[EOS] Connect Login: " + data.ResultCode);

        if (data.ResultCode == Result.Success)
        {
            LocalProductUserId = data.LocalUserId;
            IsLoggedIn = true;

            Debug.Log("[EOS] LOGIN CORRECTO");
            Debug.Log("[EOS] ProductUserId: " + LocalProductUserId);
            return;
        }

        if (data.ResultCode == Result.InvalidUser)
        {
            Debug.LogError(
                "[EOS] El Device ID todavía no tiene un Product User asociado."
            );

            if (data.ContinuanceToken == null)
            {
                Debug.LogError("[EOS] ContinuanceToken es null.");
                return;
            }

            EOSManager.Instance.CreateConnectUserWithContinuanceToken(
                data.ContinuanceToken,
                OnCreateConnectUser
            );

            return;
        }

        Debug.LogError(
            "[EOS] Connect Login falló: " + data.ResultCode
        );
    }

    private void OnCreateConnectUser(CreateUserCallbackInfo data)
    {
        Debug.Log("[EOS] Create Connect User: " + data.ResultCode);

        if (data.ResultCode != Result.Success)
        {
            Debug.LogError(
                "[EOS] No se pudo crear el Product User: " +
                data.ResultCode
            );

            return;
        }

        Debug.Log("[EOS] Product User creado. Reintentando Connect Login...");

        LoginWithDeviceId();
    }
}