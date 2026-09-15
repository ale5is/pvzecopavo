using UnityEngine;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using PlayEveryWare.EpicOnlineServices;

public class EOSAutoLogin : MonoBehaviour
{
    public static EOSAutoLogin Instance;

    public ProductUserId LocalProductUserId { get; private set; }

    public bool IsLoggedIn { get; private set; }

    private bool loginInProgress;
    private bool creatingUser;

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
        if (loginInProgress || IsLoggedIn)
        {
            return;
        }

        if (EOSManager.Instance == null)
        {
            Debug.LogError(
                "[EOS] EOSManager no existe."
            );

            return;
        }

        ConnectInterface connectInterface =
            EOSManager.Instance.GetEOSConnectInterface();

        if (connectInterface == null)
        {
            Debug.LogError(
                "[EOS] ConnectInterface no está disponible."
            );

            return;
        }

        loginInProgress = true;

        string displayName =
            System.Environment.UserName;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Jugador";
        }

        Debug.Log(
            "[EOS] Buscando Device ID existente e iniciando login..."
        );

        EOSManager.Instance.StartConnectLoginWithOptions(
            ExternalCredentialType.DeviceidAccessToken,
            null,
            displayName,
            OnConnectLogin
        );
    }

    private void OnConnectLogin(
        LoginCallbackInfo data)
    {
        Debug.Log(
            "[EOS] Connect Login: " +
            data.ResultCode
        );

        if (data.ResultCode == Result.Success)
        {
            LocalProductUserId =
                data.LocalUserId;

            IsLoggedIn = true;
            loginInProgress = false;

            Debug.Log(
                "[EOS] LOGIN CORRECTO"
            );

            Debug.Log(
                "[EOS] ProductUserId: " +
                LocalProductUserId
            );

            return;
        }

        if (data.ResultCode == Result.InvalidUser)
        {
            if (data.ContinuanceToken == null)
            {
                loginInProgress = false;

                Debug.LogError(
                    "[EOS] InvalidUser pero " +
                    "ContinuanceToken es null."
                );

                return;
            }

            Debug.Log(
                "[EOS] Device ID encontrado, " +
                "pero todavía no tiene Product User asociado."
            );

            CreateProductUser(
                data.ContinuanceToken
            );

            return;
        }

        loginInProgress = false;

        Debug.LogError(
            "[EOS] Connect Login falló: " +
            data.ResultCode
        );
    }

    private void CreateProductUser(
        ContinuanceToken continuanceToken
    )
    {
        if (creatingUser)
        {
            return;
        }

        if (EOSManager.Instance == null)
        {
            loginInProgress = false;

            Debug.LogError(
                "[EOS] EOSManager no existe."
            );

            return;
        }

        creatingUser = true;

        Debug.Log(
            "[EOS] Creando Product User..."
        );

        EOSManager.Instance.CreateConnectUserWithContinuanceToken(
            continuanceToken,
            OnCreateConnectUser
        );
    }

    private void OnCreateConnectUser(
        CreateUserCallbackInfo data
    )
    {
        creatingUser = false;

        Debug.Log(
            "[EOS] Create Connect User: " +
            data.ResultCode
        );

        if (data.ResultCode != Result.Success)
        {
            loginInProgress = false;

            Debug.LogError(
                "[EOS] No se pudo crear el Product User: " +
                data.ResultCode
            );

            return;
        }

        Debug.Log(
            "[EOS] Product User creado correctamente."
        );

        Debug.Log(
            "[EOS] Reintentando login..."
        );

        LoginWithDeviceId();
    }

    private void LoginWithDeviceId()
    {
        if (EOSManager.Instance == null)
        {
            loginInProgress = false;

            Debug.LogError(
                "[EOS] EOSManager no existe."
            );

            return;
        }

        string displayName =
            System.Environment.UserName;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Jugador";
        }

        EOSManager.Instance.StartConnectLoginWithOptions(
            ExternalCredentialType.DeviceidAccessToken,
            null,
            displayName,
            OnConnectLogin
        );
    }
}