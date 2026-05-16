using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GestureStepData
{
    public string stepName;
    public List<Vector3> waypoints = new List<Vector3>();

    public GestureStepData(string stepName)
    {
        this.stepName = stepName;
    }
}