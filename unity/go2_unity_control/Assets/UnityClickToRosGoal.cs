using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class UnityClickToRosGoal : MonoBehaviour
{
    [Header("ROS")]
    public string topicName = "/unity_clicked_point";

    [Header("Scene References")]
    public Camera sceneCamera;
    public Transform goalMarker;

    private ROSConnection ros;
    private static bool publisherRegistered = false;

    void Start()
    {
        if (sceneCamera == null)
            sceneCamera = Camera.main;

        if (sceneCamera == null)
        {
            Debug.LogError("UnityClickToRosGoal: No camera assigned and no Main Camera found.");
            enabled = false;
            return;
        }

        ros = ROSConnection.GetOrCreateInstance();

        if (!publisherRegistered)
        {
            ros.RegisterPublisher<PointMsg>(topicName);
            publisherRegistered = true;
        }

        Debug.Log("UnityClickToRosGoal ready.");
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PublishClickedPoint();
        }
    }

    private void PublishClickedPoint()
    {
        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        Vector3 unityPoint = hit.point;

        if (goalMarker != null)
            goalMarker.position = unityPoint;

        // Temporary Unity -> ROS conversion
        // Unity: x right, y up, z forward
        // ROS map/FLU-style point: x forward, y left, z up
        // So we send: ROS = (z, -x, y)
        Vector3 rosPoint = new Vector3(
            unityPoint.z,
            -unityPoint.x,
            unityPoint.y
        );

        PointMsg msg = new PointMsg(rosPoint.x, rosPoint.y, rosPoint.z);
        ros.Publish(topicName, msg);

        Debug.Log($"Unity click world point: {unityPoint}");
        Debug.Log($"Published ROS point: ({rosPoint.x:F3}, {rosPoint.y:F3}, {rosPoint.z:F3})");
    }
}