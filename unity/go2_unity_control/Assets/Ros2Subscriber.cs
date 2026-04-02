using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class Ros2Subscriber : MonoBehaviour
{
    Camera cam;
    ROSConnection ros;

    void Start()
    {
        // Initialize the camera and ROS connection
        cam = Camera.main;
        ros = ROSConnection.GetOrCreateInstance();

        // Register the publisher to the ROS topic
        ros.RegisterPublisher<PointMsg>("/unity_clicked_point");
    }

    void Update()
    {
        // Detect mouse click (left click)
        if (Input.GetMouseButtonDown(0))
        {
            // Create a ray from the camera based on the mouse position
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Check if the ray hits an object in the scene
            if (Physics.Raycast(ray, out hit))
            {
                Vector3 point = hit.point; // Get the point on the surface
                Debug.Log("Clicked: " + point); // Print the clicked point

                // Create a message with the clicked position
                PointMsg msg = new PointMsg(
                    point.x, // X position
                    point.y, // Y position
                    point.z  // Z position
                );

                // Publish the message to ROS2 topic
                ros.Publish("/unity_clicked_point", msg);
            }
        }
    }
}