using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ReConnect : MonoBehaviour
{
    public static ReConnect Instance;

    public Text BtnText;
    public Text NameText;
    public Text AnimText;

    public bool IsOpening
    {
        get
        {
            return transform.localScale.x > 0f &&
                   transform.localScale.y > 0f;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    public void OpenInit(bool needCnt)
    {
        NameText.text = "";

        Time.timeScale = 0f;

        AnimText.gameObject.SetActive(true);

        if (needCnt)
        {
            AnimText.gameObject.SetActive(false);
        }

        if (!IsOpening)
        {
            SeedBank.Instance.AllCancelPlace(NoSelect: true);
            transform.localScale = Vector3.one;
        }
    }

    public void OverClose()
    {
        Time.timeScale = 1f;
        transform.localScale = Vector3.zero;
    }

    public void LoadPlayerList(List<string> names)
    {
        NameText.text = "";

        AnimText.gameObject.SetActive(true);

        for (int i = 0; i < names.Count; i++)
        {
            NameText.text += names[i] + "\n";
        }
    }

    public void Reconnect()
    {
        UIManager.Instance.ReJoinGame();
    }

    public void ReConnectBtn()
    {
        Reconnect();
    }

    public void GiveUpBtn()
    {
        if (GameManager.Instance.isServer)
        {
            SocketServer.Instance.GiveUpReConnect();
            return;
        }

        OverClose();

        LVManager.Instance.QuitBattleGame();

        SocketClient.Instance.ReConnectGiveUp();
    }
}