using System.Collections.Generic;
using SocketSave;
using UnityEngine;

public class PlayerList : MonoBehaviour
{
    public static PlayerList Instance;

    public GameObject QuitButton;
    public GameObject PasswordButton;

    public TextMesh HostName;
    public TextMesh Name1;
    public TextMesh Name2;
    public TextMesh Name3;
    public TextMesh Tip;

    public int PlayerNum;

    private void Awake()
    {
        Instance = this;
        ClearPlayerList();
    }

    private void OnEnable()
    {
        RefreshPlayerList();
    }

    private void ClearPlayerList()
    {
        PlayerNum = 0;

        if (HostName != null)
            HostName.text = "";

        if (Name1 != null)
            Name1.text = "";

        if (Name2 != null)
            Name2.text = "";

        if (Name3 != null)
            Name3.text = "";

        if (QuitButton != null)
            QuitButton.SetActive(false);

        if (PasswordButton != null)
            PasswordButton.SetActive(false);

        if (Tip != null)
            Tip.text = "多人游戏未开启";
    }

    public void RefreshPlayerList()
    {
        if (OnlineNetworkServer.Instance == null ||
            !OnlineNetworkServer.Instance.isServerOpen)
        {
            ClearPlayerList();
            return;
        }

        PlayerInfo host = OnlineNetworkServer.Instance.GetHostPlayer();
        List<PlayerInfo> players = OnlineNetworkServer.Instance.GetPlayers();

        UpdatePlayerList(host, players);
    }

    public void UpdatePlayerList(
        PlayerInfo Host,
        List<PlayerInfo> players)
    {
        PlayerNum = 1;

        if (Host == null)
            PlayerNum = 0;

        if (players != null)
            PlayerNum += players.Count;

        if (Tip != null)
        {
            if (Host != null &&
                GameManager.Instance != null &&
                GameManager.Instance.LocalPlayerSave != null &&
                Host.Name == GameManager.Instance.LocalPlayerSave.playerName)
            {
                Tip.text = "多人游戏已开启";
            }
            else if (Host != null)
            {
                Tip.text = "已加入多人游戏";
            }
            else
            {
                Tip.text = "多人游戏未开启";
            }
        }

        if (GameManager.Instance != null &&
            GameManager.Instance.isOnline)
        {
            if (QuitButton != null)
                QuitButton.SetActive(true);
        }
        else
        {
            if (QuitButton != null)
                QuitButton.SetActive(false);

            if (PasswordButton != null)
                PasswordButton.SetActive(false);

            if (Tip != null)
                Tip.text = "多人游戏未开启";
        }

        if (GameManager.Instance != null &&
            GameManager.Instance.isServer)
        {
            if (PasswordButton != null)
                PasswordButton.SetActive(true);
        }

        if (Host != null &&
            GameManager.Instance != null)
        {
            GameManager.Instance.HostName = Host.Name;
        }

        if (HostName != null)
        {
            HostName.text = Host != null
                ? Host.Name
                : "";
        }

        if (Name1 != null)
            Name1.text = "";

        if (Name2 != null)
            Name2.text = "";

        if (Name3 != null)
            Name3.text = "";

        if (players == null)
            return;

        if (players.Count > 0 && players[0] != null)
        {
            if (Name1 != null)
                Name1.text = players[0].Name;
        }

        if (players.Count > 1 && players[1] != null)
        {
            if (Name2 != null)
                Name2.text = players[1].Name;
        }

        if (players.Count > 2 && players[2] != null)
        {
            if (Name3 != null)
                Name3.text = players[2].Name;
        }
    }
}