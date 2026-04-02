using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class ClickToGoal : MonoBehaviour
{
    Camera cam;
    ROSConnection ros;
    public Transform cube;  // Reference to the cube to move
    public float moveSpeed = 0.01f;  // Speed of cube movement
    private Vector3 targetPosition;
    private bool hasTarget = false;

    void Start()
    {
        cam = Camera.main;
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<PointMsg>("/unity_clicked_point");
        
        // If cube is not assigned, try to find it
        if (cube == null)
        {
            cube = GameObject.Find("Cube")?.transform;
        }
    }

    void Update()
    {
        // Move cube towards target position if target is set
        if (hasTarget && cube != null)
        {
            cube.position = Vector3.Lerp(cube.position, targetPosition, moveSpeed * Time.deltaTime);
            
            // Stop moving when close enough to target
            if (Vector3.Distance(cube.position, targetPosition) < 0.1f)
            {
                cube.position = targetPosition;
                hasTarget = false;
                Debug.Log("Cube reached target!");
            }
        }
        
        // Handle mouse click to set new target
        if (Input.GetMouseButtonDown(0))  // Left Mouse Button Click
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))  // If raycast hits an object
            {
                Vector3 point = hit.point;
                Debug.Log("Clicked: " + point);

                // Set target position for cube
                targetPosition = point;
                hasTarget = true;

                // Create and send the message to ROS
                PointMsg msg = new PointMsg(point.x, point.y, point.z);
                
                // Only publish if ROS connection is initialized
                if (ros != null)
                {
                    try
                    {
                        ros.Publish("/unity_clicked_point", msg);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("Failed to publish to ROS: " + e.Message);
                    }
                }
            }
        }
    }
}