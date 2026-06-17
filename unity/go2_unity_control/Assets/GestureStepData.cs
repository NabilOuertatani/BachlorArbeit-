using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GestureStepData
{
    public string stepName;
    public List<WaypointWithSpeed> waypoints = new List<WaypointWithSpeed>();

    public GestureStepData(string stepName)
    {
        this.stepName = stepName;
    }
    
    /// <summary>Get waypoint positions only (for compatibility with methods expecting List<Vector3>)</summary>
    public List<Vector3> GetWaypointPositions()
    {
        var positions = new List<Vector3>();
        foreach (var wp in waypoints)
        {
            positions.Add(wp.unityPosition);
        }
        return positions;
    }
}