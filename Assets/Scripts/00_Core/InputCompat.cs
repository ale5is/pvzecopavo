using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public static class InputCompat
{
    public static Vector3 mousePosition
    {
        get
        {
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();

            if (Touchscreen.current != null)
                return Touchscreen.current.primaryTouch.position.ReadValue();

            return Vector3.zero;
        }
    }

    public static int touchCount
    {
        get
        {
            Touchscreen touchscreen = Touchscreen.current;

            if (touchscreen == null)
                return 0;

            int count = 0;

            foreach (TouchControl touch in touchscreen.touches)
            {
                if (touch.press.isPressed)
                    count++;
            }

            return count;
        }
    }

    public static UnityEngine.Touch GetTouch(int index)
    {
        Touchscreen touchscreen = Touchscreen.current;

        if (touchscreen == null || index < 0)
            return default;

        int activeIndex = 0;

        foreach (TouchControl touch in touchscreen.touches)
        {
            bool pressed = touch.press.isPressed;
            bool pressedThisFrame = touch.press.wasPressedThisFrame;
            bool releasedThisFrame = touch.press.wasReleasedThisFrame;

            if (!pressed && !releasedThisFrame)
                continue;

            if (activeIndex != index)
            {
                if (pressed)
                    activeIndex++;

                continue;
            }

            UnityEngine.TouchPhase phase = UnityEngine.TouchPhase.Moved;

            if (pressedThisFrame)
                phase = UnityEngine.TouchPhase.Began;
            else if (releasedThisFrame)
                phase = UnityEngine.TouchPhase.Ended;
            else if (!pressed)
                phase = UnityEngine.TouchPhase.Canceled;

            return new UnityEngine.Touch
            {
                fingerId = activeIndex,
                position = touch.position.ReadValue(),
                phase = phase,
                deltaPosition = touch.delta.ReadValue()
            };
        }

        return default;
    }

    public static float GetAxis(string axisName)
    {
        if (axisName == "Mouse ScrollWheel")
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
                return 0f;

            return mouse.scroll.ReadValue().y / 120f;
        }

        if (axisName == "Horizontal")
            return GetKeyboardAxis(Key.LeftArrow, Key.RightArrow);

        if (axisName == "Vertical")
            return GetKeyboardAxis(Key.DownArrow, Key.UpArrow);

        return 0f;
    }

    private static float GetKeyboardAxis(Key negative, Key positive)
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return 0f;

        float value = 0f;

        if (keyboard[negative].isPressed)
            value -= 1f;

        if (keyboard[positive].isPressed)
            value += 1f;

        return value;
    }

    public static bool GetKey(KeyCode keyCode)
    {
        KeyControl control = GetKeyControl(keyCode);

        return control != null && control.isPressed;
    }

    public static bool GetKeyDown(KeyCode keyCode)
    {
        KeyControl control = GetKeyControl(keyCode);

        return control != null && control.wasPressedThisFrame;
    }

    public static bool GetKeyUp(KeyCode keyCode)
    {
        KeyControl control = GetKeyControl(keyCode);

        return control != null && control.wasReleasedThisFrame;
    }

    private static KeyControl GetKeyControl(KeyCode keyCode)
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return null;

        Key key;

        if (!TryConvertKeyCode(keyCode, out key))
            return null;

        return keyboard[key];
    }

    private static bool TryConvertKeyCode(KeyCode keyCode, out Key key)
    {
        if (keyCode == KeyCode.A) { key = Key.A; return true; }
        if (keyCode == KeyCode.B) { key = Key.B; return true; }
        if (keyCode == KeyCode.C) { key = Key.C; return true; }
        if (keyCode == KeyCode.D) { key = Key.D; return true; }
        if (keyCode == KeyCode.E) { key = Key.E; return true; }
        if (keyCode == KeyCode.F) { key = Key.F; return true; }
        if (keyCode == KeyCode.G) { key = Key.G; return true; }
        if (keyCode == KeyCode.H) { key = Key.H; return true; }
        if (keyCode == KeyCode.I) { key = Key.I; return true; }
        if (keyCode == KeyCode.J) { key = Key.J; return true; }
        if (keyCode == KeyCode.K) { key = Key.K; return true; }
        if (keyCode == KeyCode.L) { key = Key.L; return true; }
        if (keyCode == KeyCode.M) { key = Key.M; return true; }
        if (keyCode == KeyCode.N) { key = Key.N; return true; }
        if (keyCode == KeyCode.O) { key = Key.O; return true; }
        if (keyCode == KeyCode.P) { key = Key.P; return true; }
        if (keyCode == KeyCode.Q) { key = Key.Q; return true; }
        if (keyCode == KeyCode.R) { key = Key.R; return true; }
        if (keyCode == KeyCode.S) { key = Key.S; return true; }
        if (keyCode == KeyCode.T) { key = Key.T; return true; }
        if (keyCode == KeyCode.U) { key = Key.U; return true; }
        if (keyCode == KeyCode.V) { key = Key.V; return true; }
        if (keyCode == KeyCode.W) { key = Key.W; return true; }
        if (keyCode == KeyCode.X) { key = Key.X; return true; }
        if (keyCode == KeyCode.Y) { key = Key.Y; return true; }
        if (keyCode == KeyCode.Z) { key = Key.Z; return true; }

        if (keyCode == KeyCode.Alpha0) { key = Key.Digit0; return true; }
        if (keyCode == KeyCode.Alpha1) { key = Key.Digit1; return true; }
        if (keyCode == KeyCode.Alpha2) { key = Key.Digit2; return true; }
        if (keyCode == KeyCode.Alpha3) { key = Key.Digit3; return true; }
        if (keyCode == KeyCode.Alpha4) { key = Key.Digit4; return true; }
        if (keyCode == KeyCode.Alpha5) { key = Key.Digit5; return true; }
        if (keyCode == KeyCode.Alpha6) { key = Key.Digit6; return true; }
        if (keyCode == KeyCode.Alpha7) { key = Key.Digit7; return true; }
        if (keyCode == KeyCode.Alpha8) { key = Key.Digit8; return true; }
        if (keyCode == KeyCode.Alpha9) { key = Key.Digit9; return true; }

        if (keyCode == KeyCode.Keypad0) { key = Key.Numpad0; return true; }
        if (keyCode == KeyCode.Keypad1) { key = Key.Numpad1; return true; }
        if (keyCode == KeyCode.Keypad2) { key = Key.Numpad2; return true; }
        if (keyCode == KeyCode.Keypad3) { key = Key.Numpad3; return true; }
        if (keyCode == KeyCode.Keypad4) { key = Key.Numpad4; return true; }
        if (keyCode == KeyCode.Keypad5) { key = Key.Numpad5; return true; }
        if (keyCode == KeyCode.Keypad6) { key = Key.Numpad6; return true; }
        if (keyCode == KeyCode.Keypad7) { key = Key.Numpad7; return true; }
        if (keyCode == KeyCode.Keypad8) { key = Key.Numpad8; return true; }
        if (keyCode == KeyCode.Keypad9) { key = Key.Numpad9; return true; }

        if (keyCode == KeyCode.KeypadEnter) { key = Key.NumpadEnter; return true; }

        if (keyCode == KeyCode.Space) { key = Key.Space; return true; }
        if (keyCode == KeyCode.Escape) { key = Key.Escape; return true; }
        if (keyCode == KeyCode.Return) { key = Key.Enter; return true; }

        if (keyCode == KeyCode.Comma) { key = Key.Comma; return true; }
        if (keyCode == KeyCode.Period) { key = Key.Period; return true; }
        if (keyCode == KeyCode.Slash) { key = Key.Slash; return true; }

        if (keyCode == KeyCode.UpArrow) { key = Key.UpArrow; return true; }
        if (keyCode == KeyCode.DownArrow) { key = Key.DownArrow; return true; }
        if (keyCode == KeyCode.LeftArrow) { key = Key.LeftArrow; return true; }
        if (keyCode == KeyCode.RightArrow) { key = Key.RightArrow; return true; }

        if (keyCode == KeyCode.LeftShift) { key = Key.LeftShift; return true; }
        if (keyCode == KeyCode.RightShift) { key = Key.RightShift; return true; }

        if (keyCode == KeyCode.LeftControl) { key = Key.LeftCtrl; return true; }
        if (keyCode == KeyCode.RightControl) { key = Key.RightCtrl; return true; }

        if (keyCode == KeyCode.LeftAlt) { key = Key.LeftAlt; return true; }
        if (keyCode == KeyCode.RightAlt) { key = Key.RightAlt; return true; }

        key = Key.None;
        return false;
    }

    public static bool GetMouseButtonDown(int button)
    {
        Mouse mouse = Mouse.current;

        if (button == 0)
        {
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;

            Touchscreen touchscreen = Touchscreen.current;

            return touchscreen != null &&
                   touchscreen.primaryTouch.press.wasPressedThisFrame;
        }

        if (button == 1)
            return mouse != null && mouse.rightButton.wasPressedThisFrame;

        if (button == 2)
            return mouse != null && mouse.middleButton.wasPressedThisFrame;

        return false;
    }

    public static bool GetMouseButtonUp(int button)
    {
        Mouse mouse = Mouse.current;

        if (button == 0)
        {
            if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
                return true;

            Touchscreen touchscreen = Touchscreen.current;

            return touchscreen != null &&
                   touchscreen.primaryTouch.press.wasReleasedThisFrame;
        }

        if (button == 1)
            return mouse != null && mouse.rightButton.wasReleasedThisFrame;

        if (button == 2)
            return mouse != null && mouse.middleButton.wasReleasedThisFrame;

        return false;
    }

    public static bool GetMouseButton(int button)
    {
        Mouse mouse = Mouse.current;

        if (button == 0)
        {
            if (mouse != null && mouse.leftButton.isPressed)
                return true;

            Touchscreen touchscreen = Touchscreen.current;

            return touchscreen != null &&
                   touchscreen.primaryTouch.press.isPressed;
        }

        if (button == 1)
            return mouse != null && mouse.rightButton.isPressed;

        if (button == 2)
            return mouse != null && mouse.middleButton.isPressed;

        return false;
    }
}