using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour
{
    private Canvas mainUICanvas;

    void Start()
    {
        // Find MainUI canvas (should be in MainUI scene)
        mainUICanvas = FindObjectOfType<Canvas>();
    }

    public void GoToUnityInterface()
    {
        // Hide MainUI canvas
        if (mainUICanvas != null)
            mainUICanvas.enabled = false;
        
        // Load UnityInterface additively on top
      //  SceneManager.LoadScene("UnityInterface", LoadSceneMode.Additive);
    }

    public void GoToMainUI()
    {
        // Show MainUI canvas again
        if (mainUICanvas != null)
            mainUICanvas.enabled = true;
        
        // Unload UnityInterface scene
        //SceneManager.UnloadSceneAsync("UnityInterface");
    }
}