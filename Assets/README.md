# PvZ scripts — reorganización segura

Contenido generado a partir de `Scripts.zip`.

## Objetivo
- Reorganizar los scripts por responsabilidad sin cambiar nombres de clases.
- Conservar los `.meta` de cada script para mantener los GUID de Unity.
- Hacer solo limpiezas de bajo riesgo verificables estáticamente.
- No dividir `GameManager`, `LV`, `LVManager`, `ZombieBase`, `PlantBase`, `SocketClient` o `SocketServer` a ciegas: sus referencias serializadas, mensajes y acoplamientos pueden depender de escenas/prefabs que no están incluidos en este ZIP.

## Limpiezas aplicadas
Se eliminaron únicamente métodos `private Start()` / `private Update()` completamente vacíos:
- `ChatInput.cs`
- `CornpultZombie.cs`
- `MapManager.cs`

No se eliminaron overrides vacíos ni hooks porque pueden formar parte del contrato de las clases base.

## Estructura
- `00_Core/` — 27 scripts
- `00_Core/BootLoader/` — 1 scripts
- `01_Gameplay/World/` — 50 scripts
- `02_Gameplay/Plants/` — 68 scripts
- `03_Gameplay/Zombies/` — 64 scripts
- `04_Gameplay/CombatEffects/` — 46 scripts
- `05_Multiplayer/` — 18 scripts
- `05_Multiplayer/Messages/` — 48 scripts
- `06_Save/Models/` — 9 scripts
- `07_StartScene/` — 25 scripts
- `08_UI/` — 36 scripts
- `09_Types/` — 37 scripts
- `10_Framework/FTRuntime/` — 22 scripts
- `11_Framework/Reanim2UnityAnim/` — 1 scripts
- `12_Framework/FlashTools/` — 1 scripts
- `99_Meta/Properties/` — 1 scripts

## Nota importante sobre Unity
Los scripts mantienen sus nombres de clase y sus `.meta`. Mover un `.cs` junto con su `.meta` conserva su GUID. Las nuevas carpetas no incluyen metas propias; Unity puede regenerarlas.

Antes de reemplazar tu carpeta original:
1. Haz una copia/commit.
2. Copia `Scripts/` reorganizado sobre tu proyecto.
3. Abre Unity y deja terminar la reimportación.
4. Ejecuta un recompilado completo y prueba escenas/prefabs.
5. Si un componente aparece como `Missing Script`, restaura el script original y revisa el `.meta`.
