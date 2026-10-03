using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

[DefaultExecutionOrder(-10000)]
public sealed class InputSystemUIBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        EventSystem[] systems =
            Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < systems.Length; i++)
            Configure(systems[i]);
    }

    private static void Configure(EventSystem eventSystem)
    {
        if (eventSystem == null)
            return;

        InputSystemUIInputModule newModule =
            eventSystem.GetComponent<InputSystemUIInputModule>();

        if (newModule != null)
            return;

        BaseInputModule[] modules =
            eventSystem.GetComponents<BaseInputModule>();

        for (int i = 0; i < modules.Length; i++)
        {
            if (modules[i] is InputSystemUIInputModule)
                return;
        }

        for (int i = 0; i < modules.Length; i++)
        {
            if (modules[i] == null)
                continue;

            Object.Destroy(modules[i]);
        }

        eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }
}
