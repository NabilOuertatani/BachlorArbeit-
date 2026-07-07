using UnityEngine;

/// <summary>
/// Toggles between the Config screen and the Dog/Waypoints panel.
/// Single-scene setup — no more additive scene loading/unloading.
/// </summary>
public class SceneSwitcher : MonoBehaviour
{
    [Header("Panels to toggle")]
    public GameObject configScene;   // "Configscene" under MainPanel
    public GameObject dogPannel;     // "DogPannel" under MainPanel

    /// <summary>Hide Config screen, show the Dog/Waypoints panel.</summary>
    public void GoToUnityInterface()
    {
        if (configScene != null)
            configScene.SetActive(false);

        if (dogPannel != null)
            dogPannel.SetActive(true);
    }

    /// <summary>Hide the Dog/Waypoints panel, show Config screen again.</summary>
    public void GoToMainUI()
    {
        if (configScene != null)
            configScene.SetActive(true);

        if (dogPannel != null)
            dogPannel.SetActive(false);
    }
}