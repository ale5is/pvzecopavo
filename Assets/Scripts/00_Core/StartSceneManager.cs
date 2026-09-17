using UnityEngine;

public class StartSceneManager : MonoBehaviour
{
    public static StartSceneManager Instance;

    public Animator BackLeft;
    public Animator BackCenter;
    public Animator BackRight;

    public ChangeUser changeUser;

    public Transform AlmanacTransform;
    public Transform StoreTransform;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // La inicializacin del jugador ahora la controla GameManager.
    }

    public void LoadStartScence(bool PlayAnim)
    {
        if (GameManager.Instance == null ||
            GameManager.Instance.LocalPlayerSave == null)
        {
            return;
        }

        if (GameManager.Instance.LocalPlayerSave.StoreLvl > 0)
        {
            if (StoreTransform != null)
            {
                StoreTransform.localScale =
                    new Vector3(0.6f, 0.6f, 0.6f);
            }
        }
        else
        {
            if (StoreTransform != null)
            {
                StoreTransform.localScale =
                    Vector3.zero;
            }
        }

        if (GameManager.Instance.LocalPlayerSave.AlmanacUnLock)
        {
            if (AlmanacTransform != null)
            {
                AlmanacTransform.localScale =
                    new Vector3(0.6f, 0.6f, 0.6f);
            }
        }
        else
        {
            if (AlmanacTransform != null)
            {
                AlmanacTransform.localScale =
                    Vector3.zero;
            }
        }

        if (!PlayAnim)
            return;

        if (BackLeft != null)
            BackLeft.Play("", 0, 0f);

        if (BackCenter != null)
            BackCenter.Play("", 0, 0f);

        if (BackRight != null)
            BackRight.Play("", 0, 0f);

        if (changeUser != null)
            changeUser.PlayAnimation();
    }

    public void GoEndLess()
    {
        if (AudioManager.Instance != null &&
            GameManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.ButtonClick,
                transform.position,
                isAll: true
            );
        }

        Invoke(nameof(DoGoEndLess), 0.5f);
    }

    private void DoGoEndLess()
    {
        Debug.Log("Go Endless");
    }
}