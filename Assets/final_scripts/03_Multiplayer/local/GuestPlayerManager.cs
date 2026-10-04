using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

[DisallowMultipleComponent]
public class GuestPlayerManager : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        [Tooltip("Nombre que se muestra sobre el cursor.")]
        public string playerName = "Jugador";

        [HideInInspector]
        public GuestDevice device = GuestDevice.None;

        [Header("Controles")]
        public GuestControlProfile controls =
            new GuestControlProfile();

        [Header("Visual")]
        public Color color = Color.white;

        public GameObject cursorVisual;

        public TextMesh cursorText;
    }

    public static GuestPlayerManager Instance;

    [Header("Jugadores locales 1 a 4")]
    public List<Slot> slots = new List<Slot>
    {
        CreatePlayer1(),
        CreatePlayer2(),
        CreatePlayer3(),
        CreatePlayer4()
    };

    [Header("Cursor")]
    public GameObject defaultCursorVisual;

    public TextMesh defaultCursorText;

    [Range(0.2f, 1.5f)]
    public float cursorSizeFactor = 0.9f;

    public Vector2 cursorOffset =
        Vector2.zero;

    public bool tintTextWithPlayerColor =
        true;

    public bool showPlayerName =
        true;

    [Header("Recoleccion")]
    public bool guestCanCollect =
        true;

    [Range(0.05f, 1.5f)]
    public float collectRadius =
        0.55f;

    public bool includeTriggerColliders =
        true;

    public enum AutoJoinInput
    {
        Gamepad,
        WASD,
        Arrows
    }

    [Serializable]
    public class AutoJoinBinding
    {
        public AutoJoinInput input = AutoJoinInput.Gamepad;
        public KeyCode activateKey = KeyCode.None;
        public KeyCode deactivateKey = KeyCode.None;
        public bool enabled = true;
    }

    [Header("Asignacion automatica de Guest")]
    [Tooltip("Todos los Guest empiezan desactivados. El primer input que pulse su boton de activar toma el siguiente slot libre de Jugador 2 a 4.")]
    public bool autoAssignGuests = true;

    public AutoJoinBinding gamepadBinding = new AutoJoinBinding
    {
        input = AutoJoinInput.Gamepad,
        activateKey = KeyCode.None,
        deactivateKey = KeyCode.None,
        enabled = true
    };

    public AutoJoinBinding wasdBinding = new AutoJoinBinding
    {
        input = AutoJoinInput.WASD,
        activateKey = KeyCode.Z,
        deactivateKey = KeyCode.X,
        enabled = true
    };

    public AutoJoinBinding arrowsBinding = new AutoJoinBinding
    {
        input = AutoJoinInput.Arrows,
        activateKey = KeyCode.Return,
        deactivateKey = KeyCode.Backspace,
        enabled = true
    };

    private int assignedWasdSlot = -1;
    private int assignedArrowsSlot = -1;
    private readonly Dictionary<string, int> assignedGamepadSlots =
        new Dictionary<string, int>();

    [SerializeField]
    private bool autoJoinDefaultsApplied;

    [Header("General")]
    public bool dontDestroyOnLoad =
        true;

    public readonly List<GuestPlayer> Players =
        new List<GuestPlayer>();

    private readonly Dictionary<int, GuestPlayer> playersBySlot =
        new Dictionary<int, GuestPlayer>();

    private readonly Dictionary<int, GuestControlProfile> playerProfilesBySlot =
        new Dictionary<int, GuestControlProfile>();

    public event Action<
        GuestPlayer,
        PlantCard,
        Grid
    > ConfirmRequested;

    public static bool AllowPlayer1MouseInput
    {
        get
        {
            if (Instance == null)
                return true;

            return Instance.IsPlayer1MouseMode();
        }
    }

    private readonly List<PlantCard> cardBuffer =
        new List<PlantCard>();

    // Cartas que vienen de la cinta transportadora (niveles especiales).
    private readonly HashSet<PlantCard> beltCardSet =
        new HashSet<PlantCard>();

    [Header("Conveyor belt")]
    [SerializeField]
    private bool guestsUseConveyorBelt = true;

    // Una carta recien aparecida esta fuera de la mascara de la cinta (x local ~2.8);
    // solo se puede elegir cuando ya entro en la cinta.
    [SerializeField]
    private float beltMaxSelectableLocalX = 2.4f;

    private readonly Collider[] collectColliderBuffer =
        new Collider[64];

    private List<Grid> cellGridsRef;

    private int cellGridsCount;

    private float cellSize = 1.3f;

    private static Slot CreatePlayer1()
    {
        Slot slot =
            new Slot();

        slot.playerName =
            "Jugador 1";

        slot.device =
            GuestDevice.None;

        slot.controls.enabled =
            true;

        slot.controls.SetMouse();

        slot.color =
            Color.white;

        return slot;
    }

    private static Slot CreatePlayer2()
    {
        Slot slot =
            new Slot();

        slot.playerName =
            "Jugador 2";

        slot.device =
            GuestDevice.None;

        slot.controls.enabled =
            false;

        slot.controls.SetWASD();

        slot.color =
            new Color(
                0.30f,
                0.65f,
                1.00f
            );

        return slot;
    }

    private static Slot CreatePlayer3()
    {
        Slot slot =
            new Slot();

        slot.playerName =
            "Jugador 3";

        slot.device =
            GuestDevice.None;

        slot.controls.enabled =
            false;

        slot.controls.SetArrows();

        slot.color =
            new Color(
                1.00f,
                0.55f,
                0.20f
            );

        return slot;
    }

    private static Slot CreatePlayer4()
    {
        Slot slot =
            new Slot();

        slot.playerName =
            "Jugador 4";

        slot.device =
            GuestDevice.None;

        slot.controls.enabled =
            false;

        slot.controls.SetGamepad(1);

        slot.color =
            new Color(
                0.75f,
                0.40f,
                1.00f
            );

        return slot;
    }

    private void Awake()
    {
        if (
            Instance != null &&
            Instance != this
        )
        {
            Debug.LogWarning(
                "[Guest] Ya hay un GuestPlayerManager en la escena; se elimina este duplicado."
            );

            Destroy(gameObject);

            return;
        }

        Instance = this;

        EnsureFourSlots();

        if (!autoJoinDefaultsApplied)
        {
            slots[1].controls.enabled = false;
            slots[1].controls.mode = GuestControlMode.Disabled;
            slots[2].controls.enabled = false;
            slots[2].controls.mode = GuestControlMode.Disabled;
            slots[3].controls.enabled = false;
            slots[3].controls.mode = GuestControlMode.Disabled;
            slots[1].device = GuestDevice.None;
            slots[2].device = GuestDevice.None;
            slots[3].device = GuestDevice.None;
            autoJoinDefaultsApplied = true;
        }

        if (
            dontDestroyOnLoad &&
            transform.parent == null
        )
        {
            DontDestroyOnLoad(
                gameObject
            );
        }

        BuildPlayers();
    }

    private void EnsureFourSlots()
    {
        if (slots == null)
        {
            slots =
                new List<Slot>();
        }

        if (
            slots.Count == 3 &&
            slots[0] != null &&
            slots[0].playerName ==
                "Jugador 2"
        )
        {
            slots.Insert(
                0,
                CreatePlayer1()
            );
        }

        while (slots.Count < 4)
        {
            if (slots.Count == 0)
            {
                slots.Add(
                    CreatePlayer1()
                );
            }
            else if (slots.Count == 1)
            {
                slots.Add(
                    CreatePlayer2()
                );
            }
            else if (slots.Count == 2)
            {
                slots.Add(
                    CreatePlayer3()
                );
            }
            else
            {
                slots.Add(
                    CreatePlayer4()
                );
            }
        }

        if (slots.Count > 4)
        {
            slots.RemoveRange(
                4,
                slots.Count - 4
            );
        }
    }

    [ContextMenu(
        "Restaurar controles predeterminados"
    )]
    public void ResetControlsToDefaults()
    {
        EnsureFourSlots();

        slots[0].controls.enabled =
            true;

        slots[0].controls.SetMouse();

        slots[1].controls.enabled =
            false;

        slots[1].controls.SetWASD();

        slots[2].controls.enabled =
            false;

        slots[2].controls.SetArrows();

        slots[3].controls.enabled =
            false;

        slots[3].controls.SetGamepad(1);

        assignedWasdSlot = -1;
        assignedArrowsSlot = -1;
        assignedGamepadSlots.Clear();

        BuildPlayers();
    }

    public void SetPlayerKeyboard(
        int playerNumber)
    {
        Slot slot =
            GetSlot(playerNumber);

        if (slot == null)
            return;

        slot.controls.enabled =
            true;

        slot.controls.mode =
            GuestControlMode.Keyboard;

        BuildPlayers();
    }

    public void SetPlayerGamepad(
        int playerNumber,
        int gamepadNumber)
    {
        Slot slot =
            GetSlot(playerNumber);

        if (slot == null)
            return;

        slot.controls.enabled =
            true;

        slot.controls.SetGamepad(
            gamepadNumber
        );

        BuildPlayers();
    }

    public void SetPlayerMouse(
        int playerNumber)
    {
        Slot slot =
            GetSlot(playerNumber);

        if (slot == null)
            return;

        slot.controls.enabled =
            true;

        slot.controls.SetMouse();

        BuildPlayers();
    }

    public void DisablePlayer(
        int playerNumber)
    {
        Slot slot =
            GetSlot(playerNumber);

        if (slot == null)
            return;

        slot.controls.enabled =
            false;
        slot.controls.mode = GuestControlMode.Disabled;
        slot.controls.gamepadId = "";
        slot.device = GuestDevice.None;

        if (assignedWasdSlot == playerNumber - 1)
            assignedWasdSlot = -1;

        if (assignedArrowsSlot == playerNumber - 1)
            assignedArrowsSlot = -1;

        List<string> gamepadsToRemove = null;
        foreach (KeyValuePair<string, int> pair in assignedGamepadSlots)
        {
            if (pair.Value == playerNumber - 1)
            {
                if (gamepadsToRemove == null)
                    gamepadsToRemove = new List<string>();

                gamepadsToRemove.Add(pair.Key);
            }
        }

        if (gamepadsToRemove != null)
        {
            for (int i = 0; i < gamepadsToRemove.Count; i++)
                assignedGamepadSlots.Remove(gamepadsToRemove[i]);
        }

        BuildPlayers();
    }

    public void ApplyKeyboardPreset(
        int playerNumber,
        int preset)
    {
        Slot slot =
            GetSlot(playerNumber);

        if (slot == null)
            return;

        slot.controls.enabled =
            true;

        if (preset == 0)
        {
            slot.controls.SetWASD();
        }
        else if (preset == 1)
        {
            slot.controls.SetArrows();
        }
        else if (preset == 2)
        {
            slot.controls.SetIJKL();
        }
        else
        {
            slot.controls.SetWASD();
        }

        BuildPlayers();
    }

    private Slot GetSlot(
        int playerNumber)
    {
        int index =
            playerNumber - 1;

        if (
            index < 0 ||
            index >= slots.Count
        )
        {
            return null;
        }

        return slots[index];
    }

    private bool IsPlayer1MouseMode()
    {
        if (
            slots == null ||
            slots.Count == 0 ||
            slots[0] == null
        )
        {
            return true;
        }

        GuestControlProfile profile =
            ResolveControls(
                slots[0]
            );

        if (profile == null)
            return false;

        return profile.mode ==
            GuestControlMode.Mouse;
    }

    private GuestControlProfile ResolveControls(
        Slot slot)
    {
        if (slot == null)
            return null;

        if (
            slot.controls != null &&
            !slot.controls.enabled
        )
        {
            return null;
        }

        if (
            slot.controls != null &&
            slot.controls.enabled &&
            slot.controls.mode !=
                GuestControlMode.Disabled
        )
        {
            return slot.controls;
        }

        if (
            slot.device !=
            GuestDevice.None
        )
        {
            GuestControlProfile legacy =
                new GuestControlProfile();

            legacy.enabled =
                true;

            if (
                slot.device ==
                GuestDevice.KeyboardWASD
            )
            {
                legacy.SetWASD();
            }
            else if (
                slot.device ==
                GuestDevice.KeyboardArrows
            )
            {
                legacy.SetArrows();
            }
            else if (
                slot.device ==
                GuestDevice.KeyboardIJKL
            )
            {
                legacy.SetIJKL();
            }
            else if (
                slot.device ==
                GuestDevice.Gamepad1
            )
            {
                legacy.SetGamepad(1);
            }
            else if (
                slot.device ==
                GuestDevice.Gamepad2
            )
            {
                legacy.SetGamepad(2);
            }
            else if (
                slot.device ==
                GuestDevice.Gamepad3
            )
            {
                legacy.SetGamepad(3);
            }
            else if (
                slot.device ==
                GuestDevice.Gamepad4
            )
            {
                legacy.SetGamepad(4);
            }
            else
            {
                return null;
            }

            return legacy;
        }

        return null;
    }

    [ContextMenu("Reconstruir jugadores")]
    public void BuildPlayers()
    {
        EnsureFourSlots();

        List<GuestPlayer> rebuiltPlayers =
            new List<GuestPlayer>();

        for (int i = 0; i < 4; i++)
        {
            Slot slot = slots[i];

            if (slot == null)
            {
                RemovePlayerFromSlot(i);
                continue;
            }

            GuestControlProfile profile =
                ResolveControls(slot);

            bool shouldBeActive =
                profile != null &&
                profile.enabled &&
                profile.mode != GuestControlMode.Mouse &&
                profile.mode != GuestControlMode.Disabled;

            if (!shouldBeActive)
            {
                RemovePlayerFromSlot(i);
                continue;
            }

            GuestPlayer existingPlayer;

            if (
                playersBySlot.TryGetValue(i, out existingPlayer) &&
                existingPlayer != null &&
                playerProfilesBySlot.TryGetValue(i, out GuestControlProfile previousProfile) &&
                ProfilesMatch(previousProfile, profile)
            )
            {
                rebuiltPlayers.Add(existingPlayer);
                continue;
            }

            RemovePlayerFromSlot(i);

            GuestInput input =
                GuestInput.Create(profile);

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

            GuestPlayer player =
                new GuestPlayer(
                    slot.playerName,
                    slot.color,
                    input,
                    i + 1,
                    cursorVisual,
                    cursorText,
                    cursorSizeFactor,
                    cursorOffset,
                    tintTextWithPlayerColor,
                    showPlayerName
                );

            player.ConfirmRequested +=
                OnPlayerConfirm;

            playersBySlot[i] = player;
            playerProfilesBySlot[i] = profile.Clone();
            rebuiltPlayers.Add(player);
        }

        Players.Clear();
        Players.AddRange(rebuiltPlayers);
    }

    private void RemovePlayerFromSlot(int slotIndex)
    {
        GuestPlayer player;

        if (playersBySlot.TryGetValue(slotIndex, out player))
        {
            if (player != null)
                player.Destroy();

            playersBySlot.Remove(slotIndex);
        }

        playerProfilesBySlot.Remove(slotIndex);
    }

    private static bool ProfilesMatch(
        GuestControlProfile a,
        GuestControlProfile b)
    {
        if (a == null || b == null)
            return false;

        return
            a.enabled == b.enabled &&
            a.mode == b.mode &&
            a.gamepadNumber == b.gamepadNumber &&
            a.gamepadId == b.gamepadId &&
            a.up == b.up &&
            a.down == b.down &&
            a.left == b.left &&
            a.right == b.right &&
            a.previousCard == b.previousCard &&
            a.nextCard == b.nextCard &&
            a.confirm == b.confirm &&
            a.cancel == b.cancel;
    }

    private void OnPlayerConfirm(
        GuestPlayer player,
        PlantCard card,
        Grid grid)
    {
        if (
            player == null ||
            card == null ||
            grid == null
        )
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
        if (
            player == null ||
            card == null ||
            grid == null
        )
        {
            return;
        }

        // Las cartas de la cinta no tienen recarga ni cuestan sol (NeedSun = 0).
        bool isBelt =
            beltCardSet.Contains(
                card
            );

        if (!isBelt && !card.CanPlace)
            return;

        int needSun =
            isBelt
                ? card.NeedSun
                : -1;

        if (SeedBank.Instance == null)
            return;

        if (PlantManager.Instance == null)
            return;

        PlantType plantType =
            card.CardPlantType;

        if (
            plantType ==
            PlantType.Nope
        )
        {
            return;
        }

        PlantBase plant =
            PlantManager.Instance.GetNewPlant(
                plantType
            );

        if (plant == null)
            return;

        bool valid =
            SeedBank.Instance.CheckPlant(
                plant,
                grid,
                needSun,
                player.Name
            );

        if (!valid)
        {
            Destroy(
                plant.gameObject
            );

            return;
        }

        int spCode =
            card.isImitater
                ? 2
                : 0;

        SeedBank.Instance.PlantConfirm(
            plant,
            grid,
            needSun,
            spCode,
            player.Name
        );

        if (isBelt)
        {
            // Igual que el jugador principal: la carta de la cinta se gasta al plantar.
            beltCardSet.Remove(
                card
            );

            player.ClearSelectedCard();

            SeedBank.Instance.RemoveDropCard(
                card
            );
        }
        else
        {
            card.CanPlace = false;
        }
    }

    private void UpdateAutoAssignment()
    {
        if (!autoAssignGuests)
            return;

        CleanupAssignments();

        if (wasdBinding != null && wasdBinding.enabled)
        {
            if (InputCompat.GetKeyDown(wasdBinding.deactivateKey))
            {
                DeactivateKeyboardAssignment(ref assignedWasdSlot);
            }
            else if (InputCompat.GetKeyDown(wasdBinding.activateKey))
            {
                ActivateKeyboardAssignment(
                    AutoJoinInput.WASD,
                    ref assignedWasdSlot
                );
            }
        }

        if (arrowsBinding != null && arrowsBinding.enabled)
        {
            if (InputCompat.GetKeyDown(arrowsBinding.deactivateKey))
            {
                DeactivateKeyboardAssignment(ref assignedArrowsSlot);
            }
            else if (InputCompat.GetKeyDown(arrowsBinding.activateKey))
            {
                ActivateKeyboardAssignment(
                    AutoJoinInput.Arrows,
                    ref assignedArrowsSlot
                );
            }
        }

#if ENABLE_INPUT_SYSTEM
        if (gamepadBinding != null && gamepadBinding.enabled)
        {
            IReadOnlyList<InputDevice> pads = GamepadGuestInput.Pads;

            for (int i = 0; i < pads.Count; i++)
            {
                InputDevice pad = pads[i];
                if (pad == null)
                    continue;

                string id = GamepadGuestInput.PadId(pad);

                if (IsAssignedGamepad(id))
                {
                    if (GamepadGuestInput.SelectButtonPressed(pad))
                    {
                        int slotIndex = assignedGamepadSlots[id];
                        DeactivateSlot(slotIndex);
                        assignedGamepadSlots.Remove(id);
                        return;
                    }

                    continue;
                }

                if (GamepadGuestInput.SelectButtonPressed(pad))
                    continue;

                if (GamepadGuestInput.StartButtonPressed(pad))
                {
                    int slotIndex = FindFreeGuestSlot();
                    if (slotIndex < 0)
                        continue;

                    Slot slot = slots[slotIndex];
                    slot.device = GuestDevice.None;
                    slot.controls.enabled = true;
                    slot.controls.SetGamepad(
                        id,
                        Mathf.Clamp(i + 1, 1, 4)
                    );

                    assignedGamepadSlots[id] = slotIndex;
                    BuildPlayers();
                    return;
                }
            }
        }
#endif
    }

    private void CleanupAssignments()
    {
        if (assignedWasdSlot >= 0 && !IsGuestSlotActive(assignedWasdSlot))
            assignedWasdSlot = -1;

        if (assignedArrowsSlot >= 0 && !IsGuestSlotActive(assignedArrowsSlot))
            assignedArrowsSlot = -1;

        List<string> remove = null;

#if ENABLE_INPUT_SYSTEM
        IReadOnlyList<InputDevice> pads = GamepadGuestInput.Pads;
#endif

        foreach (KeyValuePair<string, int> pair in assignedGamepadSlots)
        {
            bool removeAssignment = !IsGuestSlotActive(pair.Value);

#if ENABLE_INPUT_SYSTEM
            if (!removeAssignment)
            {
                bool connected = false;

                for (int i = 0; i < pads.Count; i++)
                {
                    if (pads[i] != null &&
                        GamepadGuestInput.PadId(pads[i]) == pair.Key)
                    {
                        connected = true;
                        break;
                    }
                }

                if (!connected)
                    removeAssignment = true;
            }
#endif

            if (removeAssignment)
            {
                if (remove == null)
                    remove = new List<string>();

                remove.Add(pair.Key);
                DeactivateSlot(pair.Value);
            }
        }

        if (remove == null)
            return;

        for (int i = 0; i < remove.Count; i++)
            assignedGamepadSlots.Remove(remove[i]);
    }

    private bool IsGuestSlotActive(int index)
    {
        if (index < 1 || index >= slots.Count)
            return false;

        Slot slot = slots[index];
        if (slot == null || slot.controls == null)
            return false;

        return slot.controls.enabled &&
               slot.controls.mode != GuestControlMode.Disabled &&
               slot.controls.mode != GuestControlMode.Mouse;
    }

    private int FindFreeGuestSlot()
    {
        for (int i = 1; i < 4; i++)
        {
            if (!IsGuestSlotActive(i))
                return i;
        }

        return -1;
    }

    private void ActivateKeyboardAssignment(
        AutoJoinInput inputType,
        ref int assignedSlot)
    {
        if (assignedSlot >= 1 && IsGuestSlotActive(assignedSlot))
            return;

        int slotIndex = FindFreeGuestSlot();
        if (slotIndex < 0)
            return;

        Slot slot = slots[slotIndex];
        slot.device = GuestDevice.None;
        slot.controls.enabled = true;

        if (inputType == AutoJoinInput.WASD)
            slot.controls.SetWASD();
        else
            slot.controls.SetArrows();

        assignedSlot = slotIndex;
        BuildPlayers();
    }

    private void DeactivateKeyboardAssignment(ref int assignedSlot)
    {
        if (assignedSlot < 1)
        {
            assignedSlot = -1;
            return;
        }

        DeactivateSlot(assignedSlot);
        assignedSlot = -1;
    }

    private bool IsAssignedGamepad(string id)
    {
        return !string.IsNullOrEmpty(id) &&
               assignedGamepadSlots.ContainsKey(id) &&
               IsGuestSlotActive(assignedGamepadSlots[id]);
    }

    private void DeactivateSlot(int index)
    {
        if (index < 1 || index >= slots.Count)
            return;

        Slot slot = slots[index];
        if (slot == null)
            return;

        slot.controls.enabled = false;
        slot.controls.mode = GuestControlMode.Disabled;
        slot.controls.gamepadId = "";
        slot.device = GuestDevice.None;
        BuildPlayers();
    }

    private bool ShouldRun()
    {
        if (
            LVManager.Instance == null ||
            !LVManager.Instance.GameIsStart
        )
        {
            return false;
        }

        if (
            MapManager.Instance == null ||
            MapManager.Instance.mapList == null ||
            MapManager.Instance.mapList.Count == 0
        )
        {
            return false;
        }

        if (
            SeedBank.Instance == null ||
            LV.Instance == null
        )
        {
            return false;
        }

        return
            LV.Instance.CurrLVType !=
                LVType.IZombie &&
            LV.Instance.CurrLVType !=
                LVType.PvP;
    }

    private static bool TypingInInputField()
    {
        EventSystem es =
            EventSystem.current;

        if (
            es == null ||
            es.currentSelectedGameObject == null
        )
        {
            return false;
        }

        GameObject selected =
            es.currentSelectedGameObject;

        return
            selected.GetComponent<
                UnityEngine.UI.InputField
            >() != null ||
            selected.GetComponent(
                "TMP_InputField"
            ) != null;
    }

    private void Update()
    {
        UpdateAutoAssignment();

        if (!ShouldRun())
        {
            for (
                int i = 0;
                i < Players.Count;
                i++
            )
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

        if (
            map == null ||
            map.GridList == null ||
            map.GridList.Count == 0
        )
        {
            return;
        }

        List<Grid> grids =
            map.GridList;

        cardBuffer.Clear();

        IReadOnlyList<PlantCard> cards =
            SeedBank.Instance.SlotCards;

        for (
            int i = 0;
            i < cards.Count;
            i++
        )
        {
            PlantCard card =
                cards[i];

            if (
                card != null &&
                card.gameObject.activeInHierarchy &&
                card.CardPlantType !=
                    PlantType.Nope
            )
            {
                cardBuffer.Add(
                    card
                );
            }
        }

        beltCardSet.Clear();

        if (
            guestsUseConveyorBelt &&
            ConveyorBelt.Instance != null &&
            ConveyorBelt.Instance.BeltCards != null
        )
        {
            List<Transform> beltCards =
                ConveyorBelt.Instance.BeltCards;

            for (
                int i = 0;
                i < beltCards.Count;
                i++
            )
            {
                Transform t =
                    beltCards[i];

                if (t == null)
                    continue;

                PlantCard card =
                    t.GetComponent<PlantCard>();

                if (
                    card != null &&
                    card.gameObject.activeInHierarchy &&
                    card.CardPlantType !=
                        PlantType.Nope &&
                    t.localPosition.x <=
                        beltMaxSelectableLocalX
                )
                {
                    cardBuffer.Add(
                        card
                    );

                    beltCardSet.Add(
                        card
                    );
                }
            }
        }

        float cell =
            GetCellSize(
                grids
            );

        for (
            int i = 0;
            i < Players.Count;
            i++
        )
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

            if (
                !player.Input.IsAvailable
            )
            {
                player.Clear();
                continue;
            }

            player.Tick(
                grids,
                cardBuffer
            );

            if (guestCanCollect)
            {
                CollectObjectsForPlayer(
                    player
                );
            }
        }
    }

    private void CollectObjectsForPlayer(
        GuestPlayer player)
    {
        if (
            player == null ||
            player.CurrentGrid == null
        )
        {
            return;
        }

        Vector3 position =
            new Vector3(
                player.CurrentGrid.Position.x +
                    player.CursorOffset.x,

                player.CurrentGrid.Position.y +
                    player.CursorOffset.y,

                0f
            );

        QueryTriggerInteraction triggerMode =
            includeTriggerColliders
                ? QueryTriggerInteraction.Collide
                : QueryTriggerInteraction.Ignore;

        int count =
            Physics.OverlapSphereNonAlloc(
                position,
                collectRadius,
                collectColliderBuffer,
                Physics.AllLayers,
                triggerMode
            );

        for (
            int i = 0;
            i < count;
            i++
        )
        {
            Collider collider =
                collectColliderBuffer[i];

            if (collider == null)
                continue;

            Sun sun =
                collider.GetComponentInParent<Sun>();

            if (sun != null)
            {
                if (sun.CanGet)
                    sun.CollectSun();

                continue;
            }

            Allcoin coin =
                collider.GetComponentInParent<Allcoin>();

            if (coin != null)
                coin.OnMouseDown();
        }

        for (
            int i = 0;
            i < count;
            i++
        )
        {
            collectColliderBuffer[i] =
                null;
        }
    }

    private float GetCellSize(
        List<Grid> grids)
    {
        if (
            grids == cellGridsRef &&
            grids.Count ==
                cellGridsCount
        )
        {
            return cellSize;
        }

        cellGridsRef =
            grids;

        cellGridsCount =
            grids.Count;

        float best =
            float.MaxValue;

        for (
            int i = 0;
            i < grids.Count;
            i++
        )
        {
            if (grids[i] == null)
                continue;

            for (
                int j = i + 1;
                j < grids.Count;
                j++
            )
            {
                if (grids[j] == null)
                    continue;

                Vector2 delta =
                    grids[j].Position -
                    grids[i].Position;

                if (
                    Mathf.Abs(delta.y) <
                        0.05f &&
                    Mathf.Abs(delta.x) >
                        0.1f
                )
                {
                    best =
                        Mathf.Min(
                            best,
                            Mathf.Abs(
                                delta.x
                            )
                        );
                }
            }
        }

        if (
            best <
            float.MaxValue
        )
        {
            cellSize =
                best;
        }

        return cellSize;
    }

    private void OnDestroy()
    {
        for (
            int i = 0;
            i < Players.Count;
            i++
        )
        {
            if (Players[i] != null)
                Players[i].Destroy();
        }

        Players.Clear();

        if (Instance == this)
            Instance = null;
    }
}