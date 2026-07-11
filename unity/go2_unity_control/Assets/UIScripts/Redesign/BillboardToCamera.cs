using UnityEngine;

/// <summary>Keeps a world-space object (e.g. a waypoint marker canvas) facing the camera.</summary>
public class BillboardToCamera : MonoBehaviour
{
    private Camera _cam;

    void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        // Face the camera (canvas forward points away from the viewer)
        transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);
    }
}
