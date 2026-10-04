using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class UnityServicesInitializer : MonoBehaviour
{
    public static UnityServicesInitializer Instance;

    public bool IsInitialized { get; private set; }
    public bool IsAuthenticated =>
        AuthenticationService.Instance != null &&
        AuthenticationService.Instance.IsSignedIn;

    public string PlayerId =>
        IsAuthenticated
            ? AuthenticationService.Instance.PlayerId
            : "";

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        await InitializeServices();
    }

    public async System.Threading.Tasks.Task<bool> InitializeServices()
    {
        if (IsInitialized &&
            AuthenticationService.Instance != null &&
            AuthenticationService.Instance.IsSignedIn)
        {
            return true;
        }

        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance
                    .SignInAnonymouslyAsync();
            }

            IsInitialized = true;

            Debug.Log(
                "[UnityServicesInitializer] " +
                "Unity Services inicializados correctamente.");

            Debug.Log(
                "[UnityServicesInitializer] PlayerId: " +
                AuthenticationService.Instance.PlayerId);

            return true;
        }
        catch (AuthenticationException ex)
        {
            IsInitialized = false;

            Debug.LogError(
                "[UnityServicesInitializer] " +
                "Error de Authentication: " +
                ex);

            return false;
        }
        catch (RequestFailedException ex)
        {
            IsInitialized = false;

            Debug.LogError(
                "[UnityServicesInitializer] " +
                "Error de Unity Services: " +
                ex);

            return false;
        }
        catch (Exception ex)
        {
            IsInitialized = false;

            Debug.LogError(
                "[UnityServicesInitializer] " +
                "Error inicializando Unity Services: " +
                ex);

            return false;
        }
    }
}