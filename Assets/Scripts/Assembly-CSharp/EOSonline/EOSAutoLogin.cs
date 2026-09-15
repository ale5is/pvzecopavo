using System;
using UnityEngine;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using PlayEveryWare.EpicOnlineServices;

public class EOSAutoLogin : MonoBehaviour
{
    public static EOSAutoLogin Instance;

    public ProductUserId LocalProductUserId { get; private set; }

    public bool IsLoggedIn { get; private set; }

    public bool IsLoginInProgress
    {
        get
        {
            return loginInProgress || creatingUser;
        }
    }

    private bool loginInProgress;
    private bool creatingUser;

    private Action<bool> loginCallback;

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

    public void Login(Action<bool> callback = null)
    {
        if (IsLoggedIn)
        {
            callback?.Invoke(true);
            return;
        }

        if (callback != null)
        {
            loginCallback += callback;
        }

        if (loginInProgress || creatingUser)
        {
            return;
        }

        if (EOSManager.Instance == null)
        {
            Debug.LogError(
                "[EOS] EOSManager no existe."
            );

            CompleteLogin(false);
            return;
        }

        ConnectInterface connectInterface =
            EOSManager.Instance.GetEOSConnectInterface();

        if (connectInterface == null)
        {
            Debug.LogError(
                "[EOS] ConnectInterface no está disponible."
            );

            CompleteLogin(false);
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
            "[EOS] Iniciando login de Device ID..."
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

            IsLoggedIn =
                LocalProductUserId != null &&
                LocalProductUserId.IsValid();

            loginInProgress = false;

            if (!IsLoggedIn)
            {
                Debug.LogError(
                    "[EOS] Login correcto pero ProductUserId inválido."
                );

                CompleteLogin(false);
                return;
            }

            Debug.Log(
                "[EOS] LOGIN CORRECTO"
            );

            Debug.Log(
                "[EOS] ProductUserId: " +
                LocalProductUserId
            );

            CompleteLogin(true);

            return;
        }

        if (data.ResultCode == Result.InvalidUser)
        {
            if (data.ContinuanceToken == null)
            {
                loginInProgress = false;

                Debug.LogError(
                    "[EOS] InvalidUser pero ContinuanceToken es null."
                );

                CompleteLogin(false);
                return;
            }

            Debug.Log(
                "[EOS] Device ID sin Product User. " +
                "Creando usuario..."
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

        CompleteLogin(false);
    }

    private void CreateProductUser(
        ContinuanceToken continuanceToken)
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

            CompleteLogin(false);
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
        CreateUserCallbackInfo data)
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

            CompleteLogin(false);

            return;
        }

        Debug.Log(
            "[EOS] Product User creado correctamente."
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

            CompleteLogin(false);

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

    private void CompleteLogin(
        bool success)
    {
        Action<bool> callback =
            loginCallback;

        loginCallback = null;

        callback?.Invoke(success);
    }
}