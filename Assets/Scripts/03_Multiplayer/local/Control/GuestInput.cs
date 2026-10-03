using UnityEngine;

using UnityEngine.InputSystem;

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

        switch (profile.mode)
        {
            case GuestControlMode.Keyboard:
                return new KeyboardGuestInput(
                    profile
                );

            case GuestControlMode.Gamepad:
                return new GamepadGuestInput(
                    Mathf.Clamp(
                        profile.gamepadNumber,
                        1,
                        4
                    ) - 1
                );

            case GuestControlMode.Mouse:
            case GuestControlMode.Disabled:
            default:
                return null;
        }
    }

    public static GuestInput Create(
        GuestDevice device)
    {
        switch (device)
        {
            case GuestDevice.KeyboardWASD:
                {
                    GuestControlProfile p =
                        new GuestControlProfile();

                    p.SetWASD();

                    return Create(p);
                }

            case GuestDevice.KeyboardArrows:
                {
                    GuestControlProfile p =
                        new GuestControlProfile();

                    p.SetArrows();

                    return Create(p);
                }

            case GuestDevice.KeyboardIJKL:
                {
                    GuestControlProfile p =
                        new GuestControlProfile();

                    p.SetIJKL();

                    return Create(p);
                }

            case GuestDevice.Gamepad1:
            case GuestDevice.Gamepad2:
            case GuestDevice.Gamepad3:
            case GuestDevice.Gamepad4:
                {
                    GuestControlProfile p =
                        new GuestControlProfile();

                    p.SetGamepad(
                        (int)device -
                        (int)GuestDevice.Gamepad1 +
                        1
                    );

                    return Create(p);
                }

            default:
                return null;
        }
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

    public GamepadGuestInput(int index)
    {
        this.index = index;
    }

    public override string Label =>
        "Gamepad " + (index + 1);

    private Gamepad Pad =>
        index >= 0 &&
        index < Gamepad.all.Count
            ? Gamepad.all[index]
            : null;

    public override bool IsAvailable =>
        Pad != null;

    protected override Vector2Int HeldDirection()
    {
        Gamepad p = Pad;

        if (p == null)
            return Vector2Int.zero;

        Vector2 v =
            p.dpad.ReadValue();

        if (v == Vector2.zero)
            v =
                p.leftStick.ReadValue();

        return Dominant(
            v.x,
            v.y,
            0.5f
        );
    }

    protected override bool PrevPressed()
    {
        Gamepad p = Pad;

        return p != null &&
               p.leftShoulder
                   .wasPressedThisFrame;
    }

    protected override bool NextPressed()
    {
        Gamepad p = Pad;

        return p != null &&
               p.rightShoulder
                   .wasPressedThisFrame;
    }

    protected override bool ConfirmPressed()
    {
        Gamepad p = Pad;

        return p != null &&
               p.buttonSouth
                   .wasPressedThisFrame;
    }

    protected override bool CancelPressed()
    {
        Gamepad p = Pad;

        return p != null &&
               p.buttonEast
                   .wasPressedThisFrame;
    }

}