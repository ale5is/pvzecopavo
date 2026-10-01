using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class GuestPlayerManager : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        [Tooltip("Nombre que se muestra sobre el cursor.")]
        public string playerName = "Jugador";

        [Tooltip("Dispositivo que controla este jugador. None = jugador desactivado.")]
        public GuestDevice device = GuestDevice.None;

        [Tooltip("Color del jugador.")]
        public Color color = Color.white;

        [Tooltip("GameObject usado como cursor visual.")]
        public GameObject cursorVisual;

        [Tooltip("TextMesh normal usado como plantilla para el texto del cursor.")]
        public TextMesh cursorText;
    }

    public static GuestPlayerManager Instance;

    [Header("Jugadores invitados (2 a 4)")]

    public List<Slot> slots = new List<Slot>
    {
        new Slot
        {
            playerName = "Jugador 2",
            device = GuestDevice.KeyboardWASD,
            color = new Color(0.30f, 0.65f, 1.00f)
        },

        new Slot
        {
            playerName = "Jugador 3",
            device = GuestDevice.KeyboardArrows,
            color = new Color(1.00f, 0.55f, 0.20f)
        },

        new Slot
        {
            playerName = "Jugador 4",
            device = GuestDevice.Gamepad1,
            color = new Color(0.75f, 0.40f, 1.00f)
        }
    };

    [Header("Cursor")]

    [Tooltip("GameObject usado como cursor cuando el jugador no tiene uno propio.")]
    public GameObject defaultCursorVisual;

    [Tooltip("TextMesh usado como texto cuando el jugador no tiene uno propio.")]
    public TextMesh defaultCursorText;

    [Tooltip("Tamaño del cursor respecto a la casilla.")]
    [Range(0.2f, 1.5f)]
    public float cursorSizeFactor = 0.9f;

    [Tooltip("Corrimiento del cursor respecto al centro de la casilla.")]
    public Vector2 cursorOffset = Vector2.zero;

    [Tooltip("Colorea el TextMesh con el color del jugador.")]
    public bool tintTextWithPlayerColor = true;

    [Tooltip("Muestra el nombre del jugador sobre el cursor.")]
    public bool showPlayerName = true;

    [Header("General")]

    [Tooltip("Mantener este objeto al cambiar de escena.")]
    public bool dontDestroyOnLoad = true;

    public readonly List<GuestPlayer> Players =
        new List<GuestPlayer>();

    public event Action<GuestPlayer, PlantCard, Grid> ConfirmRequested;

    private readonly List<PlantCard> cardBuffer =
        new List<PlantCard>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[Guest] Ya hay un GuestPlayerManager en la escena; se elimina este duplicado."
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad && transform.parent == null)
            DontDestroyOnLoad(gameObject);

        BuildPlayers();
    }

    [ContextMenu("Reconstruir jugadores")]
    public void BuildPlayers()
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i] != null)
                Players[i].Destroy();
        }

        Players.Clear();

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];

            if (slot == null)
                continue;

            GuestInput input =
                GuestInput.Create(slot.device);

            if (input == null)
                continue;

            GameObject cursorVisual =
                slot.cursorVisual != null
                    ? slot.cursorVisual
                    : defaultCursorVisual;

            TextMesh cursorText =
                slot.cursorText != null
                    ? slot.cursorText
                    : defaultCursorText;

            if (cursorVisual == null)
            {
                Debug.LogWarning(
                    "[Guest] " +
                    slot.playerName +
                    " no tiene GameObject de cursor asignado."
                );
            }

            GuestPlayer player =
                new GuestPlayer(
                    slot.playerName,
                    slot.color,
                    input,
                    Players.Count + 1,
                    cursorVisual,
                    cursorText,
                    cursorSizeFactor,
                    cursorOffset,
                    tintTextWithPlayerColor,
                    showPlayerName
                );

            player.ConfirmRequested +=
                OnPlayerConfirm;

            Players.Add(player);
        }
    }

    private void OnPlayerConfirm(
        GuestPlayer player,
        PlantCard card,
        Grid grid)
    {
        if (player == null ||
            card == null ||
            grid == null)
        {
            return;
        }

        ConfirmRequested?.Invoke(
            player,
            card,
            grid
        );

        PlaceGuestPlant(
            player,
            card,
            grid
        );
    }

    private void PlaceGuestPlant(
        GuestPlayer player,
        PlantCard card,
        Grid grid)
    {
        if (player == null ||
            card == null ||
            grid == null)
        {
            return;
        }

        if (SeedBank.Instance == null)
        {
            Debug.LogWarning(
                "[Guest] No existe SeedBank.Instance."
            );

            return;
        }

        if (PlantManager.Instance == null)
        {
            Debug.LogWarning(
                "[Guest] No existe PlantManager.Instance."
            );

            return;
        }

        PlantType plantType =
            card.CardPlantType;

        if (plantType == PlantType.Nope)
        {
            Debug.LogWarning(
                "[Guest] La carta seleccionada no contiene una planta."
            );

            return;
        }

        PlantBase plant =
            PlantManager.Instance.GetNewPlant(
                plantType
            );

        if (plant == null)
        {
            Debug.LogWarning(
                "[Guest] PlantManager no pudo crear " +
                plantType +
                "."
            );

            return;
        }

        bool valid =
            SeedBank.Instance.CheckPlant(
                plant,
                grid,
                -1,
                player.Name
            );

        if (!valid)
        {
            Destroy(plant.gameObject);
            return;
        }

        int spCode =
            card.isImitater ? 2 : 0;

        SeedBank.Instance.PlantConfirm(
            plant,
            grid,
            -1,
            spCode,
            player.Name
        );

        player.ClearSelectedCard();
    }

    private bool ShouldRun()
    {
        if (LVManager.Instance == null ||
            !LVManager.Instance.GameIsStart)
        {
            return false;
        }

        if (MapManager.Instance == null ||
            MapManager.Instance.mapList == null ||
            MapManager.Instance.mapList.Count == 0)
        {
            return false;
        }

        if (SeedBank.Instance == null ||
            LV.Instance == null)
        {
            return false;
        }

        return LV.Instance.CurrLVType != LVType.IZombie &&
               LV.Instance.CurrLVType != LVType.PvP;
    }

    private static bool TypingInInputField()
    {
        EventSystem es =
            EventSystem.current;

        if (es == null ||
            es.currentSelectedGameObject == null)
        {
            return false;
        }

        GameObject selected =
            es.currentSelectedGameObject;

        return
            selected.GetComponent<UnityEngine.UI.InputField>() != null ||
            selected.GetComponent("TMP_InputField") != null;
    }

    private void Update()
    {
        if (!ShouldRun())
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i] != null)
                    Players[i].Clear();
            }

            return;
        }

        if (TypingInInputField())
            return;

        MapBase map =
            MapManager.Instance.mapList[0];

        if (map == null ||
            map.GridList == null ||
            map.GridList.Count == 0)
        {
            return;
        }

        List<Grid> grids =
            map.GridList;

        cardBuffer.Clear();

        IReadOnlyList<PlantCard> cards =
            SeedBank.Instance.SlotCards;

        for (int i = 0; i < cards.Count; i++)
        {
            PlantCard card = cards[i];

            if (card != null &&
                card.gameObject.activeInHierarchy &&
                card.CardPlantType != PlantType.Nope)
            {
                cardBuffer.Add(card);
            }
        }

        float cell =
            GetCellSize(grids);

        for (int i = 0; i < Players.Count; i++)
        {
            GuestPlayer player =
                Players[i];

            if (player == null)
                continue;

            player.CellSize =
                cell;

            player.CursorSizeFactor =
                cursorSizeFactor;

            player.CursorOffset =
                cursorOffset;

            if (!player.Input.IsAvailable)
            {
                player.Clear();
                continue;
            }

            player.Tick(
                grids,
                cardBuffer
            );
        }
    }

    private List<Grid> cellGridsRef;
    private int cellGridsCount;
    private float cellSize = 1.3f;

    private float GetCellSize(
        List<Grid> grids)
    {
        if (grids == cellGridsRef &&
            grids.Count == cellGridsCount)
        {
            return cellSize;
        }

        cellGridsRef = grids;
        cellGridsCount = grids.Count;

        float best =
            float.MaxValue;

        for (int i = 0; i < grids.Count; i++)
        {
            if (grids[i] == null)
                continue;

            for (int j = i + 1; j < grids.Count; j++)
            {
                if (grids[j] == null)
                    continue;

                Vector2 delta =
                    grids[j].Position -
                    grids[i].Position;

                if (Mathf.Abs(delta.y) < 0.05f &&
                    Mathf.Abs(delta.x) > 0.1f)
                {
                    best =
                        Mathf.Min(
                            best,
                            Mathf.Abs(delta.x)
                        );
                }
            }
        }

        if (best < float.MaxValue)
            cellSize = best;

        return cellSize;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i] != null)
                Players[i].Destroy();
        }

        Players.Clear();

        if (Instance == this)
            Instance = null;
    }
}