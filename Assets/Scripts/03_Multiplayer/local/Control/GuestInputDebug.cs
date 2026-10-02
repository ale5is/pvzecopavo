using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prueba SOLO la entrada (sin tocar el juego). Pone este script en cualquier GameObject de una
/// escena y mira la consola de Unity: escribe cada movimiento y boton de cada dispositivo.
/// Necesita GuestInput.cs. Borralo cuando termines de probar.
/// </summary>
public class GuestInputDebug : MonoBehaviour
{
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

    private void Start()
    {
        foreach (GuestDevice d in devices)
        {
            GuestInput input = GuestInput.Create(d);
            inputs.Add(input);
            wasAvailable.Add(false);
        }
    }

    private void Update()
    {
        for (int i = 0; i < inputs.Count; i++)
        {
            GuestInput input = inputs[i];
            if (input == null)
                continue;

            bool available = input.IsAvailable;
            if (available != wasAvailable[i])
            {
                wasAvailable[i] = available;
                Debug.Log($"[GuestInput] {input.Label}: {(available ? "CONECTADO" : "no disponible")}");
            }

            if (!available)
                continue;

            GuestInputState s = input.Poll();

            if (s.Move != Vector2Int.zero) Debug.Log($"[GuestInput] {input.Label}: mover {s.Move}");
            if (s.Prev)    Debug.Log($"[GuestInput] {input.Label}: carta anterior");
            if (s.Next)    Debug.Log($"[GuestInput] {input.Label}: carta siguiente");
            if (s.Confirm) Debug.Log($"[GuestInput] {input.Label}: CONFIRMAR");
            if (s.Cancel)  Debug.Log($"[GuestInput] {input.Label}: CANCELAR");
        }
    }
}
