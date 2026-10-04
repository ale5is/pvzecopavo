using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

public enum GuestDevice
{
    None,
    KeyboardWASD,
    KeyboardArrows,
    KeyboardIJKL,
    Gamepad1,
    Gamepad2,
    Gamepad3,
    Gamepad4
}

public struct GuestInputState
{
    public Vector2Int Move;
    public bool Prev;
    public bool Next;
    public bool Confirm;
    public bool Cancel;
}

public abstract class GuestInput
{
    private const float RepeatDelay = 0.35f;
    private const float RepeatRate = 0.12f;

    private Vector2Int lastDir;
    private float nextRepeat;

    public abstract string Label { get; }

    public virtual bool IsAvailable => true;

    protected abstract Vector2Int HeldDirection();
    protected abstract bool PrevPressed();
    protected abstract bool NextPressed();
    protected abstract bool ConfirmPressed();
    protected abstract bool CancelPressed();

    public GuestInputState Poll()
    {
        GuestInputState s = default;

        Vector2Int dir =
            HeldDirection();

        if (dir == Vector2Int.zero)
        {
            lastDir = Vector2Int.zero;
        }
        else if (dir != lastDir)
        {
            s.Move = dir;

            lastDir = dir;

            nextRepeat =
                Time.unscaledTime +
                RepeatDelay;
        }
        else if (Time.unscaledTime >= nextRepeat)
        {
            s.Move = dir;

            nextRepeat =
                Time.unscaledTime +
                RepeatRate;
        }

        s.Prev =
            PrevPressed();

        s.Next =
            NextPressed();

        s.Confirm =
            ConfirmPressed();

        s.Cancel =
            CancelPressed();

        return s;
    }

    protected static Vector2Int Dominant(
        float x,
        float y,
        float threshold)
    {
        if (
            Mathf.Abs(x) < threshold &&
            Mathf.Abs(y) < threshold
        )
        {
            return Vector2Int.zero;
        }

        if (Mathf.Abs(x) >= Mathf.Abs(y))
        {
            return new Vector2Int(
                x > 0f ? 1 : -1,
                0
            );
        }

        return new Vector2Int(
            0,
            y > 0f ? 1 : -1
        );
    }

    public static GuestInput Create(
        GuestControlProfile profile)
    {
        if (
            profile == null ||
            !profile.enabled
        )
        {
            return null;
        }

        if (profile.mode == GuestControlMode.Keyboard)
        {
            return new KeyboardGuestInput(profile);
        }

        if (profile.mode == GuestControlMode.Gamepad)
        {
            return new GamepadGuestInput(
                Mathf.Clamp(profile.gamepadNumber, 1, 4) - 1,
                profile.gamepadId
            );
        }

        return null;
    }

    public static GuestInput Create(
        GuestDevice device)
    {
        if (device == GuestDevice.KeyboardWASD)
        {
            GuestControlProfile p = new GuestControlProfile();
            p.SetWASD();
            return Create(p);
        }

        if (device == GuestDevice.KeyboardArrows)
        {
            GuestControlProfile p = new GuestControlProfile();
            p.SetArrows();
            return Create(p);
        }

        if (device == GuestDevice.KeyboardIJKL)
        {
            GuestControlProfile p = new GuestControlProfile();
            p.SetIJKL();
            return Create(p);
        }

        if (
            device == GuestDevice.Gamepad1 ||
            device == GuestDevice.Gamepad2 ||
            device == GuestDevice.Gamepad3 ||
            device == GuestDevice.Gamepad4
        )
        {
            GuestControlProfile p = new GuestControlProfile();
            p.SetGamepad(
                (int)device - (int)GuestDevice.Gamepad1 + 1
            );
            return Create(p);
        }

        return null;
    }

}

public sealed class KeyboardGuestInput : GuestInput
{
    private readonly GuestControlProfile profile;

    public KeyboardGuestInput(
        GuestControlProfile profile)
    {
        this.profile = profile;
    }

    public override string Label =>
        "Teclado";

    private bool joined;

    // En PC cuenta como disponible si hay teclado. En celular el sistema registra "teclados"
    // fantasma (botones de volumen, sensor de huella, el propio mando), asi que el invitado de
    // teclado solo aparece cuando se aprieta una de sus teclas en un teclado real.
    public override bool IsAvailable
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
                return false;

            if (!Application.isMobilePlatform)
                return true;

            if (!joined && !GamepadGuestInput.IsPadSibling(keyboard) && AnyOwnKeyDown())
                joined = true;

            return joined;
#else
            return false;
#endif
        }
    }

    private bool AnyOwnKeyDown()
    {
        return InputCompat.GetKeyDown(profile.up) ||
               InputCompat.GetKeyDown(profile.down) ||
               InputCompat.GetKeyDown(profile.left) ||
               InputCompat.GetKeyDown(profile.right) ||
               InputCompat.GetKeyDown(profile.previousCard) ||
               InputCompat.GetKeyDown(profile.nextCard) ||
               InputCompat.GetKeyDown(profile.confirm) ||
               InputCompat.GetKeyDown(profile.cancel);
    }

    protected override Vector2Int HeldDirection()
    {
        int x =
            (InputCompat.GetKey(profile.right)
                ? 1
                : 0)
            -
            (InputCompat.GetKey(profile.left)
                ? 1
                : 0);

        int y =
            (InputCompat.GetKey(profile.up)
                ? 1
                : 0)
            -
            (InputCompat.GetKey(profile.down)
                ? 1
                : 0);

        if (x != 0)
            y = 0;

        return new Vector2Int(
            x,
            y
        );
    }

    protected override bool PrevPressed()
    {
        return InputCompat.GetKeyDown(
            profile.previousCard
        );
    }

    protected override bool NextPressed()
    {
        return InputCompat.GetKeyDown(
            profile.nextCard
        );
    }

    protected override bool ConfirmPressed()
    {
        return InputCompat.GetKeyDown(
            profile.confirm
        );
    }

    protected override bool CancelPressed()
    {
        return InputCompat.GetKeyDown(
            profile.cancel
        );
    }
}

public sealed class GamepadGuestInput : GuestInput
{
    private readonly int index;
    private readonly string deviceId;

    public GamepadGuestInput(int index, string deviceId = "")
    {
        this.index = index;
        this.deviceId = deviceId ?? "";
    }

    public override string Label =>
        "Gamepad " + (index + 1);

#if ENABLE_INPUT_SYSTEM

    // Lista de mandos: los Gamepad reconocidos por el Input System o, si no hay ninguno,
    // los Joystick / HID genericos. Muchos mandos Bluetooth de celular entran como Joystick o
    // HID y no como Gamepad, por eso antes no se detectaban.
    private static readonly List<InputDevice> pads =
        new List<InputDevice>();

    private static int padsFrame = -1;

    public static IReadOnlyList<InputDevice> Pads
    {
        get
        {
            RefreshPads();
            return pads;
        }
    }

    private static readonly HashSet<string> seenPadIds =
        new HashSet<string>();

    // Identificador de mando fisico. En Android un mismo mando puede registrarse como varios
    // dispositivos del Input System (por ejemplo Gamepad + HID/teclado); comparten "descriptor".
    public static string PadId(InputDevice d)
    {
        string caps = d.description.capabilities;

        if (!string.IsNullOrEmpty(caps))
        {
            int k = caps.IndexOf(
                "\"descriptor\"",
                System.StringComparison.Ordinal
            );

            if (k >= 0)
            {
                int colon = caps.IndexOf(':', k);
                int q1 = colon >= 0 ? caps.IndexOf('"', colon + 1) : -1;
                int q2 = q1 >= 0 ? caps.IndexOf('"', q1 + 1) : -1;

                if (q2 > q1 + 1)
                    return "d:" + caps.Substring(q1 + 1, q2 - q1 - 1);
            }
        }

        return "k:" + d.description.interfaceName + "|" +
               d.description.manufacturer + "|" +
               d.description.product + "|" +
               d.description.version + "|" +
               d.description.serial;
    }

    // Dispositivos del propio telefono que Android expone como si fueran mandos
    // (por ejemplo "uinput-fpc", el sensor de huella). Se pueden agregar mas nombres.
    private static readonly string[] ignoredNameParts =
    {
        "uinput",
        "fpc",
        "fingerprint",
        "goodix",
        "gpio"
    };

    private static bool IsIgnoredDevice(InputDevice d)
    {
        string name = d.displayName + " " + d.description.product;

        if (name.IndexOf("virtual", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        foreach (string part in ignoredNameParts)
        {
            if (name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        string caps = d.description.capabilities;

        return !string.IsNullOrEmpty(caps) &&
               caps.IndexOf(
                   "\"isVirtual\":true",
                   System.StringComparison.Ordinal
               ) >= 0;
    }

    // True si el dispositivo (por ejemplo un "teclado") es en realidad parte de un mando conectado.
    public static bool IsPadSibling(InputDevice d)
    {
        string product = d.description.product;

        if (string.IsNullOrEmpty(product))
            return false;

        RefreshPads();

        foreach (InputDevice p in pads)
        {
            if (p != d && p.description.product == product)
                return true;
        }

        return false;
    }

    // Un dispositivo generico solo cuenta como mando si tiene una cruceta/stick y varios botones
    // (un telefono tiene dispositivos "fantasma" con solo uno o dos botones, como volumen o encendido).
    private static bool LooksLikeController(InputDevice d)
    {
        int buttons = 0;
        bool hasDirection = false;

        foreach (InputControl c in d.allControls)
        {
            if (c.parent != d || c.synthetic)
                continue;

            if (c is ButtonControl)
                buttons++;
            else if (c is Vector2Control)
                hasDirection = true;
        }

        return hasDirection && buttons >= 4;
    }

    public static bool StartButtonPressed(InputDevice d)
    {
        ButtonControl button = FindButton(
            d,
            "startButton",
            "start",
            "button9"
        );

        return button != null && button.wasPressedThisFrame;
    }

    public static bool SelectButtonPressed(InputDevice d)
    {
        ButtonControl button = FindButton(
            d,
            "selectButton",
            "select",
            "button10"
        );

        return button != null && button.wasPressedThisFrame;
    }

    public static bool IsConnected(InputDevice d)
    {
        return d != null && d.added;
    }

    private static void RefreshPads()
    {
        if (padsFrame == Time.frameCount)
            return;

        padsFrame = Time.frameCount;
        pads.Clear();
        seenPadIds.Clear();

        foreach (Gamepad g in Gamepad.all)
        {
            // Un mismo mando fisico no cuenta dos veces y los dispositivos virtuales no son mandos.
            if (IsIgnoredDevice(g))
                continue;

            if (seenPadIds.Add(PadId(g)))
                pads.Add(g);
        }

        // Los Joystick / HID genericos solo se usan si el sistema no reconocio ningun Gamepad;
        // si no, el mismo mando aparecia dos veces (Gamepad 1 y Gamepad 2).
        if (pads.Count > 0)
            return;

        foreach (InputDevice d in InputSystem.devices)
        {
            if (d == null || !d.added || d is Gamepad)
                continue;

            bool generic =
                d is Joystick ||
                (
                    d.description.interfaceName == "HID" &&
                    !(d is Keyboard) &&
                    !(d is Pointer)
                );

            if (!generic || IsIgnoredDevice(d) || !LooksLikeController(d))
                continue;

            if (seenPadIds.Add(PadId(d)))
                pads.Add(d);
        }
    }

    private InputDevice mappedDevice;
    private Vector2Control dirControl;
    private Vector2Control stickControl;
    private ButtonControl bConfirm;
    private ButtonControl bCancel;
    private ButtonControl bPrev;
    private ButtonControl bNext;
    private ButtonControl bStart;
    private ButtonControl bSelect;

    private InputDevice Pad
    {
        get
        {
            RefreshPads();

            InputDevice d = null;

            if (!string.IsNullOrEmpty(deviceId))
            {
                for (int i = 0; i < pads.Count; i++)
                {
                    if (PadId(pads[i]) == deviceId)
                    {
                        d = pads[i];
                        break;
                    }
                }
            }

            if (d == null && index >= 0 && index < pads.Count)
                d = pads[index];

            if (d != mappedDevice)
                BuildMap(d);

            return d;
        }
    }

    public override bool IsAvailable =>
        Pad != null;

    public bool StartPressed
    {
        get
        {
            return Pad != null &&
                   bStart != null &&
                   bStart.wasPressedThisFrame;
        }
    }

    public bool SelectPressed
    {
        get
        {
            return Pad != null &&
                   bSelect != null &&
                   bSelect.wasPressedThisFrame;
        }
    }

    public InputDevice CurrentDevice
    {
        get { return Pad; }
    }


    private static ButtonControl FindButton(
        InputDevice d,
        string a,
        string b,
        string c)
    {
        InputControl control = d.TryGetChildControl(a);
        if (control is ButtonControl) return (ButtonControl)control;

        control = d.TryGetChildControl(b);
        if (control is ButtonControl) return (ButtonControl)control;

        control = d.TryGetChildControl(c);
        if (control is ButtonControl) return (ButtonControl)control;

        return null;
    }

    private static Vector2Control FindVector(
        InputDevice d,
        string a,
        string b,
        string c)
    {
        InputControl control = d.TryGetChildControl(a);
        if (control is Vector2Control) return (Vector2Control)control;

        control = d.TryGetChildControl(b);
        if (control is Vector2Control) return (Vector2Control)control;

        control = d.TryGetChildControl(c);
        if (control is Vector2Control) return (Vector2Control)control;

        return null;
    }

    private void BuildMap(InputDevice d)
    {
        mappedDevice = d;
        dirControl = null;
        stickControl = null;
        bConfirm = null;
        bCancel = null;
        bPrev = null;
        bNext = null;
        bStart = null;
        bSelect = null;

        if (d == null)
            return;

        Gamepad gp = d as Gamepad;
        if (gp != null)
        {
            dirControl = gp.dpad;
            stickControl = gp.leftStick;
            bPrev = gp.leftShoulder;
            bNext = gp.rightShoulder;
            bStart = gp.startButton;
            bSelect = gp.selectButton;
            bConfirm = gp.buttonSouth;
            bCancel = gp.buttonEast;
            return;
        }

        // Mando generico (Joystick / HID): se buscan los controles por nombre.
        dirControl = FindVector(d, "hatswitch", "dpad", "hat");

        Joystick js = d as Joystick;
        if (js != null)
            stickControl = js.stick;
        else
            stickControl = FindVector(d, "stick", "leftStick", "stick1");

        bConfirm = FindButton(d, "buttonSouth", "button1", "trigger");
        bCancel = FindButton(d, "buttonEast", "button2", "button3");
        bPrev = FindButton(d, "leftShoulder", "button5", "button7");
        bNext = FindButton(d, "rightShoulder", "button6", "button8");
        bStart = FindButton(d, "startButton", "start", "button9");
        bSelect = FindButton(d, "selectButton", "select", "button10");

        // Si no hay nombres conocidos, se usan los botones directos del dispositivo en orden.
        List<ButtonControl> ordered = new List<ButtonControl>();
        foreach (InputControl c in d.allControls)
        {
            ButtonControl bc = c as ButtonControl;
            if (bc != null && c.parent == d && !c.synthetic)
                ordered.Add(bc);
        }

        if (bConfirm == null && ordered.Count > 0) bConfirm = ordered[0];
        if (bCancel == null && ordered.Count > 1) bCancel = ordered[1];
        if (bPrev == null && ordered.Count > 4) bPrev = ordered[4];
        if (bNext == null && ordered.Count > 5) bNext = ordered[5];
    }

    protected override Vector2Int HeldDirection()
    {
        if (Pad == null)
            return Vector2Int.zero;

        Vector2 v = Vector2.zero;

        if (dirControl != null)
            v = dirControl.ReadValue();

        if (v == Vector2.zero && stickControl != null)
            v = stickControl.ReadValue();

        return Dominant(
            v.x,
            v.y,
            0.5f
        );
    }

    protected override bool PrevPressed()
    {
        return Pad != null &&
               bPrev != null &&
               bPrev.wasPressedThisFrame;
    }

    protected override bool NextPressed()
    {
        return Pad != null &&
               bNext != null &&
               bNext.wasPressedThisFrame;
    }

    protected override bool ConfirmPressed()
    {
        return Pad != null &&
               bConfirm != null &&
               bConfirm.wasPressedThisFrame;
    }

    protected override bool CancelPressed()
    {
        return Pad != null &&
               bCancel != null &&
               bCancel.wasPressedThisFrame;
    }

#else

    public override bool IsAvailable =>
        false;

    protected override Vector2Int HeldDirection() =>
        Vector2Int.zero;

    protected override bool PrevPressed() =>
        false;

    protected override bool NextPressed() =>
        false;

    protected override bool ConfirmPressed() =>
        false;

    protected override bool CancelPressed() =>
        false;

#endif
}