using System.Collections.Generic;
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

    [Header("Zig-Zag Settings")]
    public bool useZigZag = true;
    public float zigZagWidth = 0.5f;
    public int zigZagSegments = 6;

    private float fixedY;
    private readonly Queue<Vector3> waypoints = new Queue<Vector3>();
    private Vector3 currentTarget;
    private bool hasTarget = false;

    void Start()
    {
        cam = Camera.main;
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<PointMsg>("/unity_clicked_point");

        if (cube == null)
        {
            cube = GameObject.Find("Cube")?.transform;
        }

        if (cube != null)
        {
            fixedY = cube.position.y;

            Vector3 startEuler = cube.rotation.eulerAngles;
            cube.rotation = Quaternion.Euler(fixedXRotation, startEuler.y, fixedZRotation);
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

        HandleMouseClick();
        MoveAlongPath();
    }

    void HandleMouseClick()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit))
            return;

        Vector3 clickedPoint = hit.point;
        Vector3 goal = new Vector3(clickedPoint.x, fixedY, clickedPoint.z);

        Debug.Log("Clicked: " + goal);

        BuildPath(cube.position, goal);

        PointMsg msg = new PointMsg(clickedPoint.x, clickedPoint.y, clickedPoint.z);

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

    void BuildPath(Vector3 start, Vector3 goal)
    {
        waypoints.Clear();

        Vector3 flatStart = new Vector3(start.x, fixedY, start.z);
        Vector3 flatGoal = new Vector3(goal.x, fixedY, goal.z);

        if (!useZigZag || zigZagSegments < 2)
        {
            waypoints.Enqueue(flatGoal);
        }
        else
        {
            Vector3 mainDir = (flatGoal - flatStart).normalized;
            Vector3 sideDir = Vector3.Cross(Vector3.up, mainDir).normalized;

            float totalDistance = Vector3.Distance(flatStart, flatGoal);

            for (int i = 1; i <= zigZagSegments; i++)
            {
                float t = (float)i / zigZagSegments;
                Vector3 basePoint = Vector3.Lerp(flatStart, flatGoal, t);

                if (i < zigZagSegments)
                {
                    float side = (i % 2 == 0) ? -zigZagWidth : zigZagWidth;
                    basePoint += sideDir * side;
                }

                basePoint.y = fixedY;
                waypoints.Enqueue(basePoint);
            }
        }

        if (waypoints.Count > 0)
        {
            currentTarget = waypoints.Dequeue();
            hasTarget = true;
        }
        else
        {
            hasTarget = false;
        }
    }

    void MoveAlongPath()
    {
        if (!hasTarget)
            return;

        Vector3 flatCurrent = new Vector3(cube.position.x, fixedY, cube.position.z);
        Vector3 flatTarget = new Vector3(currentTarget.x, fixedY, currentTarget.z);
        Vector3 direction = flatTarget - flatCurrent;

        float distance = direction.magnitude;

        if (distance <= stopDistance)
        {
            cube.position = flatTarget;

            if (waypoints.Count > 0)
            {
                currentTarget = waypoints.Dequeue();
            }
            else
            {
                hasTarget = false;
                Debug.Log("Dog reached final target.");
            }

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

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        Vector3? prev = null;

        if (hasTarget)
        {
            Gizmos.DrawSphere(currentTarget, 0.08f);
            prev = cube != null ? cube.position : currentTarget;
            Gizmos.DrawLine(prev.Value, currentTarget);
            prev = currentTarget;
        }

        foreach (Vector3 wp in waypoints)
        {
            Gizmos.DrawSphere(wp, 0.08f);

            if (prev.HasValue)
                Gizmos.DrawLine(prev.Value, wp);

            prev = wp;
        }
    }
}