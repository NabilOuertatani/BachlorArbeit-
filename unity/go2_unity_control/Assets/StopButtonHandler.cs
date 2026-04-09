using UnityEngine;
using UnityEngine.UI;

public class StopButtonHandler : MonoBehaviour
{
    public ClickToGoal clickToGoal;  // Reference to ClickToGoal script
    private Button stopButton;

    void Start()
    {
        stopButton = GetComponent<Button>();
        stopButton.onClick.AddListener(OnStopPressed);
        Debug.Log("Stop button ready!");
    }

    void OnStopPressed()
    {
        if (clickToGoal != null)
        {
            clickToGoal.StopMovement();
            Debug.Log("Stop button pressed!");
        }
    }
}