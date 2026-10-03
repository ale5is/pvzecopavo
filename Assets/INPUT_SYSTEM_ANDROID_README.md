# Migración de controles a Input System (Android)

Este paquete elimina las llamadas de los scripts a `UnityEngine.Input` y las concentra en `InputCompat`, que usa `UnityEngine.InputSystem`.

## Configuración obligatoria en Unity

1. Instalar/activar `Input System` (`com.unity.inputsystem`).
2. Ir a `Edit > Project Settings > Player > Active Input Handling`.
3. Seleccionar `Input System Package (New)`.
4. Reiniciar Unity cuando lo solicite.
5. No usar `Both` ni `Input Manager (Old)` para este proyecto.
6. El `EventSystem` debe usar `InputSystemUIInputModule`. `InputSystemUIBootstrap` lo instala automáticamente al cargar una escena.

## Jugadores locales

- Jugador 1: mouse.
- Jugador 2: WASD.
- Jugador 3: flechas.
- Jugador 4: gamepad 1.
- Los perfiles siguen siendo editables desde `GuestPlayerManager`.
- `GuestInput` usa el Input System nuevo para teclado y gamepad.
- `InputCompat` traduce mouse/touch, teclado y rueda usados por el código existente.

## Android

En Android, el click izquierdo de la capa de compatibilidad puede proceder del mouse o del toque primario cuando no hay mouse disponible. Esto permite que las rutinas existentes de colocación, pala, guante, cartas y selección sigan funcionando sin reactivar el Input Manager antiguo.

## Importante

El ZIP contiene scripts. No contiene `ProjectSettings`, escenas ni `Packages/manifest.json`, por lo que la selección de `Input System Package (New)` debe hacerse en el proyecto de Unity.
