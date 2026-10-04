using System;
using UnityEngine;

public enum GuestControlMode
{
    Disabled,
    Mouse,
    Keyboard,
    Gamepad
}

[Serializable]
public class GuestControlProfile
{
    [Tooltip("Activa o desactiva este jugador local.")]
    public bool enabled = true;

    [Tooltip("Forma de controlar este jugador.")]
    public GuestControlMode mode = GuestControlMode.Disabled;

    [Range(1, 4)]
    [Tooltip("Numero de mando de Unity: 1, 2, 3 o 4.")]
    public int gamepadNumber = 1;

    [HideInInspector]
    public string gamepadId = "";

    [Header("Movimiento")]
    public KeyCode up = KeyCode.W;
    public KeyCode down = KeyCode.S;
    public KeyCode left = KeyCode.A;
    public KeyCode right = KeyCode.D;

    [Header("Cartas")]
    public KeyCode previousCard = KeyCode.Q;
    public KeyCode nextCard = KeyCode.E;

    [Header("Acciones")]
    public KeyCode confirm = KeyCode.Space;
    public KeyCode cancel = KeyCode.R;

    public GuestControlProfile Clone()
    {
        return new GuestControlProfile
        {
            enabled = enabled,
            mode = mode,
            gamepadNumber = gamepadNumber,
            gamepadId = gamepadId,
            up = up,
            down = down,
            left = left,
            right = right,
            previousCard = previousCard,
            nextCard = nextCard,
            confirm = confirm,
            cancel = cancel
        };
    }

    public void SetWASD()
    {
        mode = GuestControlMode.Keyboard;

        up = KeyCode.W;
        down = KeyCode.S;
        left = KeyCode.A;
        right = KeyCode.D;

        previousCard = KeyCode.Q;
        nextCard = KeyCode.E;

        confirm = KeyCode.Space;
        cancel = KeyCode.R;
    }

    public void SetArrows()
    {
        mode = GuestControlMode.Keyboard;

        up = KeyCode.UpArrow;
        down = KeyCode.DownArrow;
        left = KeyCode.LeftArrow;
        right = KeyCode.RightArrow;

        previousCard = KeyCode.Comma;
        nextCard = KeyCode.Period;

        confirm = KeyCode.RightShift;
        cancel = KeyCode.RightControl;
    }

    public void SetIJKL()
    {
        mode = GuestControlMode.Keyboard;

        up = KeyCode.I;
        down = KeyCode.K;
        left = KeyCode.J;
        right = KeyCode.L;

        previousCard = KeyCode.U;
        nextCard = KeyCode.O;

        confirm = KeyCode.H;
        cancel = KeyCode.Y;
    }

    public void SetGamepad(int number)
    {
        mode = GuestControlMode.Gamepad;
        gamepadNumber = Mathf.Clamp(number, 1, 4);
        gamepadId = "";
    }

    public void SetGamepad(string id, int number)
    {
        mode = GuestControlMode.Gamepad;
        gamepadNumber = Mathf.Clamp(number, 1, 4);
        gamepadId = id ?? "";
    }

    public void SetMouse()
    {
        mode = GuestControlMode.Mouse;
    }

    public void SetDisabled()
    {
        mode = GuestControlMode.Disabled;
    }
}