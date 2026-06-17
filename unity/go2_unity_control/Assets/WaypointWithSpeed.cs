using UnityEngine;
using System;

/// <summary>
/// Data structure representing a single waypoint with configurable speed.
/// Stores both Unity world coordinates and ROS frame coordinates,
/// along with the desired speed for navigation to this waypoint.
/// </summary>
[System.Serializable]
public class WaypointWithSpeed
{
    public Vector3 unityPosition;   // Unity world position (clicked location)
    public Vector3 rosGoal;         // ROS frame position (converted from Unity)
    public float speed;             // metres per second (0.1 to 1.0)

    public WaypointWithSpeed(Vector3 unityPos, Vector3 rosPos, float speedMps = 0.4f)
    {
        unityPosition = unityPos;
        rosGoal = rosPos;
        speed = Mathf.Clamp(speedMps, 0.1f, 1.0f);
    }

    public override string ToString()
    {
        return $"Waypoint(Unity: {unityPosition:F2}, ROS: {rosGoal:F2}, Speed: {speed:F2} m/s)";
    }
}
