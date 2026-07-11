using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space canvas marker shown at each waypoint: a teal-outlined circle
/// with a white fill and a mono number inside. Billboarded to the camera.
/// </summary>
public class WaypointMarker : MonoBehaviour
{
    [Header("Wired by RedesignBuilder")]
    public Image ring;    // accent outline
    public Image fill;    // white/dark inner disc
    public TMP_Text numberText;

    public void SetNumber(int number)
    {
        if (numberText != null) numberText.text = number.ToString();
    }
}
