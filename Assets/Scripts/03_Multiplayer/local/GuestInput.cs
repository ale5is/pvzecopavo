using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Dispositivo asignable a un jugador local invitado.</summary>
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

/// <summary>Lectura de un frame de un dispositivo. Move.y = +1 es "arriba" (eje Y del mundo).</summary>
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

    /// <summary>Devuelve el estado del frame. El movimiento se repite mientras se mantiene la direccion.</summary>
    public GuestInputState Poll()
    {
        GuestInputState s = default;

        Vector2Int dir = HeldDirection();
        if (dir == Vector2Int.zero)
        {
            lastDir = Vector2Int.zero;
        }
        else if (dir != lastDir)
        {
            s.Move = dir;
            lastDir = dir;
            nextRepeat = Time.unscaledTime + RepeatDelay;
        }
        else if (Time.unscaledTime >= nextRepeat)
        {
            s.Move = dir;
            nextRepeat = Time.unscaledTime + RepeatRate;
        }

        s.Prev = PrevPressed();
        s.Next = NextPressed();
        s.Confirm = ConfirmPressed();
        s.Cancel = CancelPressed();
        return s;
    }

    protected static Vector2Int Dominant(float x, float y, float threshold)
    {
        if (Mathf.Abs(x) < threshold && Mathf.Abs(y) < threshold)
            return Vector2Int.zero;

        if (Mathf.Abs(x) >= Mathf.Abs(y))
            return new Vector2Int(x > 0f ? 1 : -1, 0);

        return new Vector2Int(0, y > 0f ? 1 : -1);
    }

    public static GuestInput Create(GuestDevice device)
    {
        switch (device)
        {
            case GuestDevice.KeyboardWASD:
                return new KeyboardGuestInput("Teclado WASD",
                    KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D,
                    KeyCode.Q, KeyCode.E, KeyCode.Space, KeyCode.R);

            case GuestDevice.KeyboardArrows:
                return new KeyboardGuestInput("Teclado Flechas",
                    KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
                    KeyCode.Comma, KeyCode.Period, KeyCode.RightShift, KeyCode.RightControl);

            case GuestDevice.KeyboardIJKL:
                return new KeyboardGuestInput("Teclado IJKL",
                    KeyCode.I, KeyCode.K, KeyCode.J, KeyCode.L,
                    KeyCode.U, KeyCode.O, KeyCode.H, KeyCode.Y);

            case GuestDevice.Gamepad1: return new GamepadGuestInput(0);
            case GuestDevice.Gamepad2: return new GamepadGuestInput(1);
            case GuestDevice.Gamepad3: return new GamepadGuestInput(2);
            case GuestDevice.Gamepad4: return new GamepadGuestInput(3);
            default: return null;
        }
    }
}

public class KeyboardGuestInput : GuestInput
{
    private readonly string label;
    private readonly KeyCode up, down, left, right, prev, next, confirm, cancel;

    public KeyboardGuestInput(string label,
        KeyCode up, KeyCode down, KeyCode left, KeyCode right,
        KeyCode prev, KeyCode next, KeyCode confirm, KeyCode cancel)
    {
        this.label = label;
        this.up = up; this.down = down; this.left = left; this.right = right;
        this.prev = prev; this.next = next; this.confirm = confirm; this.cancel = cancel;
    }

    public override string Label => label;

    protected override Vector2Int HeldDirection()
    {
        int x = (Input.GetKey(right) ? 1 : 0) - (Input.GetKey(left) ? 1 : 0);
        int y = (Input.GetKey(up) ? 1 : 0) - (Input.GetKey(down) ? 1 : 0);
        if (x != 0) y = 0; // un solo eje a la vez
        return new Vector2Int(x, y);
    }

    protected override bool PrevPressed() => Input.GetKeyDown(prev);
    protected override bool NextPressed() => Input.GetKeyDown(next);
    protected override bool ConfirmPressed() => Input.GetKeyDown(confirm);
    protected override bool CancelPressed() => Input.GetKeyDown(cancel);
}

/// <summary>
/// Joystick/gamepad: cruceta o stick izquierdo mueve, LB/RB cambia de carta,
/// A (sur) confirma, B (este) cancela. Requiere el paquete Input System de Unity
/// (Project Settings > Player > Active Input Handling = "Both").
/// </summary>
public class GamepadGuestInput : GuestInput
{
    private readonly int index;

    public GamepadGuestInput(int index)
    {
        this.index = index;
    }

    public override string Label => "Gamepad " + (index + 1);

#if ENABLE_INPUT_SYSTEM
    private Gamepad Pad => index < Gamepad.all.Count ? Gamepad.all[index] : null;

    public override bool IsAvailable => Pad != null;

    protected override Vector2Int HeldDirection()
    {
        Gamepad p = Pad;
        if (p == null) return Vector2Int.zero;

        Vector2 v = p.dpad.ReadValue();
        if (v == Vector2.zero) v = p.leftStick.ReadValue();
        return Dominant(v.x, v.y, 0.5f);
    }

    protected override bool PrevPressed() { Gamepad p = Pad; return p != null && p.leftShoulder.wasPressedThisFrame; }
    protected override bool NextPressed() { Gamepad p = Pad; return p != null && p.rightShoulder.wasPressedThisFrame; }
    protected override bool ConfirmPressed() { Gamepad p = Pad; return p != null && p.buttonSouth.wasPressedThisFrame; }
    protected override bool CancelPressed() { Gamepad p = Pad; return p != null && p.buttonEast.wasPressedThisFrame; }
#else
    // Sin el Input System el gamepad queda deshabilitado (el Input clasico no distingue mandos
    // sin definir ejes por jugador en el Input Manager).
    public override bool IsAvailable => false;
    protected override Vector2Int HeldDirection() => Vector2Int.zero;
    protected override bool PrevPressed() => false;
    protected override bool NextPressed() => false;
    protected override bool ConfirmPressed() => false;
    protected override bool CancelPressed() => false;
#endif
}
