using UnityEngine;

public class EOSOnlineUI : MonoBehaviour
{
    public static EOSOnlineUI Instance;

    private bool operationInProgress;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void CreateOnlineGame()
    {
        if (operationInProgress)
        {
            return;
        }

        if (EOSAutoLogin.Instance == null)
        {
            Debug.LogError(
                "[EOS UI] EOSAutoLogin no existe."
            );

            return;
        }

        operationInProgress = true;

        Debug.Log(
            "[EOS UI] Iniciando creación de partida..."
        );

        EOSAutoLogin.Instance.Login(
            OnCreateLoginFinished
        );
    }

    private void OnCreateLoginFinished(
        bool success)
    {
        if (!success)
        {
            operationInProgress = false;

            Debug.LogError(
                "[EOS UI] No se pudo iniciar sesión."
            );

            return;
        }

        if (EOSOnlineSession.Instance == null)
        {
            operationInProgress = false;

            Debug.LogError(
                "[EOS UI] EOSOnlineSession no existe."
            );

            return;
        }

        Debug.Log(
            "[EOS UI] Login correcto. Creando partida..."
        );

        EOSOnlineSession.Instance.CreateOnlineGame();

        operationInProgress = false;
    }

    public void SearchOnlineGames()
    {
        if (operationInProgress)
        {
            return;
        }

        if (EOSAutoLogin.Instance == null)
        {
            Debug.LogError(
                "[EOS UI] EOSAutoLogin no existe."
            );

            return;
        }

        operationInProgress = true;

        Debug.Log(
            "[EOS UI] Iniciando búsqueda de partidas..."
        );

        EOSAutoLogin.Instance.Login(
            OnSearchLoginFinished
        );
    }

    private void OnSearchLoginFinished(
        bool success)
    {
        if (!success)
        {
            operationInProgress = false;

            Debug.LogError(
                "[EOS UI] No se pudo iniciar sesión."
            );

            return;
        }

        if (EOSOnlineSession.Instance == null)
        {
            operationInProgress = false;

            Debug.LogError(
                "[EOS UI] EOSOnlineSession no existe."
            );

            return;
        }

        Debug.Log(
            "[EOS UI] Login correcto. Buscando partidas..."
        );

        EOSOnlineSession.Instance.SearchOnlineGames();

        operationInProgress = false;
    }

    public void JoinOnlineGame()
    {
        if (operationInProgress)
        {
            return;
        }

        if (EOSAutoLogin.Instance == null)
        {
            Debug.LogError(
                "[EOS UI] EOSAutoLogin no existe."
            );

            return;
        }

        operationInProgress = true;

        Debug.Log(
            "[EOS UI] Preparando unión a partida..."
        );

        EOSAutoLogin.Instance.Login(
            OnJoinLoginFinished
        );
    }

    private void OnJoinLoginFinished(
        bool success)
    {
        if (!success)
        {
            operationInProgress = false;

            Debug.LogError(
                "[EOS UI] No se pudo iniciar sesión."
            );

            return;
        }

        if (EOSOnlineSession.Instance == null)
        {
            operationInProgress = false;

            Debug.LogError(
                "[EOS UI] EOSOnlineSession no existe."
            );

            return;
        }

        Debug.Log(
            "[EOS UI] Login correcto. Uniéndose a partida..."
        );

        EOSOnlineSession.Instance.JoinFirstOnlineGame();

        operationInProgress = false;
    }

    public void LeaveOnlineGame()
    {
        if (EOSOnlineSession.Instance == null)
        {
            return;
        }

        Debug.Log(
            "[EOS UI] Saliendo de partida online..."
        );

        EOSOnlineSession.Instance.LeaveOnlineGame();

        operationInProgress = false;
    }
}