using UnityEngine;

public class EOSTestButtons : MonoBehaviour
{
    public void CrearPartida()
    {
        if (EOSOnlineSession.Instance == null)
        {
            Debug.LogError("[EOS TEST] EOSOnlineSession no existe.");
            return;
        }

        EOSOnlineSession.Instance.CreateOnlineGame();
    }

    public void BuscarPartidas()
    {
        if (EOSOnlineSession.Instance == null)
        {
            Debug.LogError("[EOS TEST] EOSOnlineSession no existe.");
            return;
        }

        EOSOnlineSession.Instance.SearchOnlineGames();
    }

    public void Unirse()
    {
        if (EOSOnlineSession.Instance == null)
        {
            Debug.LogError("[EOS TEST] EOSOnlineSession no existe.");
            return;
        }

        EOSOnlineSession.Instance.JoinFirstOnlineGame();
    }

    public void Salir()
    {
        if (EOSOnlineSession.Instance == null)
        {
            Debug.LogError("[EOS TEST] EOSOnlineSession no existe.");
            return;
        }

        EOSOnlineSession.Instance.LeaveOnlineGame();
    }
}