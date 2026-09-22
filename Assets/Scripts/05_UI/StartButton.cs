using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StartButton : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, IPointerClickHandler
{
    public Text StartText;

    private Image LightImage;

    private void Awake()
    {
        LightImage = base.transform.Find("Light").GetComponent<Image>();
        LightImage.transform.localScale = Vector3.zero;

        Debug.Log(
            "[StartButton] Awake | " +
            "GameObject=" + gameObject.name
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        LightImage.transform.localScale = Vector3.one;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        LightImage.transform.localScale = Vector3.zero;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log(
            "[StartButton] ======================================="
        );

        Debug.Log(
            "[StartButton] CLICK RECIBIDO"
        );

        Debug.Log(
            "[StartButton] GameManager.Instance=" +
            (GameManager.Instance != null)
        );

        if (GameManager.Instance != null)
        {
            Debug.Log(
                "[StartButton] isClient=" +
                GameManager.Instance.isClient +
                " | isServer=" +
                GameManager.Instance.isServer
            );
        }

        if (SeedBank.Instance == null)
        {
            Debug.LogError(
                "[StartButton] SeedBank.Instance == NULL"
            );
            return;
        }

        Debug.Log(
            "[StartButton] Guardando cartas seleccionadas..."
        );

        SeedBank.Instance.SaveSelectedCard();

        if (AudioManager.Instance != null &&
            GameManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.ButtonClick,
                base.transform.position,
                isAll: true
            );
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError(
                "[StartButton] GameManager.Instance == NULL"
            );
            return;
        }

        // CLIENTE
        if (GameManager.Instance.isClient)
        {
            Debug.Log(
                "[StartButton] MODO CLIENTE"
            );

            if (SpectatorList.Instance == null)
            {
                Debug.LogError(
                    "[StartButton] SpectatorList.Instance == NULL"
                );
                return;
            }

            Debug.Log(
                "[StartButton] LocalIsSpectator=" +
                SpectatorList.Instance.LocalIsSpectator
            );

            if (!SpectatorList.Instance.LocalIsSpectator)
            {
                Debug.Log(
                    "[StartButton] Cliente -> Prepare()"
                );

                if (SeedChooser.Instance == null)
                {
                    Debug.LogError(
                        "[StartButton] SeedChooser.Instance == NULL"
                    );
                    return;
                }

                if (ZombieChooser.Instance == null)
                {
                    Debug.LogError(
                        "[StartButton] ZombieChooser.Instance == NULL"
                    );
                    return;
                }

                SeedChooser.Instance.Prepare();
                ZombieChooser.Instance.Prepare();

                Debug.Log(
                    "[StartButton] Cliente -> Prepare() TERMINADO"
                );
            }

            Debug.Log(
                "[StartButton] Cliente -> RETURN"
            );

            return;
        }

        // SERVIDOR / HOST
        if (GameManager.Instance.isServer)
        {
            Debug.Log(
                "[StartButton] MODO SERVIDOR / HOST"
            );

            if (BattlePlayerList.Instance == null)
            {
                Debug.LogError(
                    "[StartButton] BattlePlayerList.Instance == NULL"
                );
                return;
            }

            Debug.Log(
                "[StartButton] Ejecutando CheckPrepare()..."
            );

            bool canStart =
                BattlePlayerList.Instance.CheckPrepare();

            Debug.Log(
                "[StartButton] CheckPrepare() = " +
                canStart
            );

            if (!canStart)
            {
                Debug.LogWarning(
                    "[StartButton] INICIO BLOQUEADO POR CheckPrepare()"
                );

                Debug.Log(
                    "[StartButton] ======================================="
                );

                return;
            }

            Debug.Log(
                "[StartButton] CheckPrepare() OK"
            );

            if (OnlineNetworkServer.Instance == null)
            {
                Debug.LogError(
                    "[StartButton] OnlineNetworkServer.Instance == NULL"
                );
                return;
            }

            Debug.Log(
                "[StartButton] LLAMANDO OnlineNetworkServer.StartRunLv()..."
            );

            OnlineNetworkServer.Instance.StartRunLv();

            Debug.Log(
                "[StartButton] OnlineNetworkServer.StartRunLv() TERMINADO"
            );
        }
        else
        {
            Debug.LogWarning(
                "[StartButton] NO ES CLIENTE NI SERVIDOR"
            );
        }

        Debug.Log(
            "[StartButton] Ejecutando SeedChooser.StartRunLv()..."
        );

        if (SeedChooser.Instance == null)
        {
            Debug.LogError(
                "[StartButton] SeedChooser.Instance == NULL"
            );
            return;
        }

        SeedChooser.Instance.StartRunLv();

        Debug.Log(
            "[StartButton] SeedChooser.StartRunLv() TERMINADO"
        );

        Debug.Log(
            "[StartButton] Ejecutando ZombieChooser.StartRunLv()..."
        );

        if (ZombieChooser.Instance == null)
        {
            Debug.LogError(
                "[StartButton] ZombieChooser.Instance == NULL"
            );
            return;
        }

        ZombieChooser.Instance.StartRunLv();

        Debug.Log(
            "[StartButton] ZombieChooser.StartRunLv() TERMINADO"
        );

        Debug.Log(
            "[StartButton] ======================================="
        );
    }
}