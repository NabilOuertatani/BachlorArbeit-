using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class EventSystemEnforcer
{
    private const string PreferredSceneName = "MainUI";
    private static bool _sceneLoadedHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (!_sceneLoadedHooked)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneLoadedHooked = true;
        }

        EnsureSingleEventSystem();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSingleEventSystem();
    }

    private static void EnsureSingleEventSystem()
    {
        EventSystem[] eventSystems = Object.FindObjectsOfType<EventSystem>();
        if (eventSystems.Length <= 1)
            return;

        EventSystem keep = null;

        for (int i = 0; i < eventSystems.Length; i++)
        {
            EventSystem eventSystem = eventSystems[i];
            if (eventSystem != null && eventSystem.gameObject.scene.name == PreferredSceneName)
            {
                keep = eventSystem;
                break;
            }
        }

        if (keep == null)
            keep = eventSystems[0];

        for (int i = 0; i < eventSystems.Length; i++)
        {
            EventSystem eventSystem = eventSystems[i];
            if (eventSystem != null && eventSystem != keep)
                Object.Destroy(eventSystem.gameObject);
        }
    }
}