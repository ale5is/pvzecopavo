using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reemplaza OnMouseEnter/Exit/Over/Down/Up del Input Manager antiguo para objetos de escena
/// (Collider / Collider2D). Funciona con mouse y con pantalla tactil (primer dedo).
/// </summary>
public sealed class SceneInputCompat : MonoBehaviour
{
    private static SceneInputCompat instance;
    private GameObject hoveredObject;
    private GameObject pressedObject;
    private Camera cachedCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("Scene Input Compat");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SceneInputCompat>();
    }

    private void Update()
    {
        // Antes se salia si no habia Mouse: en celular (solo Touchscreen) nunca se enviaba nada.
        if (Mouse.current == null && Touchscreen.current == null)
        {
            ClearHover();
            pressedObject = null;
            return;
        }

        Camera camera = GetCamera();
        if (camera == null)
        {
            ClearHover();
            return;
        }

        bool isTouch = InputCompat.IsTouchPointer;
        bool down = InputCompat.GetMouseButtonDown(0);
        bool up = InputCompat.GetMouseButtonUp(0);
        bool held = InputCompat.GetMouseButton(0);

        // Con el dedo no existe "hover": sin contacto no hay objeto bajo el puntero.
        if (isTouch && !down && !held && !up)
        {
            ClearHover();
            pressedObject = null;
            return;
        }

        GameObject current = RaycastObject(camera, InputCompat.mousePosition);
        UpdateHover(current);

        if (current != null && down)
        {
            pressedObject = current;
            current.SendMessage("OnMouseDown", SendMessageOptions.DontRequireReceiver);
        }

        if (current != null)
            current.SendMessage("OnMouseOver", SendMessageOptions.DontRequireReceiver);

        if (up)
        {
            if (pressedObject != null)
            {
                pressedObject.SendMessage("OnMouseUp", SendMessageOptions.DontRequireReceiver);
                pressedObject = null;
            }

            // Al levantar el dedo el puntero desaparece: se cierra el hover (OnMouseExit).
            if (isTouch)
                ClearHover();
        }
    }

    private Camera GetCamera()
    {
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            return cachedCamera;

        cachedCamera = Camera.main;
        return cachedCamera;
    }

    private GameObject RaycastObject(Camera camera, Vector3 screenPosition)
    {
        Ray ray = camera.ScreenPointToRay(screenPosition);

        bool found3D = Physics.Raycast(
            ray,
            out RaycastHit hit3D,
            Mathf.Infinity,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.UseGlobal);

        RaycastHit2D hit2D = Physics2D.GetRayIntersection(
            ray,
            Mathf.Infinity,
            Physics2D.DefaultRaycastLayers);

        bool found2D = hit2D.collider != null;

        if (found3D && found2D)
        {
            if (hit3D.distance <= hit2D.distance)
                return hit3D.collider.gameObject;

            return hit2D.collider.gameObject;
        }

        if (found3D)
            return hit3D.collider.gameObject;

        if (found2D)
            return hit2D.collider.gameObject;

        return null;
    }

    private void UpdateHover(GameObject current)
    {
        if (current == hoveredObject)
            return;

        if (hoveredObject != null)
            hoveredObject.SendMessage("OnMouseExit", SendMessageOptions.DontRequireReceiver);

        hoveredObject = current;

        if (hoveredObject != null)
            hoveredObject.SendMessage("OnMouseEnter", SendMessageOptions.DontRequireReceiver);
    }

    private void ClearHover()
    {
        if (hoveredObject == null)
            return;

        hoveredObject.SendMessage("OnMouseExit", SendMessageOptions.DontRequireReceiver);
        hoveredObject = null;
    }
}