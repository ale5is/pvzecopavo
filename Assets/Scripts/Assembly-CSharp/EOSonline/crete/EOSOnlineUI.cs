using System.Collections.Generic;
using Epic.OnlineServices;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class EOSOnlineUI : MonoBehaviour
{
    public static EOSOnlineUI Instance;

    [Header("Panel")]
    [SerializeField] private GameObject onlineRoomsPanel;
    [SerializeField] private TMP_Text roomsStatusText;

    [Header("Textos de las salas")]
    [SerializeField] private TMP_Text[] roomNameTexts;
    [SerializeField] private TMP_Text[] roomPlayersTexts;
    [SerializeField] private TMP_Text[] roomHostTexts;

    private bool operationInProgress;
    private bool statusLocked;
    private int selectedRoomIndex = -1;

    private readonly List<EOSOnlineRoom> displayedRooms = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        ConfigureRoomClickTargets();
        HideRoomTexts();

        if (onlineRoomsPanel != null)
            onlineRoomsPanel.SetActive(false);
    }

    private void Update()
    {
        if (EOSOnlineSession.Instance == null ||
            EOSOnlineSession.Instance.IsSearching)
            return;

        RefreshRooms();
    }

    private bool IsBusy()
    {
        return operationInProgress ||
               (EOSOnlineSession.Instance != null &&
                EOSOnlineSession.Instance.IsOperationInProgress) ||
               (EOSAutoLogin.Instance != null &&
                EOSAutoLogin.Instance.IsLoginInProgress);
    }

    public void CreateOnlineGame()
    {
        if (IsBusy())
            return;

        if (EOSAutoLogin.Instance == null)
        {
            SetSessionStatus("EOSAutoLogin no existe.");
            return;
        }

        operationInProgress = true;
        SetSessionStatus("Creando partida...");

        EOSAutoLogin.Instance.Login(OnCreateLoginFinished);
    }

    private void OnCreateLoginFinished(bool success)
    {
        operationInProgress = false;

        if (!success)
        {
            SetSessionStatus("No se pudo iniciar sesión.");
            return;
        }

        if (EOSOnlineSession.Instance == null)
        {
            SetSessionStatus("EOSOnlineSession no existe.");
            return;
        }

        EOSOnlineSession.Instance.CreateOnlineGame();
    }

    public void SearchOnlineGames()
    {
        if (IsBusy())
            return;

        if (EOSAutoLogin.Instance == null)
        {
            SetStatus("EOSAutoLogin no existe.");
            return;
        }

        operationInProgress = true;
        selectedRoomIndex = -1;
        displayedRooms.Clear();

        ShowOnlineRooms();
        ClearRoomTexts();
        SetStatus("Buscando partidas...");

        EOSAutoLogin.Instance.Login(OnSearchLoginFinished);
    }

    private void OnSearchLoginFinished(bool success)
    {
        operationInProgress = false;

        if (!success)
        {
            SetStatus("No se pudo iniciar sesión.");
            return;
        }

        if (EOSOnlineSession.Instance == null)
        {
            SetStatus("EOSOnlineSession no existe.");
            return;
        }

        EOSOnlineSession.Instance.SearchOnlineGames();
    }

    public void JoinOnlineGame()
    {
        if (IsBusy())
            return;

        if (selectedRoomIndex < 0)
        {
            SetStatus("Seleccioná una partida primero.");
            return;
        }

        if (selectedRoomIndex >= displayedRooms.Count)
        {
            SetStatus("La partida seleccionada ya no está disponible.");
            selectedRoomIndex = -1;
            return;
        }

        if (EOSAutoLogin.Instance == null)
        {
            SetStatus("EOSAutoLogin no existe.");
            return;
        }

        operationInProgress = true;
        SetSessionStatus("Uniéndose a la partida...");

        EOSAutoLogin.Instance.Login(OnJoinLoginFinished);
    }

    private void OnJoinLoginFinished(bool success)
    {
        operationInProgress = false;

        if (!success)
        {
            SetSessionStatus("No se pudo iniciar sesión.");
            return;
        }

        if (EOSOnlineSession.Instance == null)
        {
            SetSessionStatus("EOSOnlineSession no existe.");
            return;
        }

        EOSOnlineRoom room = GetSelectedRoom();

        if (room == null)
        {
            SetSessionStatus("La partida seleccionada ya no está disponible.");
            selectedRoomIndex = -1;
            return;
        }

        if (!EOSOnlineSession.Instance.SelectRoom(room))
        {
            SetSessionStatus("No se pudo seleccionar la partida.");
            return;
        }

        EOSOnlineSession.Instance.JoinSelectedOnlineGame();
    }

    public void LeaveOnlineGame()
    {
        operationInProgress = false;
        selectedRoomIndex = -1;
        displayedRooms.Clear();

        if (EOSOnlineSession.Instance != null)
            EOSOnlineSession.Instance.LeaveOnlineGame();

        ClearRoomTexts();
        HideOnlineRooms();
    }

    public void ShowOnlineRooms()
    {
        if (onlineRoomsPanel != null)
            onlineRoomsPanel.SetActive(true);
    }

    public void HideOnlineRooms()
    {
        if (onlineRoomsPanel != null)
            onlineRoomsPanel.SetActive(false);
    }

    public void SetSessionStatus(string message)
    {
        statusLocked = true;
        SetStatus(message);
    }

    private void RefreshRooms()
    {
        IReadOnlyList<EOSOnlineRoom> rooms =
            EOSOnlineSession.Instance.AvailableRooms;

        if (rooms == null)
            return;

        int textCount = GetTextCount();
        int roomCount = Mathf.Min(rooms.Count, textCount);

        displayedRooms.Clear();

        for (int i = 0; i < roomCount; i++)
        {
            if (rooms[i] != null)
                displayedRooms.Add(rooms[i]);
        }

        if (selectedRoomIndex >= displayedRooms.Count)
            selectedRoomIndex = -1;

        for (int i = 0; i < textCount; i++)
        {
            if (i < displayedRooms.Count)
                ShowRoom(i, displayedRooms[i]);
            else
                HideRoom(i);
        }

        if (!statusLocked)
        {
            if (displayedRooms.Count == 0)
                SetStatus("No hay partidas disponibles.");
            else if (selectedRoomIndex >= 0)
                SetStatus("Partida seleccionada. Presioná Unirse.");
            else
                SetStatus(displayedRooms.Count + " partida(s) disponible(s).");
        }

        if (rooms.Count > textCount)
        {
            Debug.LogWarning(
                "[EOS UI] Hay " + rooms.Count +
                " partidas, pero solo hay " + textCount +
                " grupos de textos configurados."
            );
        }
    }

    private void ShowRoom(int index, EOSOnlineRoom room)
    {
        if (room == null)
        {
            HideRoom(index);
            return;
        }

        bool selected = index == selectedRoomIndex;

        if (HasText(roomNameTexts, index))
            roomNameTexts[index].text =
                selected
                    ? "▶ " + room.RoomName + "  [SELECCIONADA]"
                    : room.RoomName;

        if (HasText(roomPlayersTexts, index))
            roomPlayersTexts[index].text =
                room.CurrentPlayers + " / " +
                room.MaxPlayers + " jugadores";

        if (HasText(roomHostTexts, index))
            roomHostTexts[index].text =
                "Host: " + ShortId(room.HostUserId);

        SetRoomTextsActive(index, true);
    }

    private void HideRoom(int index)
    {
        if (HasText(roomNameTexts, index))
            roomNameTexts[index].text = "";

        if (HasText(roomPlayersTexts, index))
            roomPlayersTexts[index].text = "";

        if (HasText(roomHostTexts, index))
            roomHostTexts[index].text = "";

        SetRoomTextsActive(index, false);
    }

    private void SetRoomTextsActive(int index, bool active)
    {
        if (HasText(roomNameTexts, index))
            roomNameTexts[index].gameObject.SetActive(active);

        if (HasText(roomPlayersTexts, index))
            roomPlayersTexts[index].gameObject.SetActive(active);

        if (HasText(roomHostTexts, index))
            roomHostTexts[index].gameObject.SetActive(active);
    }

    private bool HasText(TMP_Text[] texts, int index)
    {
        return texts != null &&
               index >= 0 &&
               index < texts.Length &&
               texts[index] != null;
    }

    private void ConfigureRoomClickTargets()
    {
        if (roomNameTexts == null)
            return;

        for (int i = 0; i < roomNameTexts.Length; i++)
        {
            if (roomNameTexts[i] == null)
                continue;

            GameObject target =
                roomNameTexts[i].transform.parent != null
                    ? roomNameTexts[i].transform.parent.gameObject
                    : roomNameTexts[i].gameObject;

            EventTrigger trigger =
                target.GetComponent<EventTrigger>() ??
                target.AddComponent<EventTrigger>();

            trigger.triggers.Clear();

            int index = i;
            EventTrigger.Entry entry = new()
            {
                eventID = EventTriggerType.PointerClick
            };

            entry.callback.AddListener(
                _ => SelectRoomByIndex(index)
            );

            trigger.triggers.Add(entry);
        }
    }

    private void SelectRoomByIndex(int index)
    {
        if (index < 0 || index >= displayedRooms.Count)
            return;

        EOSOnlineRoom room = displayedRooms[index];

        if (room == null ||
            EOSOnlineSession.Instance == null)
            return;

        if (!EOSOnlineSession.Instance.SelectRoom(room))
        {
            SetStatus("No se pudo seleccionar la partida.");
            return;
        }

        statusLocked = false;
        selectedRoomIndex = index;

        RefreshRooms();

        SetStatus("Partida seleccionada. Presioná Unirse.");

        Debug.Log("[EOS UI] Sala seleccionada: " + room.RoomName);
    }

    private EOSOnlineRoom GetSelectedRoom()
    {
        if (selectedRoomIndex < 0 ||
            selectedRoomIndex >= displayedRooms.Count)
            return null;

        return displayedRooms[selectedRoomIndex];
    }

    private void HideRoomTexts()
    {
        for (int i = 0; i < GetTextCount(); i++)
            HideRoom(i);
    }

    private void ClearRoomTexts()
    {
        for (int i = 0; i < GetTextCount(); i++)
            HideRoom(i);

        selectedRoomIndex = -1;
        displayedRooms.Clear();
        statusLocked = false;
        SetStatus("");
    }

    private int GetTextCount()
    {
        int count = 0;

        if (roomNameTexts != null)
            count = Mathf.Max(count, roomNameTexts.Length);

        if (roomPlayersTexts != null)
            count = Mathf.Max(count, roomPlayersTexts.Length);

        if (roomHostTexts != null)
            count = Mathf.Max(count, roomHostTexts.Length);

        return count;
    }

    private void SetStatus(string message)
    {
        if (roomsStatusText != null)
            roomsStatusText.text = message;
    }

    private string ShortId(ProductUserId userId)
    {
        if (userId == null)
            return "Desconocido";

        string id = userId.ToString();

        return id.Length <= 12
            ? id
            : id.Substring(0, 12) + "...";
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}