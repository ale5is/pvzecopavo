using UnityEngine;
using UnityEngine.UI;

public class GuestControlsOptions : MonoBehaviour
{
    [Header("Referencia")]
    public GuestPlayerManager manager;

    [Header("Pagina de controles")]
    public GameObject controlsPage;

    [Header("Texto opcional")]
    public Text statusText;

    public void Open()
    {
        if (controlsPage != null)
            controlsPage.SetActive(true);

        Refresh();
    }

    public void Close()
    {
        if (controlsPage != null)
            controlsPage.SetActive(false);
    }

    public void SetKeyboard(
        int playerNumber)
    {
        if (!GetManager())
            return;

        manager.SetPlayerKeyboard(
            playerNumber
        );

        Refresh();
    }

    public void SetMouse(
        int playerNumber)
    {
        if (!GetManager())
            return;

        manager.SetPlayerMouse(
            playerNumber
        );

        Refresh();
    }

    public void SetGamepad1(
        int playerNumber)
    {
        SetGamepad(
            playerNumber,
            1
        );
    }

    public void SetGamepad2(
        int playerNumber)
    {
        SetGamepad(
            playerNumber,
            2
        );
    }

    public void SetGamepad3(
        int playerNumber)
    {
        SetGamepad(
            playerNumber,
            3
        );
    }

    public void SetGamepad4(
        int playerNumber)
    {
        SetGamepad(
            playerNumber,
            4
        );
    }

    public void SetGamepad(
        int playerNumber,
        int gamepadNumber)
    {
        if (!GetManager())
            return;

        manager.SetPlayerGamepad(
            playerNumber,
            gamepadNumber
        );

        Refresh();
    }

    public void SetWASD(
        int playerNumber)
    {
        if (!GetManager())
            return;

        manager.ApplyKeyboardPreset(
            playerNumber,
            0
        );

        Refresh();
    }

    public void SetArrows(
        int playerNumber)
    {
        if (!GetManager())
            return;

        manager.ApplyKeyboardPreset(
            playerNumber,
            1
        );

        Refresh();
    }

    public void SetIJKL(
        int playerNumber)
    {
        if (!GetManager())
            return;

        manager.ApplyKeyboardPreset(
            playerNumber,
            2
        );

        Refresh();
    }

    public void DisablePlayer(
        int playerNumber)
    {
        if (!GetManager())
            return;

        manager.DisablePlayer(
            playerNumber
        );

        Refresh();
    }

    public void RestoreDefaults()
    {
        if (!GetManager())
            return;

        manager.ResetControlsToDefaults();

        Refresh();
    }

    public void Refresh()
    {
        if (!GetManager())
            return;

        if (statusText != null)
        {
            statusText.text =
                "Controles locales configurados para 4 jugadores.";
        }
    }

    private bool GetManager()
    {
        if (manager != null)
            return true;

        manager =
            GuestPlayerManager.Instance;

        if (manager != null)
            return true;

        manager =
            FindObjectOfType<
                GuestPlayerManager
            >();

        return manager != null;
    }
}