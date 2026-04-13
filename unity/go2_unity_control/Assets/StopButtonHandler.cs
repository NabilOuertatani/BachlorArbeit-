using UnityEngine;
using UnityEngine.UI;

public class StopButtonHandler : MonoBehaviour
{
    public DogNavigator dogNavigator;
    private Button stopButton;

    void Start()
    {
        stopButton = GetComponent<Button>();
        stopButton.onClick.AddListener(OnStopPressed);
        Debug.Log("Stop button ready!");
    }

    void OnStopPressed()
    {
        if (dogNavigator != null)
        {
            dogNavigator.StopMovement();
            Debug.Log("Stop button pressed!");
        }
    }
}