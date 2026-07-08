using UnityEngine;

/// <summary>
/// Standalone click/raycast diagnostic — completely independent of
/// MultiGoalManager, RobotBridge, or any other script. Attach this to
/// any active GameObject in the scene (a new empty GameObject is fine)
/// to find out if clicks and raycasts work at all.
/// </summary>
public class ClickDiagnostic : MonoBehaviour
{
    public Camera testCamera;

    void Start()
    {
        Debug.Log("[ClickDiagnostic] Script is ALIVE and running Start().");

        if (testCamera == null)
        {
            testCamera = Camera.main;
            Debug.Log("[ClickDiagnostic] No camera assigned — using Camera.main = " +
                      (testCamera != null ? testCamera.name : "NULL"));
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("[ClickDiagnostic] Mouse click detected at " + Input.mousePosition);

            if (testCamera == null)
            {
                Debug.LogWarning("[ClickDiagnostic] No camera available — cannot raycast.");
                return;
            }

            Ray ray = testCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
            {
                Debug.Log("[ClickDiagnostic] HIT: " + hit.collider.name +
                          " on layer " + LayerMask.LayerToName(hit.collider.gameObject.layer) +
                          " at " + hit.point);
            }
            else
            {
                Debug.Log("[ClickDiagnostic] Raycast MISSED everything.");
            }
        }
    }
}