using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the Configure screen's waypoint list:
/// a numbered badge plus mono coordinates like "x 2.1, y 0.4".
/// </summary>
public class WaypointListItem : MonoBehaviour
{
    [Header("Wired by RedesignBuilder")]
    public Image badgeBg;
    public TMP_Text badgeText;
    public TMP_Text coordsText;

    public void Bind(int number, Vector3 unityPosition, float scaleX, float scaleZ)
    {
        // Show ROS-style meters (the coordinate system the robot actually walks in):
        // ros_x = unity_z / scaleZ, ros_y = -unity_x / scaleX — same mapping MultiGoalManager uses.
        float x = unityPosition.z / scaleZ;
        float y = -unityPosition.x / scaleX;

        if (badgeText != null) badgeText.text = number.ToString();
        if (coordsText != null)
            coordsText.text = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                            "x {0:0.0}, y {1:0.0}", x, y);
    }
}
