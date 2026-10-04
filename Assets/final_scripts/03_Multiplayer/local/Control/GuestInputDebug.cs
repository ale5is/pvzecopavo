using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// Prueba SOLO la entrada (sin tocar el juego). Pone este script en cualquier GameObject de una
/// escena. Muestra EN PANTALLA (sirve en el celular) y en la consola: los dispositivos que ve el
/// Input System (con su layout y si son Gamepad), cada movimiento y boton de cada invitado, y el
/// nombre de cada boton que se aprieta en mandos que no son Gamepad. Necesita GuestInput.cs.
/// Borralo cuando termines de probar.
/// </summary>
public class GuestInputDebug : MonoBehaviour
{
    public bool showOnScreen = true;

    private readonly List<GuestDevice> devices = new List<GuestDevice>
    {
        GuestDevice.KeyboardWASD,
        GuestDevice.KeyboardArrows,
        GuestDevice.KeyboardIJKL,
        GuestDevice.Gamepad1,
        GuestDevice.Gamepad2,
        GuestDevice.Gamepad3,
        GuestDevice.Gamepad4,
    };

    private readonly List<GuestInput> inputs = new List<GuestInput>();
    private readonly List<bool> wasAvailable = new List<bool>();
    private readonly List<string> lines = new List<string>();
    private const int MaxLines = 22;
    private bool warnedFocus;

    private void Log(string message)
    {
        Debug.Log("[GuestInput] " + message);
        lines.Add(message);
        if (lines.Count > MaxLines)
            lines.RemoveAt(0);
    }

    private void Start()
    {
        foreach (GuestDevice d in devices)
        {
            GuestInput input = GuestInput.Create(d);
            inputs.Add(input);
            wasAvailable.Add(false);
        }

#if ENABLE_INPUT_SYSTEM
        Log("ENABLE_INPUT_SYSTEM: activo");
        LogAllDevices();
#else
        Log("ERROR: ENABLE_INPUT_SYSTEM NO esta definido. Player Settings > Active Input Handling debe ser 'Input System Package (New)' o 'Both'.");
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added ||
            change == InputDeviceChange.Removed ||
            change == InputDeviceChange.Reconnected ||
            change == InputDeviceChange.Disconnected)
        {
            Log("Dispositivo " + change + ": " + Describe(device));
            Log("Gamepad.all=" + Gamepad.all.Count + "  mandos usables=" + GamepadGuestInput.Pads.Count);
        }
    }

    private static string Describe(InputDevice d)
    {
        string kind;
        if (d is Gamepad)
            kind = "Gamepad";
        else if (d is Joystick)
            kind = "Joystick";
        else
            kind = d.GetType().Name;

        return d.displayName + " | layout=" + d.layout + " | tipo=" + kind +
               " | interfaz=" + d.description.interfaceName +
               " | fab=" + d.description.manufacturer +
               " | prod=" + d.description.product +
               " | id=" + GamepadGuestInput.PadId(d);
    }

    private void LogAllDevices()
    {
        Log("Dispositivos: " + InputSystem.devices.Count +
            "  Gamepad.all=" + Gamepad.all.Count +
            "  mandos usables=" + GamepadGuestInput.Pads.Count);

        foreach (InputDevice d in InputSystem.devices)
            Log("- " + Describe(d));

        IReadOnlyList<InputDevice> usable = GamepadGuestInput.Pads;
        for (int i = 0; i < usable.Count; i++)
            Log("MANDO USABLE " + (i + 1) + ": " + usable[i].displayName + " (" + usable[i].layout + ")");
    }

    // En mandos que no son Gamepad, escribe el nombre del boton que se aprieta para poder mapearlo.
    private void SniffGenericPads()
    {
        IReadOnlyList<InputDevice> pads = GamepadGuestInput.Pads;
        for (int i = 0; i < pads.Count; i++)
        {
            InputDevice d = pads[i];
            if (d == null || d is Gamepad)
                continue;

            foreach (InputControl c in d.allControls)
            {
                ButtonControl b = c as ButtonControl;
                if (b != null && c.parent == d && b.wasPressedThisFrame)
                    Log("Mando " + (i + 1) + " (generico) boton: " + c.name);
            }
        }
    }
#endif

    private void Update()
    {
        if (!Application.isFocused && !warnedFocus)
        {
            warnedFocus = true;
            Log("AVISO: la ventana no tiene foco; el Input System puede ignorar los mandos.");
        }
        else if (Application.isFocused)
        {
            warnedFocus = false;
        }

#if ENABLE_INPUT_SYSTEM
        SniffGenericPads();
#endif

        for (int i = 0; i < inputs.Count; i++)
        {
            GuestInput input = inputs[i];
            if (input == null)
                continue;

            bool available = input.IsAvailable;
            if (available != wasAvailable[i])
            {
                wasAvailable[i] = available;
                Log(input.Label + (i >= 3 ? "" : " (" + devices[i] + ")") + ": " + (available ? "CONECTADO" : "no disponible"));
            }

            if (!available)
                continue;

            GuestInputState s = input.Poll();
            string label = input.Label + " [" + devices[i] + "]";

            if (s.Move != Vector2Int.zero) Log(label + ": mover " + s.Move);
            if (s.Prev) Log(label + ": carta anterior");
            if (s.Next) Log(label + ": carta siguiente");
            if (s.Confirm) Log(label + ": CONFIRMAR");
            if (s.Cancel) Log(label + ": CANCELAR");
        }
    }

    private void OnGUI()
    {
        if (!showOnScreen)
            return;

        int size = Mathf.Max(14, Screen.height / 45);
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = size;
        style.wordWrap = true;
        style.normal.textColor = Color.white;

        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height * 0.6f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string text = string.Join("\n", lines.ToArray());
        GUI.Label(new Rect(8, 8, Screen.width - 16, Screen.height * 0.6f - 16), text, style);
    }
}