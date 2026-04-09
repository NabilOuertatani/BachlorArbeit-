using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class ClickToGoal : MonoBehaviour
{
    private Camera cam;
    private ROSConnection ros;

    [Header("Dog")]
    public Transform cube;

    [Header("Movement")]
    public float moveSpeed = 2.0f;
    public float rotationSpeed = 180.0f;
    public float stopDistance = 0.05f;

    [Header("Model Orientation")]
    public float fixedXRotation = -90f;
    public float fixedZRotation = 0f;

    private Vector3 targetPosition;
    private bool hasTarget = false;
    private float fixedY;

    private const string topicName = "/unity_clicked_point";
    private static bool publisherRegistered = false;

    void Start()
    {
        // Get the main camera and ROS connection
        cam = Camera.main;
        ros = ROSConnection.GetOrCreateInstance();

        Debug.Log($"ROSConnection found and ready ({ros.RosIPAddress}:{ros.RosPort})");

        // Register the publisher for /unity_clicked_point if not already registered
        if (!publisherRegistered)
        {
            ros.RegisterPublisher<PointMsg>(topicName);
            publisherRegistered = true;
            Debug.Log("Registered publisher for /unity_clicked_point");
        }
        else
        {
            Debug.Log("Publisher already registered, skipping duplicate registration.");
        }

        // Ensure the dog model is assigned
        if (cube == null)
        {
            Debug.LogError("No dog assigned to ClickToGoal. Please drag the dog Transform into the 'cube' field in Inspector.");
            return;
        }

        // Set the fixed Y position for the dog (ensure it stays grounded)
        fixedY = 0.0f; // Make sure the dog is at ground level
        targetPosition = cube.position;

        // Set the dog's fixed rotation
        Vector3 startEuler = cube.rotation.eulerAngles;
        cube.rotation = Quaternion.Euler(fixedXRotation, startEuler.y, fixedZRotation);

        Debug.Log("Found dog model: " + cube.name);
    }

    void Update()
    {
        if (cube == null || cam == null)
            return;

        if (hasTarget)
        {
            // Move dog towards the target position while keeping the Y position fixed
            Vector3 flatTarget = new Vector3(targetPosition.x, fixedY, targetPosition.z);
            Vector3 flatCurrent = new Vector3(cube.position.x, fixedY, cube.position.z);
            Vector3 direction = flatTarget - flatCurrent;

            float distance = direction.magnitude;

            // Move the dog towards the target if it's not close enough
            if (distance > stopDistance)
            {
                direction.Normalize();

                // Rotate the dog toward the target
                if (direction != Vector3.zero)
                {
                    Quaternion lookRotation = Quaternion.LookRotation(direction);
                    float targetY = lookRotation.eulerAngles.y;

                    Quaternion desiredRotation = Quaternion.Euler(fixedXRotation, targetY, fixedZRotation);
                    cube.rotation = Quaternion.RotateTowards(cube.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
                }

                // Move the dog towards the target if it's in the right direction
                Vector3 next = Vector3.MoveTowards(flatCurrent, flatTarget, moveSpeed * Time.deltaTime);
                cube.position = new Vector3(next.x, fixedY, next.z);
            }
            else
            {
                // Reached the target
                cube.position = flatTarget;
                hasTarget = false;
                Debug.Log("Dog reached final target.");
            }
        }

        // Click detection
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Mouse click detected.");

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                Vector3 point = hit.point;
                Debug.Log($"Clicked: ({point.x:F2}, {point.y:F2}, {point.z:F2})");

                // Set the target position
                targetPosition = new Vector3(point.x, fixedY, point.z);
                hasTarget = true;

                // Publish the clicked point to ROS
                PointMsg msg = new PointMsg(point.x, point.y, point.z);

                try
                {
                    ros.Publish(topicName, msg);
                    Debug.Log("Published /unity_clicked_point");
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Failed to publish to ROS: " + e.Message);
                }
            }
            else
            {
                Debug.LogWarning("Raycast did not hit anything.");
            }
        }

        // Press SPACE to stop
        if (Input.GetKeyDown(KeyCode.Space))
        {
            hasTarget = false;
            Debug.Log("✓ Dog stopped by SPACE key!");
        }
    }

    // Public method to stop movement
    public void StopMovement()
    {
        hasTarget = false;
        Debug.Log("Dog movement stopped!");

        // Also send stop signal to ROS2 - publish current position as goal
        // This stops the robot immediately
        if (ros != null)
        {
            try
            {
                // Publish current position as new goal (stops movement)
                PointMsg stopMsg = new PointMsg(cube.position.x, cube.position.y, cube.position.z);
                ros.Publish(topicName, stopMsg);
                Debug.Log("✓ Stop command sent to ROS2");
            }
            catch (System.Exception e)
            {
                Debug.LogError("Failed to send stop to ROS: " + e.Message);
            }
        }
    }
}