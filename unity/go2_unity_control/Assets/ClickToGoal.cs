using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class ClickToGoal : MonoBehaviour
{
    Camera cam;
    ROSConnection ros;

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

    void Start()
    {
        cam = Camera.main;
        ros = ROSConnection.GetOrCreateInstance();

        Debug.Log($"ROSConnection found and ready ({ros.RosIPAddress}:{ros.RosPort})");

        ros.RegisterPublisher<PointMsg>("/unity_clicked_point");
        Debug.Log("Registered publisher for /unity_clicked_point");

        if (cube == null)
        {
            cube = GameObject.Find("Cube")?.transform;
        }

        if (cube != null)
        {
            fixedY = cube.position.y;
            targetPosition = cube.position;

            Vector3 startEuler = cube.rotation.eulerAngles;
            cube.rotation = Quaternion.Euler(fixedXRotation, startEuler.y, fixedZRotation);

            Debug.Log("Found dog model: " + cube.name);
        }
        else
        {
            Debug.LogError("No dog assigned to ClickToGoal.");
        }
    }

    void Update()
    {
        if (cube == null || cam == null)
            return;

        if (hasTarget)
        {
            Vector3 flatTarget = new Vector3(targetPosition.x, fixedY, targetPosition.z);
            Vector3 flatCurrent = new Vector3(cube.position.x, fixedY, cube.position.z);
            Vector3 direction = flatTarget - flatCurrent;

            float distance = direction.magnitude;

            if (distance <= stopDistance)
            {
                cube.position = flatTarget;
                hasTarget = false;
                Debug.Log("Dog reached final target.");
                return;
            }

            direction.Normalize();

            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                float targetY = lookRotation.eulerAngles.y;

                Quaternion desiredRotation = Quaternion.Euler(fixedXRotation, targetY, fixedZRotation);

                cube.rotation = Quaternion.RotateTowards(
                    cube.rotation,
                    desiredRotation,
                    rotationSpeed * Time.deltaTime
                );
            }

            Vector3 currentForward = cube.forward;
            Vector3 flatForward = new Vector3(currentForward.x, 0f, currentForward.z).normalized;
            float angle = Vector3.Angle(flatForward, direction);

            if (angle < 15f)
            {
                Vector3 next = Vector3.MoveTowards(
                    flatCurrent,
                    flatTarget,
                    moveSpeed * Time.deltaTime
                );

                cube.position = new Vector3(next.x, fixedY, next.z);
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Mouse click detected.");

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                Vector3 point = hit.point;
                Debug.Log($"Clicked: ({point.x:F2}, {point.y:F2}, {point.z:F2})");

                targetPosition = new Vector3(point.x, fixedY, point.z);
                hasTarget = true;

                PointMsg msg = new PointMsg(point.x, point.y, point.z);

                try
                {
                    ros.Publish("/unity_clicked_point", msg);
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
    }
}