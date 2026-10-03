MIGRACION AL NUEVO INPUT SYSTEM

Esta carpeta contiene la version actualizada de Scripts(5) para la migracion de entrada.

CAMBIOS PRINCIPALES
- Se agrego 00_Core/InputCompat.cs.
- Se agrego 00_Core/SceneInputCompat.cs.
- Las llamadas directas a UnityEngine.Input de los scripts de entrada migrados fueron reemplazadas por InputCompat.
- Input.mousePosition fue reemplazado por InputCompat.mousePosition donde correspondia.
- Input.GetKey/GetKeyDown/GetKeyUp/GetMouseButton/GetMouseButtonDown/GetMouseButtonUp/GetAxis/touchCount/GetTouch fueron migrados.
- Se corrigio la ambiguedad de TouchPhase usando UnityEngine.TouchPhase.
- GuestInput usa el nuevo Input System para teclado y conserva Gamepad del Input System.
- Los switches de la capa de input migrada fueron reemplazados por if/else.
- SceneInputCompat mantiene compatibilidad con los antiguos OnMouseEnter, OnMouseExit, OnMouseDown, OnMouseUp y OnMouseOver sin depender del Input Manager antiguo.
- SceneInputCompat hace raycast 2D/3D sin asignaciones de arrays por frame.
- SceneInputCompat se crea automaticamente al iniciar el juego; no necesita agregarse manualmente a una escena.

IMPORTANTE
- En Player Settings debe estar activo Input System Package (New).
- El EventSystem de las escenas debe usar InputSystemUIInputModule para la UI.
- Los objetos de escena que deban recibir clic necesitan Collider o Collider2D, igual que antes.
- No es necesario agregar Physics2DRaycaster ni PhysicsRaycaster para SceneInputCompat, porque el puente hace el raycast directamente.
- No se modificaron switches preexistentes de logica de juego que no forman parte de la migracion de entrada. Se dejaron intactos para no cambiar comportamiento ajeno al sistema de input.
