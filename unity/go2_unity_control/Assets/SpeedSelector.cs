using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the speed selection UI panel.
/// Shows after each waypoint is placed, allows user to select speed,
/// and fires an event when speed is confirmed.
/// </summary>
public class SpeedSelector : MonoBehaviour
{
    [Header("UI References")]
    public GameObject speedPanel;
    public TextMeshProUGUI speedLabel;
    public Button slowButton;
    public Button normalButton;
    public Button fastButton;
    public TextMeshProUGUI selectedSpeedText;
    public TMP_InputField customSpeedInput;

    [Header("Speed Presets")]
    public float slowSpeed = 0.2f;
    public float normalSpeed = 0.4f;
    public float fastSpeed = 0.8f;

    private float _selectedSpeed = 0.4f;
    private int _currentWaypointIndex = -1;
    private bool _speedConfirmed = false;

    /// <summary>Event fired when user confirms speed selection (button click or Enter).</summary>
    public event System.Action<float> OnSpeedSelected;
    
    /// <summary>Event fired when speed is finalized - for auto-saving to JSON</summary>
    public event System.Action<int, float> OnSpeedConfirmed;  // waypointIndex, speed

    void Start()
    {
        if (speedPanel != null)
            speedPanel.SetActive(false);

        // Register button listeners - hide panel after selection
        if (slowButton != null)
            slowButton.onClick.AddListener(() => { SetSpeed(slowSpeed); Hide(); });
        if (normalButton != null)
            normalButton.onClick.AddListener(() => { SetSpeed(normalSpeed); Hide(); });
        if (fastButton != null)
            fastButton.onClick.AddListener(() => { SetSpeed(fastSpeed); Hide(); });

        if (customSpeedInput != null)
        {
            customSpeedInput.onEndEdit.AddListener(OnCustomSpeedInput);
        }

        _selectedSpeed = normalSpeed;
        UpdateDisplay();
    }

    /// <summary>
    /// Shows the speed panel for a new waypoint.
    /// Called after TryAddWaypoint places a waypoint visually.
    /// </summary>
    /// <param name="waypointIndex">0-based waypoint number for the label.</param>
    public void Show(int waypointIndex)
    {
        _currentWaypointIndex = waypointIndex;
        _speedConfirmed = false;
        _selectedSpeed = normalSpeed; // Default to normal

        Debug.Log($"[SpeedSelector] Show() called for waypoint {waypointIndex + 1}");

        if (speedLabel != null)
        {
            speedLabel.text = $"Set speed for waypoint {waypointIndex + 1}:";
            Debug.Log("[SpeedSelector] Label updated");
        }
        else
        {
            Debug.LogWarning("[SpeedSelector] speedLabel is NULL!");
        }

        UpdateDisplay();

        if (speedPanel != null)
        {
            speedPanel.SetActive(true);
            Debug.Log("[SpeedSelector] Panel activated");
        }
        else
        {
            Debug.LogError("[SpeedSelector] speedPanel is NULL — cannot show!");
        }

        Debug.Log($"[SpeedSelector] Showing panel for waypoint {waypointIndex + 1}");
    }

    /// <summary>
    /// Hides the speed panel. Called when ADD POINTS is pressed.
    /// </summary>
    public void Hide()
    {
        if (speedPanel != null)
            speedPanel.SetActive(false);

        _speedConfirmed = true;
        
        // Fire event so MultiGoalManager can update the waypoint and save to JSON
        if (OnSpeedConfirmed != null)
        {
            OnSpeedConfirmed.Invoke(_currentWaypointIndex, _selectedSpeed);
        }
        
        Debug.Log($"[SpeedSelector] Hidden — confirmed speed {_selectedSpeed:F2} m/s for waypoint {_currentWaypointIndex + 1}");
    }

    /// <summary>
    /// Returns the currently selected speed value.
    /// </summary>
    /// <returns>Speed in metres per second.</returns>
    public float GetSelectedSpeed()
    {
        return _selectedSpeed;
    }

    /// <summary>
    /// Sets the speed from a preset button (Slow/Normal/Fast).
    /// </summary>
    /// <param name="speed">Speed in metres per second.</param>
    public void SetSpeed(float speed)
    {
        _selectedSpeed = Mathf.Clamp(speed, 0.1f, 1.0f);
        UpdateDisplay();
        Debug.Log($"[SpeedSelector] Speed set to {_selectedSpeed:F2} m/s");
    }

    /// <summary>
    /// Handles custom speed input from the TMP_InputField.
    /// Hides panel after user presses Enter.
    /// </summary>
    /// <param name="input">User-typed speed value.</param>
    private void OnCustomSpeedInput(string input)
    {
        if (float.TryParse(input, out float speed))
        {
            SetSpeed(speed);
            Hide();  // Hide panel after custom speed is confirmed via Enter
            Debug.Log($"[SpeedSelector] Custom speed {_selectedSpeed:F2} m/s confirmed, panel hidden");
        }
        else
        {
            Debug.LogWarning($"[SpeedSelector] Invalid speed input: '{input}'");
        }
    }

    /// <summary>
    /// Updates the displayed speed text to show current selection.
    /// </summary>
    private void UpdateDisplay()
    {
        if (selectedSpeedText != null)
            selectedSpeedText.text = $"Selected: {_selectedSpeed:F2} m/s";

        // Highlight the matching preset button
        HighlightPresetButton();
    }

    /// <summary>
    /// Visually highlights the preset button that matches the current speed.
    /// </summary>
    private void HighlightPresetButton()
    {
        // Slight tolerance for float comparison
        const float tolerance = 0.01f;

        if (slowButton != null)
            slowButton.targetGraphic.color = 
                Mathf.Abs(_selectedSpeed - slowSpeed) < tolerance ? new Color(0, 1, 0) : Color.white;

        if (normalButton != null)
            normalButton.targetGraphic.color = 
                Mathf.Abs(_selectedSpeed - normalSpeed) < tolerance ? new Color(0, 1, 0) : Color.white;

        if (fastButton != null)
            fastButton.targetGraphic.color = 
                Mathf.Abs(_selectedSpeed - fastSpeed) < tolerance ? new Color(0, 1, 0) : Color.white;
    }

    /// <summary>
    /// Returns the speed category name (Slow/Normal/Fast) for display on markers.
    /// </summary>
    /// <param name="speed">Speed in metres per second.</param>
    /// <returns>Human-readable speed label.</returns>
    public static string GetSpeedLabel(float speed)
    {
        if (speed < 0.3f) return "Slow";
        if (speed < 0.6f) return "Normal";
        return "Fast";
    }

    /// <summary>
    /// Returns the color for a waypoint marker based on speed.
    /// </summary>
    /// <param name="speed">Speed in metres per second.</param>
    /// <returns>Color for the marker.</returns>
    public static Color GetSpeedColor(float speed)
    {
        if (speed < 0.3f) return new Color(0f, 0f, 1f);  // Blue (Slow)
        if (speed < 0.6f) return new Color(1f, 0.8f, 0f); // Yellow (Normal)
        return new Color(1f, 0f, 0f);  // Red (Fast)
    }
}
